using System.Globalization;
using System.Text.Json;
using Google.Cloud.Firestore;
using Iderma.Core.Services;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public interface IInventarioNube
{
    void Publicar(IReadOnlyList<FichaTecnica> fichas, IReadOnlyCollection<Guid> eliminadas, bool reemplazarColeccion);
}

public static class InventarioNube
{
    private static readonly Motor _motor = new();

    public static string Estado => _motor.Estado;

    public static event Action? EstadoCambiado
    {
        add => _motor.EstadoCambiado += value;
        remove => _motor.EstadoCambiado -= value;
    }

    public static void Preparar(FichaRepository repositorio) => _motor.Preparar(repositorio);

    public static void Iniciar(FichaRepository repositorio) =>
        _motor.Iniciar(repositorio, VistaLecturaInventario.Inventario);

    public static void Iniciar(FichaRepository repositorio, VistaLecturaInventario vista) =>
        _motor.Iniciar(repositorio, vista);

    public static Task<FichaTecnica?> CargarCompletaAsync(Guid id) => _motor.CargarCompletaAsync(id);

    public static Task<IReadOnlyList<FichaTecnica>> CargarCompletasAsync(IReadOnlyList<FichaTecnica> fichas) =>
        _motor.CargarCompletasAsync(fichas);

    public static Task<string> ActualizarAsync() => _motor.ActualizarAsync();

    public static void ReemplazarCon(FichaRepository repositorio) => _motor.ReemplazarCon(repositorio);

    private sealed class Motor : IInventarioNube
    {
        private readonly object _colaLock = new();
        private readonly Queue<Func<Task>> _cola = new();
        private readonly SemaphoreSlim _escrituras = new(1, 1);
        private readonly SemaphoreSlim _conexion = new(1, 1);
        private FichaRepository? _repositorio;
        private readonly object _cursorLock = new();
        private FirestoreDb? _base;
        private FirestoreChangeListener? _escucha;
        private FirestoreChangeListener? _escuchaBajas;
        private Task _colaEnCurso = Task.CompletedTask;
        private Timestamp _cursor;
        private Timestamp _cursorDatos;
        private Timestamp _cursorBajas;
        private bool _cambiosInicial;
        private bool _bajasInicial;
        private bool _datosMarco;
        private bool _bajasMarco;
        private int _procesandoCola;
        private int _reemplazoEnCurso;
        private bool _sincronizacionInicial;
        private readonly HashSet<string> _campos = new(StringComparer.Ordinal);
        private int _quiereEscucha;
        private VistaLecturaInventario _vista = VistaLecturaInventario.Inventario;
        private string _estado = "Inventario local";

        public string Estado => _estado;

        public event Action? EstadoCambiado;

        public void Preparar(FichaRepository repositorio)
        {
            _repositorio = repositorio;
            repositorio.Nube = this;
        }

        public void Iniciar(FichaRepository repositorio, VistaLecturaInventario vista)
        {
            Preparar(repositorio);
            _vista = vista;
            var pedidos = CamposLecturaInventario.De(vista);
            var cambio = _campos.Count != pedidos.Count || pedidos.Any(campo => !_campos.Contains(campo));
            if (cambio)
            {
                _campos.Clear();
                foreach (var campo in pedidos)
                {
                    _campos.Add(campo);
                }
            }

            Volatile.Write(ref _quiereEscucha, 1);
            if (_escucha is not null && cambio)
            {
                _ = AmpliarCamposAsync();
            }
            else if (_escucha is null)
            {
                _ = ConectarAsync();
            }
        }

        private async Task AmpliarCamposAsync()
        {
            try
            {
                await AsegurarBaseAsync();
                if (_base is null || _escucha is null)
                {
                    return;
                }

                await _conexion.WaitAsync();
                try
                {
                    if (_escucha is null || _base is null)
                    {
                        return;
                    }

                    var repo = _repositorio;
                    if (repo is not null && repo.CopiarTodas().Any(f => !f.TieneFichaCompleta))
                    {
                        var snap = await SoloColumnas(_base.Collection(InventarioDocumento.Coleccion)).GetSnapshotAsync();
                        if (snap.Count > 0)
                        {
                            AplicarCompleto(repo, snap);
                        }
                    }

                    await _escucha.StopAsync();
                    _escucha = null;
                    _cambiosInicial = false;
                    await EscucharCambiosAsync();
                    CambiarEstado(AvisoListas());
                }
                finally
                {
                    _conexion.Release();
                }
            }
            catch (Exception ex)
            {
                CambiarEstado("No se pudieron ampliar las columnas de la lista. " + ex.Message);
            }
        }

        public async Task<FichaTecnica?> CargarCompletaAsync(Guid id)
        {
            var cargadas = await CargarPorIdAsync([id]);
            return cargadas.Count > 0 ? cargadas[0] : _repositorio?.Obtener(id);
        }

        public async Task<IReadOnlyList<FichaTecnica>> CargarCompletasAsync(IReadOnlyList<FichaTecnica> fichas)
        {
            if (fichas.Count == 0)
            {
                return fichas;
            }

            await CargarPorIdAsync(fichas.Select(f => f.Id).ToList());
            var repo = _repositorio;
            if (repo is null)
            {
                return fichas;
            }

            return fichas.Select(f => repo.Obtener(f.Id) ?? f).ToList();
        }

        public async Task<string> ActualizarAsync()
        {
            try
            {
                Volatile.Write(ref _quiereEscucha, 1);
                await ConectarAsync();
                var cantidad = _repositorio?.CopiarTodas().Count ?? 0;
                if (_escucha is null)
                {
                    return "No se pudo conectar el inventario en vivo.";
                }

                CambiarEstado(AvisoListas());
                return $"Inventario en vivo ({cantidad} fichas). Los cambios llegan solos.";
            }
            catch (Exception ex)
            {
                CambiarEstado("No se pudo actualizar el inventario. " + ex.Message);
                return "No se pudo actualizar el inventario. " + ex.Message;
            }
        }

        public void ReemplazarCon(FichaRepository repositorio)
        {
            var copia = repositorio.CopiarTodas();
            Encolar(() => ReemplazarColeccionAsync(copia));
            _ = AsegurarEscrituraAsync();
        }

        public void Publicar(IReadOnlyList<FichaTecnica> fichas, IReadOnlyCollection<Guid> eliminadas, bool reemplazarColeccion)
        {
            var copia = fichas.Select(f => f.Clonar()).ToList();
            var borradas = eliminadas.ToList();
            Encolar(() => reemplazarColeccion
                ? ReemplazarColeccionAsync(copia)
                : EscribirCambiosAsync(copia, borradas));
            _ = AsegurarEscrituraAsync();
        }

        private async Task ConectarAsync()
        {
            try
            {
                if (Volatile.Read(ref _quiereEscucha) == 1 && _escucha is null)
                {
                    CambiarEstado("Conectando el inventario con Firebase…");
                }

                await AsegurarBaseAsync();
                if (_base is null)
                {
                    if (Volatile.Read(ref _quiereEscucha) == 1)
                    {
                        CambiarEstado("Inventario local: no se encontraron las credenciales de Firebase.");
                    }

                    return;
                }

                await EsperarColaAsync();
                if (Volatile.Read(ref _quiereEscucha) != 1 || _escucha is not null)
                {
                    return;
                }

                await _conexion.WaitAsync();
                try
                {
                    if (_escucha is null && _base is not null)
                    {
                        await EscucharCambiosAsync();
                        CambiarEstado(AvisoListas());
                        await QuitarCamposRetiradosAsync();
                    }
                }
                finally
                {
                    _conexion.Release();
                }
            }
            catch (Exception ex)
            {
                Volatile.Write(ref _quiereEscucha, 0);
                CambiarEstado("Inventario local: no hay conexión con Firebase. " + ex.Message);
            }
        }

        private async Task QuitarCamposRetiradosAsync()
        {
            if (_repositorio is null || File.Exists(RutaLimpiezaCampos))
            {
                return;
            }

            var fichas = _repositorio.CopiarTodas();
            _repositorio.Guardar();
            if (fichas.Count > 0)
            {
                await EscribirCambiosAsync(fichas, []);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(RutaLimpiezaCampos)!);
            File.WriteAllText(RutaLimpiezaCampos, "ok");
        }

        private static string RutaLimpiezaCampos { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IdermaCapilar",
            "inventario-sin-presentacion.txt");

        private async Task AsegurarEscrituraAsync()
        {
            try
            {
                await AsegurarBaseAsync();
                if (_base is null)
                {
                    return;
                }

                await EsperarColaAsync();
            }
            catch (Exception ex)
            {
                CambiarEstado("Inventario local: no hay conexión con Firebase. " + ex.Message);
            }
        }

        private async Task AsegurarBaseAsync()
        {
            if (_base is not null)
            {
                return;
            }

            await _conexion.WaitAsync();
            try
            {
                if (_base is not null)
                {
                    return;
                }

                _base = await ConexionFirebase.AbrirAsync();
            }
            finally
            {
                _conexion.Release();
            }
        }

        private async Task EsperarColaAsync()
        {
            while (true)
            {
                Task tarea;
                lock (_colaLock)
                {
                    if (_cola.Count == 0 && Volatile.Read(ref _procesandoCola) == 0)
                    {
                        return;
                    }

                    if (_colaEnCurso.IsCompleted)
                    {
                        _colaEnCurso = ProcesarColaAsync();
                    }

                    tarea = _colaEnCurso;
                }

                await tarea;
            }
        }

        private void AlRecibir(QuerySnapshot snapshot)
        {
            var repositorio = _repositorio;
            if (Volatile.Read(ref _reemplazoEnCurso) > 0 || repositorio is null)
            {
                return;
            }

            try
            {
                if (!_sincronizacionInicial)
                {
                    _sincronizacionInicial = true;
                    if (snapshot.Count == 0)
                    {
                        var local = repositorio.CopiarTodas();
                        if (local.Count > 0)
                        {
                            Encolar(() => ReemplazarColeccionAsync(local));
                        }

                        return;
                    }

                    AplicarCompleto(repositorio, snapshot);
                    return;
                }

                var cambios = new List<FichaTecnica>();
                var eliminadas = new List<Guid>();
                foreach (var cambio in snapshot.Changes)
                {
                    if (cambio.ChangeType == DocumentChange.Type.Removed)
                    {
                        if (Guid.TryParse(cambio.Document.Id, out var id))
                        {
                            eliminadas.Add(id);
                        }

                        continue;
                    }

                    if (LeerParcial(cambio.Document, out var ficha, out var cambioFicha) && cambioFicha)
                    {
                        cambios.Add(ficha);
                    }
                }

                if (cambios.Count > 0 || eliminadas.Count > 0)
                {
                    repositorio.AplicarDesdeNube(cambios, eliminadas);
                }
            }
            catch (Exception ex)
            {
                CambiarEstado("Inventario en vivo, con un error al leer: " + ex.Message);
            }
        }

        private void AplicarCompleto(FichaRepository repositorio, QuerySnapshot snapshot)
        {
            var remotos = new List<FichaTecnica>();
            var ids = new HashSet<Guid>();
            foreach (var documento in snapshot.Documents)
            {
                if (!LeerParcial(documento, out var ficha, out var cambio))
                {
                    continue;
                }

                ids.Add(ficha.Id);
                if (cambio)
                {
                    remotos.Add(ficha);
                }
            }

            var sobrantes = repositorio.CopiarTodas()
                .Select(f => f.Id)
                .Where(id => !ids.Contains(id))
                .ToList();
            repositorio.AplicarDesdeNube(remotos, sobrantes);
        }

        private bool LeerParcial(DocumentSnapshot documento, out FichaTecnica ficha, out bool cambio)
        {
            cambio = false;
            ficha = new FichaTecnica();
            if (!documento.Exists)
            {
                return false;
            }

            var datos = documento.ToDictionary();
            var leida = InventarioDocumento.Leer(documento.Id, datos);
            var anterior = _repositorio?.Obtener(leida.Id);
            if (anterior is null)
            {
                leida.LecturaCompleta = false;
                ficha = leida;
                cambio = true;
                return true;
            }

            var fusion = anterior.Clonar();
            InventarioDocumento.CopiarCampos(fusion, leida, datos.Keys);
            fusion.LecturaCompleta = anterior.LecturaCompleta;
            ficha = fusion;
            cambio = InventarioDocumento.Huella(fusion) != InventarioDocumento.Huella(anterior);
            return true;
        }

        private async Task<List<FichaTecnica>> CargarPorIdAsync(IReadOnlyList<Guid> ids)
        {
            var cargadas = new List<FichaTecnica>();
            if (ids.Count == 0)
            {
                return cargadas;
            }

            try
            {
                await AsegurarBaseAsync();
                if (_base is null || _repositorio is null)
                {
                    return cargadas;
                }

                var coleccion = _base.Collection(InventarioDocumento.Coleccion);
                for (var inicio = 0; inicio < ids.Count; inicio += 100)
                {
                    var lote = ids.Skip(inicio).Take(100)
                        .Select(id => coleccion.Document(id.ToString("D")))
                        .ToList();
                    var snaps = await _base.GetAllSnapshotsAsync(lote);
                    var fichas = new List<FichaTecnica>();
                    foreach (var snap in snaps)
                    {
                        if (!snap.Exists)
                        {
                            continue;
                        }

                        var nube = InventarioDocumento.Leer(snap.Id, snap.ToDictionary());
                        nube.LecturaCompleta = true;
                        var local = _repositorio.Obtener(nube.Id);
                        if (local is null || !local.TieneFichaCompleta || local.FechaActualizacion <= nube.FechaActualizacion)
                        {
                            fichas.Add(nube);
                        }
                    }

                    if (fichas.Count > 0)
                    {
                        _repositorio.AplicarDesdeNube(fichas, []);
                        cargadas.AddRange(fichas);
                    }
                }
            }
            catch (Exception ex)
            {
                CambiarEstado("No se pudo abrir la ficha completa. " + ex.Message);
            }

            return cargadas;
        }

        private async Task CompletarAntesDeEscribirAsync(List<FichaTecnica> fichas)
        {
            var pendientes = fichas.Where(f => !f.TieneFichaCompleta).ToList();
            if (pendientes.Count == 0 || _base is null)
            {
                return;
            }

            var coleccion = _base.Collection(InventarioDocumento.Coleccion);
            var snaps = await _base.GetAllSnapshotsAsync(
                pendientes.Select(f => coleccion.Document(f.Id.ToString("D"))));
            for (var i = 0; i < pendientes.Count && i < snaps.Count; i++)
            {
                var snap = snaps[i];
                if (!snap.Exists)
                {
                    pendientes[i].LecturaCompleta = true;
                    continue;
                }

                var nube = InventarioDocumento.Leer(snap.Id, snap.ToDictionary());
                var combinada = InventarioDocumento.CombinarListaConNube(pendientes[i], nube, _campos);
                CopiarEn(pendientes[i], combinada);
            }

            _repositorio?.AplicarDesdeNube(pendientes, []);
        }

        private static void CopiarEn(FichaTecnica destino, FichaTecnica origen)
        {
            var id = destino.Id;
            var copia = origen.Clonar();
            foreach (var propiedad in typeof(FichaTecnica).GetProperties())
            {
                if (!propiedad.CanRead || !propiedad.CanWrite || propiedad.Name == nameof(FichaTecnica.Id))
                {
                    continue;
                }

                propiedad.SetValue(destino, propiedad.GetValue(copia));
            }

            destino.Id = id;
            destino.LecturaCompleta = true;
        }

        private string AvisoListas() => _vista switch
        {
            VistaLecturaInventario.Vencimientos => "Vencimientos en vivo, solo columnas de la lista",
            VistaLecturaInventario.Registro => "Registro en vivo, solo columnas de la lista",
            VistaLecturaInventario.Precios => "Historial de precios en vivo, solo columnas de la lista",
            VistaLecturaInventario.Ordenes => "Órdenes en vivo, solo columnas de la lista",
            VistaLecturaInventario.Area => "Área en vivo, solo columnas de la lista",
            _ => "Inventario en vivo, solo columnas de la tabla"
        };

        private Query SoloColumnas(Query consulta)
        {
            IEnumerable<string> campos = _campos.Count > 0 ? _campos : CamposLecturaInventario.Listas;
            var rutas = campos.Select(campo => new FieldPath(campo)).ToArray();
            return consulta.Select(rutas);
        }

        private async Task EscribirCambiosAsync(List<FichaTecnica> fichas, List<Guid> eliminadas)
        {
            if (_base is null)
            {
                return;
            }

            await _escrituras.WaitAsync();
            try
            {
                await CompletarAntesDeEscribirAsync(fichas);
                var coleccion = _base.Collection(InventarioDocumento.Coleccion);
                var bajas = _base.Collection(InventarioDocumento.Coleccion + "-bajas");
                await EnLotesAsync(fichas, (lote, ficha) =>
                    lote.Set(coleccion.Document(ficha.Id.ToString("D")), DatosInventario(ficha)));
                await EnLotesAsync(eliminadas, (lote, id) => lote.Delete(coleccion.Document(id.ToString("D"))));
                await EnLotesAsync(eliminadas, (lote, id) => lote.Set(bajas.Document(id.ToString("D")), MarcaBorrado()));
            }
            finally
            {
                _escrituras.Release();
            }
        }

        private async Task ReemplazarColeccionAsync(List<FichaTecnica> fichas)
        {
            if (_base is null)
            {
                return;
            }

            Interlocked.Increment(ref _reemplazoEnCurso);
            await _escrituras.WaitAsync();
            try
            {
                var coleccion = _base.Collection(InventarioDocumento.Coleccion);
                var bajas = _base.Collection(InventarioDocumento.Coleccion + "-bajas");
                var conservar = fichas.Select(f => f.Id.ToString("D")).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var borrar = new List<DocumentReference>();
                if (!CursorLocal.Cargar().Listo)
                {
                    var existentes = await coleccion.Select(new FieldPath("Actualizado")).GetSnapshotAsync();
                    borrar.AddRange(existentes.Documents
                        .Where(d => !conservar.Contains(d.Id))
                        .Select(d => d.Reference));
                }

                await EnLotesAsync(borrar, (lote, referencia) => lote.Delete(referencia));
                await EnLotesAsync(borrar, (lote, referencia) => lote.Set(bajas.Document(referencia.Id), MarcaBorrado()));
                await CompletarAntesDeEscribirAsync(fichas);
                await EnLotesAsync(fichas, (lote, ficha) =>
                    lote.Set(coleccion.Document(ficha.Id.ToString("D")), DatosInventario(ficha)));
            }
            finally
            {
                _escrituras.Release();
                Interlocked.Decrement(ref _reemplazoEnCurso);
            }
        }

        private async Task EnLotesAsync<T>(IReadOnlyList<T> items, Action<WriteBatch, T> agregar)
        {
            if (_base is null || items.Count == 0)
            {
                return;
            }

            const int tamano = 400;
            for (var inicio = 0; inicio < items.Count; inicio += tamano)
            {
                var lote = _base.StartBatch();
                var cuenta = 0;
                for (var i = inicio; i < items.Count && i < inicio + tamano; i++)
                {
                    agregar(lote, items[i]);
                    cuenta++;
                }

                if (cuenta > 0)
                {
                    await lote.CommitAsync();
                }
            }
        }

        private void Encolar(Func<Task> trabajo)
        {
            lock (_colaLock)
            {
                _cola.Enqueue(trabajo);
            }

            if (_base is not null)
            {
                ArrancarCola();
            }
        }

        private Task ArrancarCola()
        {
            lock (_colaLock)
            {
                if (_colaEnCurso.IsCompleted)
                {
                    _colaEnCurso = ProcesarColaAsync();
                }

                return _colaEnCurso;
            }
        }

        private async Task ProcesarColaAsync()
        {
            if (Interlocked.CompareExchange(ref _procesandoCola, 1, 0) != 0)
            {
                return;
            }

            try
            {
                while (_base is not null)
                {
                    Func<Task>? trabajo;
                    lock (_colaLock)
                    {
                        if (_cola.Count == 0)
                        {
                            break;
                        }

                        trabajo = _cola.Dequeue();
                    }

                    try
                    {
                        await trabajo();
                    }
                    catch (Exception ex)
                    {
                        CambiarEstado("Inventario en vivo, con un error al guardar: " + ex.Message);
                    }
                }
            }
            finally
            {
            Interlocked.Exchange(ref _procesandoCola, 0);
            lock (_colaLock)
            {
                if (_cola.Count > 0 && _base is not null)
                {
                    _colaEnCurso = ProcesarColaAsync();
                }
            }
            }
        }

        private async Task EscucharCambiosAsync()
        {
            if (_base is null || _escucha is not null)
            {
                return;
            }

            var cursor = CursorLocal.Cargar();
            if (!cursor.Listo)
            {
                var local = _repositorio?.CopiarTodas();
                if (local is { Count: > 0 })
                {
                    cursor = CursorLocal.Desde(Timestamp.FromDateTime(DateTime.UtcNow));
                }
                else
                {
                    var snap = await SoloColumnas(_base.Collection(InventarioDocumento.Coleccion)).GetSnapshotAsync();
                    if (snap.Count > 0 && _repositorio is not null)
                    {
                        AplicarCompleto(_repositorio, snap);
                    }

                    cursor = CursorLocal.Desde(snap.ReadTime);
                }

                cursor.Guardar();
            }

            _cursor = cursor.Marca;
            _escucha = SoloColumnas(_base.Collection(InventarioDocumento.Coleccion)
                .WhereGreaterThan("Actualizado", _cursor)
                .OrderBy("Actualizado"))
                .Listen(AlRecibirCambios);
            _escuchaBajas = _base.Collection(InventarioDocumento.Coleccion + "-bajas")
                .WhereGreaterThan("Actualizado", _cursor)
                .OrderBy("Actualizado")
                .Listen(AlRecibirBajas);
        }

        private void AlRecibirCambios(QuerySnapshot snapshot)
        {
            var repositorio = _repositorio;
            if (Volatile.Read(ref _reemplazoEnCurso) > 0 || repositorio is null)
            {
                return;
            }

            try
            {
                if (!_cambiosInicial)
                {
                    _cambiosInicial = true;
                    var cambios = new List<FichaTecnica>();
                    foreach (var documento in snapshot.Documents)
                    {
                        if (LeerParcial(documento, out var ficha, out var cambio) && cambio)
                        {
                            cambios.Add(ficha);
                        }
                    }

                    if (cambios.Count > 0)
                    {
                        repositorio.AplicarDesdeNube(cambios, []);
                    }
                }
                else
                {
                    var cambios = new List<FichaTecnica>();
                    var eliminadas = new List<Guid>();
                    foreach (var cambio in snapshot.Changes)
                    {
                        if (cambio.ChangeType == DocumentChange.Type.Removed)
                        {
                            if (Guid.TryParse(cambio.Document.Id, out var id))
                            {
                                eliminadas.Add(id);
                            }

                            continue;
                        }

                        if (LeerParcial(cambio.Document, out var ficha, out var cambioFicha) && cambioFicha)
                        {
                            cambios.Add(ficha);
                        }
                    }

                    if (cambios.Count > 0 || eliminadas.Count > 0)
                    {
                        repositorio.AplicarDesdeNube(cambios, eliminadas);
                    }
                }

                AvanzarCursor(false, snapshot.ReadTime);
            }
            catch (Exception ex)
            {
                CambiarEstado("Inventario en vivo, con un error al leer: " + ex.Message);
            }
        }

        private void AlRecibirBajas(QuerySnapshot snapshot)
        {
            var repositorio = _repositorio;
            if (Volatile.Read(ref _reemplazoEnCurso) > 0 || repositorio is null)
            {
                return;
            }

            try
            {
                var ids = (_bajasInicial ? snapshot.Changes.Select(cambio => cambio.Document) : snapshot.Documents)
                    .Select(doc => Guid.TryParse(doc.Id, out var id) ? id : Guid.Empty)
                    .Where(id => id != Guid.Empty)
                    .ToList();
                _bajasInicial = true;
                if (ids.Count > 0)
                {
                    repositorio.AplicarDesdeNube([], ids);
                }

                AvanzarCursor(true, snapshot.ReadTime);
            }
            catch (Exception ex)
            {
                CambiarEstado("Inventario en vivo, con un error al leer: " + ex.Message);
            }
        }

        private void AvanzarCursor(bool bajas, Timestamp marca)
        {
            lock (_cursorLock)
            {
                if (bajas)
                {
                    if (_bajasMarco && marca <= _cursorBajas)
                    {
                        return;
                    }

                    _cursorBajas = marca;
                    _bajasMarco = true;
                }
                else
                {
                    if (_datosMarco && marca <= _cursorDatos)
                    {
                        return;
                    }

                    _cursorDatos = marca;
                    _datosMarco = true;
                }

                if (!_datosMarco || !_bajasMarco)
                {
                    return;
                }

                var seguro = _cursorDatos <= _cursorBajas ? _cursorDatos : _cursorBajas;
                if (seguro <= _cursor)
                {
                    return;
                }

                _cursor = seguro;
                CursorLocal.Desde(seguro).Guardar();
            }
        }

        private static Dictionary<string, object> DatosInventario(FichaTecnica ficha)
        {
            var datos = InventarioDocumento.Crear(ficha);
            datos["Actualizado"] = FieldValue.ServerTimestamp;
            return datos;
        }

        private static Dictionary<string, object> MarcaBorrado() => new()
        {
            ["Actualizado"] = FieldValue.ServerTimestamp
        };

        private void CambiarEstado(string texto)
        {
            if (_estado == texto)
            {
                return;
            }

            _estado = texto;
            EstadoCambiado?.Invoke();
        }

        private sealed class CursorLocal
        {
            public bool Listo { get; init; }

            public Timestamp Marca { get; init; }

            public static CursorLocal Cargar()
            {
                var ruta = Ruta();
                if (!File.Exists(ruta))
                {
                    return new CursorLocal();
                }

                try
                {
                    var archivo = JsonSerializer.Deserialize<Archivo>(File.ReadAllText(ruta));
                    if (archivo is not { Listo: true })
                    {
                        return new CursorLocal();
                    }

                    return new CursorLocal
                    {
                        Listo = true,
                        Marca = Timestamp.FromDateTimeOffset(DateTimeOffset.Parse(archivo.Marca, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
                    };
                }
                catch
                {
                    return new CursorLocal();
                }
            }

            public static CursorLocal Desde(Timestamp marca) => new() { Listo = true, Marca = marca };

            public void Guardar()
            {
                var ruta = Ruta();
                Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
                File.WriteAllText(ruta, JsonSerializer.Serialize(new Archivo
                {
                    Listo = true,
                    Marca = Marca.ToDateTimeOffset().ToString("o", CultureInfo.InvariantCulture)
                }));
            }

            private static string Ruta() =>
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "IdermaCapilar",
                    "nube-cursor-inventario.json");

            private sealed class Archivo
            {
                public bool Listo { get; set; }

                public string Marca { get; set; } = string.Empty;
            }
        }
    }
}
