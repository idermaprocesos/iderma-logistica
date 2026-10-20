using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Cloud.Firestore;

namespace IdermaFichas.Services;

internal sealed record DocumentoNube(string Id, Dictionary<string, object> Datos);

internal sealed class SincronizadorNube
{
    internal const int DiasVentanaHistorial = 90;
    private const string Marca = "Actualizado";
    private const string CampoFicha = "Id ficha";
    private readonly string _coleccion;
    private readonly object _colaLock = new();
    private readonly object _cursorLock = new();
    private readonly Queue<Func<Task>> _cola = new();
    private readonly SemaphoreSlim _escrituras = new(1, 1);
    private FirestoreDb? _base;
    private FirestoreChangeListener? _escucha;
    private FirestoreChangeListener? _escuchaBajas;
    private int _procesandoCola;
    private int _reemplazoEnCurso;
    private int _historialListo;
    private bool _inicial;
    private bool _historialInicial;
    private bool _bajasInicial;
    private bool _datosMarco;
    private bool _bajasMarco;
    private bool _historial;
    private int _escuchaPedida;
    private int _coleccionLeida;
    private string[]? _campos;
    private readonly Dictionary<string, Task<bool>> _fichasEnCurso = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _fichasListas = new(StringComparer.OrdinalIgnoreCase);
    private Timestamp _cursor;
    private Timestamp _cursorDatos;
    private Timestamp _cursorBajas;
    private Func<IReadOnlyList<DocumentoNube>>? _copiarLocal;
    private Action<IReadOnlyList<DocumentoNube>, IReadOnlyList<string>, bool>? _aplicar;

    public SincronizadorNube(string coleccion) => _coleccion = coleccion;

    public void Preparar(
        FirestoreDb db,
        Func<IReadOnlyList<DocumentoNube>> copiarLocal,
        Action<IReadOnlyList<DocumentoNube>, IReadOnlyList<string>, bool> aplicar,
        bool historial = false,
        IReadOnlyList<string>? campos = null)
    {
        _base = db;
        _copiarLocal = copiarLocal;
        _aplicar = aplicar;
        _historial = historial;
        _campos = campos is { Count: > 0 } ? campos.ToArray() : null;
        _ = ProcesarColaAsync();
    }

    public void Escuchar()
    {
        if (_base is null || Interlocked.Exchange(ref _escuchaPedida, 1) == 1)
        {
            return;
        }

        _ = IniciarIncrementalAsync();
    }

    public Task<bool> CargarFichaAsync(Guid fichaId)
    {
        if (_base is null || !_historial || _aplicar is null || Volatile.Read(ref _coleccionLeida) == 1)
        {
            return Task.FromResult(false);
        }

        var clave = fichaId.ToString("D");

        lock (_colaLock)
        {
            if (_fichasListas.Contains(clave))
            {
                return Task.FromResult(false);
            }

            if (_fichasEnCurso.TryGetValue(clave, out var existente))
            {
                return existente;
            }

            var tarea = LeerFichaAsync(clave);
            _fichasEnCurso[clave] = tarea;
            return tarea;
        }
    }

    private async Task<bool> LeerFichaAsync(string clave)
    {
        try
        {
            if (_base is null || _aplicar is null)
            {
                return false;
            }

            var snap = await Consulta().WhereEqualTo(CampoFicha, clave).GetSnapshotAsync();
            if (snap.Count == 0)
            {
                return false;
            }

            var documentos = Leer(snap.Documents);
            if (documentos.Count > 0)
            {
                _aplicar(documentos, [], false);
                lock (_colaLock)
                {
                    _fichasListas.Add(clave);
                }
            }

            return documentos.Count > 0;
        }
        catch (Exception ex)
        {
            ModulosNube.Reportar("Módulos en vivo, con un error al leer: " + ex.Message);
            return false;
        }
        finally
        {
            lock (_colaLock)
            {
                _fichasEnCurso.Remove(clave);
            }
        }
    }

    public Task EmpujarLocalAsync()
    {
        if (_historial || _base is null || _copiarLocal is null || !IndiceHuellas.Cargar(_coleccion).Alineado)
        {
            return Task.CompletedTask;
        }

        var local = _copiarLocal().ToList();
        var listo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Encolar(async () =>
        {
            try
            {
                await ReemplazarAsync(_coleccion, local);
                listo.TrySetResult();
            }
            catch (Exception ex)
            {
                listo.TrySetException(ex);
            }
        });
        return listo.Task;
    }

    public void Publicar(IReadOnlyList<DocumentoNube> documentos, IReadOnlyCollection<string> eliminar, bool reemplazarColeccion) =>
        PublicarEn(_coleccion, documentos, eliminar, reemplazarColeccion);

    public void PublicarEn(string coleccion, IReadOnlyList<DocumentoNube> documentos, IReadOnlyCollection<string> eliminar, bool reemplazarColeccion)
    {
        var copia = documentos.ToList();
        var borrados = eliminar.ToList();
        Encolar(() => reemplazarColeccion
            ? ReemplazarAsync(coleccion, copia)
            : EscribirAsync(coleccion, copia, borrados));
    }

    public void PublicarAlcance(IReadOnlyList<DocumentoNube> documentos, IReadOnlyCollection<string> grupos)
    {
        var copia = documentos.ToList();
        var alcance = grupos.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Encolar(() => EscribirAlcanceAsync(copia, alcance));
    }

    public void Recordar(IReadOnlyList<DocumentoNube> documentos, IReadOnlyCollection<string> quitar)
    {
        var copia = documentos.ToList();
        var ids = quitar.ToList();
        Encolar(() =>
        {
            var indice = IndiceHuellas.Cargar(_coleccion);
            foreach (var id in ids)
            {
                indice.Quitar(id);
            }

            var grupos = copia
                .Select(GrupoDe)
                .Where(grupo => grupo.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var conservar = copia.Select(doc => doc.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var id in indice.IdsEnGrupos(grupos))
            {
                if (!conservar.Contains(id))
                {
                    indice.Quitar(id);
                }
            }

            foreach (var doc in copia)
            {
                indice.Poner(doc.Id, HuellaDatos(doc.Datos), GrupoDe(doc));
            }

            indice.Guardar(_coleccion);
            return Task.CompletedTask;
        });
    }

    private async Task IniciarIncrementalAsync()
    {
        if (_base is null || _copiarLocal is null || _aplicar is null)
        {
            return;
        }

        try
        {
            if (_historial)
            {
                var cursor = CursorHistorial.Cargar(_coleccion);
                if (!cursor.Listo)
                {
                    cursor = CursorHistorial.Desde(Timestamp.FromDateTime(DateTime.UtcNow));
                    cursor.Guardar(_coleccion);
                }

                _cursor = cursor.Marca;
            }
            else if (Volatile.Read(ref _historialListo) == 1)
            {
                if (_cursor == default)
                {
                    _cursor = CursorHistorial.Cargar(_coleccion).Marca;
                }
            }
            else
            {
                var cursor = CursorHistorial.Cargar(_coleccion);
                if (!cursor.Listo)
                {
                    var local = _copiarLocal();
                    if (local.Count > 0)
                    {
                        cursor = CursorHistorial.Desde(Timestamp.FromDateTime(DateTime.UtcNow));
                        cursor.Guardar(_coleccion);
                        _cursor = cursor.Marca;
                    }
                    else
                    {
                        var snap = await _base.Collection(_coleccion).GetSnapshotAsync();
                        if (snap.Count == 0)
                        {
                            var indiceLocal = IndiceHuellas.Cargar(_coleccion);
                            indiceLocal.Alineado = true;
                            indiceLocal.Guardar(_coleccion);
                        }
                        else
                        {
                            _aplicar(Leer(snap.Documents), [], true);
                            var indice = IndiceHuellas.Cargar(_coleccion);
                            indice.Reemplazar(snap.Documents.Select(doc =>
                                (doc.Id, HuellaDatos(doc.ToDictionary()), GrupoDe(doc.ToDictionary()))));
                            indice.Alineado = true;
                            indice.Guardar(_coleccion);
                        }

                        cursor = CursorHistorial.Desde(snap.ReadTime);
                        cursor.Guardar(_coleccion);
                        _cursor = snap.ReadTime;
                    }
                }
                else
                {
                    _cursor = cursor.Marca;
                }
            }

            _escucha = Consulta()
                .WhereGreaterThan(Marca, _cursor)
                .OrderBy(Marca)
                .Listen(AlRecibirHistorial);
            _escuchaBajas = _base.Collection(ColeccionBajas)
                .WhereGreaterThan(Marca, _cursor)
                .OrderBy(Marca)
                .Listen(AlRecibirBajas);
            Volatile.Write(ref _historialListo, 1);
            _ = ProcesarColaAsync();
        }
        catch (Exception ex)
        {
            ModulosNube.Reportar("Módulos en vivo, con un error al leer: " + ex.Message);
        }
    }

    private void AlRecibir(QuerySnapshot snapshot)
    {
        if (Volatile.Read(ref _reemplazoEnCurso) > 0 || _aplicar is null || _copiarLocal is null)
        {
            return;
        }

        try
        {
            if (!_inicial)
            {
                _inicial = true;
                if (snapshot.Count == 0)
                {
                    var local = _copiarLocal();
                    if (local.Count > 0)
                    {
                        Publicar(local, [], true);
                    }

                    return;
                }

                _aplicar(Leer(snapshot.Documents), [], true);
                return;
            }

            AplicarCambios(snapshot);
        }
        catch (Exception ex)
        {
            ModulosNube.Reportar("Módulos en vivo, con un error al leer: " + ex.Message);
        }
    }

    private void AlRecibirHistorial(QuerySnapshot snapshot)
    {
        if (Volatile.Read(ref _reemplazoEnCurso) > 0 || _aplicar is null)
        {
            return;
        }

        try
        {
            if (!_historialInicial)
            {
                _historialInicial = true;
                var documentos = Leer(snapshot.Documents);
                if (documentos.Count > 0)
                {
                    _aplicar(documentos, [], false);
                }
            }
            else
            {
                AplicarCambios(snapshot);
            }

            AvanzarCursor(false, snapshot.ReadTime);
        }
        catch (Exception ex)
        {
            ModulosNube.Reportar("Módulos en vivo, con un error al leer: " + ex.Message);
        }
    }

    private void AlRecibirBajas(QuerySnapshot snapshot)
    {
        if (Volatile.Read(ref _reemplazoEnCurso) > 0 || _aplicar is null)
        {
            return;
        }

        try
        {
            if (!_bajasInicial)
            {
                _bajasInicial = true;
                var eliminados = snapshot.Documents.Select(doc => doc.Id).ToList();
                if (eliminados.Count > 0)
                {
                    _aplicar([], eliminados, false);
                }
            }
            else
            {
                var eliminados = snapshot.Changes
                    .Where(cambio => cambio.ChangeType != DocumentChange.Type.Removed)
                    .Select(cambio => cambio.Document.Id)
                    .ToList();
                if (eliminados.Count > 0)
                {
                    _aplicar([], eliminados, false);
                }
            }

            AvanzarCursor(true, snapshot.ReadTime);
        }
        catch (Exception ex)
        {
            ModulosNube.Reportar("Módulos en vivo, con un error al leer: " + ex.Message);
        }
    }

    private void AplicarCambios(QuerySnapshot snapshot)
    {
        if (_aplicar is null)
        {
            return;
        }

        var cambios = new List<DocumentoNube>();
        var eliminados = new List<string>();
        foreach (var cambio in snapshot.Changes)
        {
            if (cambio.ChangeType == DocumentChange.Type.Removed)
            {
                eliminados.Add(cambio.Document.Id);
                continue;
            }

            if (cambio.Document.Exists)
            {
                cambios.Add(new DocumentoNube(cambio.Document.Id, cambio.Document.ToDictionary()));
            }
        }

        if (cambios.Count > 0 || eliminados.Count > 0)
        {
            _aplicar(cambios, eliminados, false);
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
            CursorHistorial.Desde(seguro).Guardar(_coleccion);
        }
    }

    private Query Consulta()
    {
        Query consulta = _base!.Collection(_coleccion);
        if (_campos is { Length: > 0 })
        {
            consulta = consulta.Select(_campos.Select(campo => new FieldPath(campo)).ToArray());
        }

        return consulta;
    }

    private static List<DocumentoNube> Leer(IEnumerable<DocumentSnapshot> documentos)
    {
        var lista = new List<DocumentoNube>();
        foreach (var documento in documentos)
        {
            if (documento.Exists)
            {
                lista.Add(new DocumentoNube(documento.Id, documento.ToDictionary()));
            }
        }

        return lista;
    }

    private async Task EscribirAsync(string nombre, IReadOnlyList<DocumentoNube> documentos, List<string> eliminados)
    {
        if (_base is null)
        {
            return;
        }

        await _escrituras.WaitAsync();
        try
        {
            var indice = IndiceHuellas.Cargar(nombre);
            var pendientes = documentos
                .Where(doc => !indice.Misma(doc.Id, HuellaDatos(doc.Datos)))
                .ToList();
            var coleccion = _base.Collection(nombre);
            await EnLotesAsync(pendientes, (lote, doc) => lote.Set(coleccion.Document(doc.Id), DatosParaEscribir(doc)));
            await EliminarAsync(nombre, eliminados);
            foreach (var id in eliminados)
            {
                indice.Quitar(id);
            }

            foreach (var doc in documentos)
            {
                indice.Poner(doc.Id, HuellaDatos(doc.Datos), GrupoDe(doc));
            }

            indice.Guardar(nombre);
        }
        finally
        {
            _escrituras.Release();
        }
    }

    private async Task EscribirAlcanceAsync(List<DocumentoNube> documentos, HashSet<string> grupos)
    {
        if (_base is null || grupos.Count == 0)
        {
            return;
        }

        await _escrituras.WaitAsync();
        try
        {
            var indice = IndiceHuellas.Cargar(_coleccion);
            var conservar = documentos.Select(doc => doc.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var borrar = indice.IdsEnGrupos(grupos).Where(id => !conservar.Contains(id)).ToList();
            var pendientes = documentos
                .Where(doc => !indice.Misma(doc.Id, HuellaDatos(doc.Datos)))
                .ToList();
            var coleccion = _base.Collection(_coleccion);
            await EnLotesAsync(pendientes, (lote, doc) => lote.Set(coleccion.Document(doc.Id), DatosParaEscribir(doc)));
            await EliminarAsync(_coleccion, borrar);
            foreach (var id in borrar)
            {
                indice.Quitar(id);
            }

            foreach (var doc in documentos)
            {
                indice.Poner(doc.Id, HuellaDatos(doc.Datos), GrupoDe(doc));
            }

            indice.Guardar(_coleccion);
        }
        finally
        {
            _escrituras.Release();
        }
    }

    private async Task ReemplazarAsync(string nombre, List<DocumentoNube> documentos)
    {
        if (_base is null)
        {
            return;
        }

        var escuchada = string.Equals(nombre, _coleccion, StringComparison.Ordinal);
        if (escuchada)
        {
            Interlocked.Increment(ref _reemplazoEnCurso);
        }

        await _escrituras.WaitAsync();
        try
        {
            var coleccion = _base.Collection(nombre);
            var indice = IndiceHuellas.Cargar(nombre);
            List<string> borrar;
            List<DocumentoNube> pendientes;
            if (!indice.Alineado)
            {
                var existentes = await coleccion.GetSnapshotAsync();
                var remoto = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var documento in existentes.Documents)
                {
                    if (documento.Exists)
                    {
                        remoto[documento.Id] = HuellaDatos(documento.ToDictionary());
                    }
                }

                var deseados = documentos.Select(doc => doc.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                borrar = remoto.Keys.Where(id => !deseados.Contains(id)).ToList();
                pendientes = documentos.Where(doc => !remoto.TryGetValue(doc.Id, out var huella) || huella != HuellaDatos(doc.Datos)).ToList();
            }
            else
            {
                var deseados = documentos.Select(doc => doc.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                borrar = indice.Ids.Where(id => !deseados.Contains(id)).ToList();
                pendientes = documentos.Where(doc => !indice.Misma(doc.Id, HuellaDatos(doc.Datos))).ToList();
            }

            await EliminarAsync(nombre, borrar);
            await EnLotesAsync(pendientes, (lote, doc) => lote.Set(coleccion.Document(doc.Id), DatosParaEscribir(doc)));
            indice.Reemplazar(documentos.Select(doc => (doc.Id, HuellaDatos(doc.Datos), GrupoDe(doc))));
            indice.Alineado = true;
            indice.Guardar(nombre);
        }
        finally
        {
            _escrituras.Release();
            if (escuchada)
            {
                Interlocked.Decrement(ref _reemplazoEnCurso);
            }
        }
    }

    private async Task EliminarAsync(string nombre, IReadOnlyList<string> ids)
    {
        if (_base is null || ids.Count == 0)
        {
            return;
        }

        var coleccion = _base.Collection(nombre);
        var bajas = _base.Collection(nombre + "-bajas");
        const int operaciones = 400;
        var porLote = bajas is null ? operaciones : operaciones / 2;
        for (var inicio = 0; inicio < ids.Count; inicio += porLote)
        {
            var lote = _base.StartBatch();
            var cuenta = 0;
            for (var i = inicio; i < ids.Count && i < inicio + porLote; i++)
            {
                lote.Delete(coleccion.Document(ids[i]));
                if (bajas is not null)
                {
                    lote.Set(bajas.Document(ids[i]), new Dictionary<string, object>
                    {
                        [Marca] = FieldValue.ServerTimestamp
                    });
                }

                cuenta++;
            }

            if (cuenta > 0)
            {
                await lote.CommitAsync();
            }
        }
    }

    private Dictionary<string, object> DatosParaEscribir(DocumentoNube doc)
    {
        var copia = CopiarFirestore(doc.Datos);
        copia[Marca] = FieldValue.ServerTimestamp;
        return copia;
    }

    private static Dictionary<string, object> CopiarFirestore(IDictionary<string, object> datos)
    {
        var copia = new Dictionary<string, object>(datos.Count);
        foreach (var par in datos)
        {
            copia[par.Key] = ValorFirestore(par.Value);
        }

        return copia;
    }

    private static object ValorFirestore(object valor) => valor switch
    {
        decimal numero => (double)numero,
        byte[] => valor,
        IDictionary<string, object> mapa => CopiarFirestore(mapa),
        IList lista => lista.Cast<object>().Select(ValorFirestore).ToList(),
        _ => valor
    };

    private string ColeccionBajas => _coleccion + "-bajas";

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

        if (_base is not null && (!_historial || Volatile.Read(ref _historialListo) == 1))
        {
            _ = ProcesarColaAsync();
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
                    ModulosNube.Reportar("Módulos en vivo, con un error al guardar: " + ex.Message);
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _procesandoCola, 0);
            var hayMas = false;
            lock (_colaLock)
            {
                hayMas = _cola.Count > 0;
            }

            if (hayMas && _base is not null)
            {
                _ = ProcesarColaAsync();
            }
        }
    }

    private static string GrupoDe(DocumentoNube doc) => GrupoDe(doc.Datos);

    private static string GrupoDe(IDictionary<string, object> datos)
    {
        if (!datos.TryGetValue("Id ficha", out var valor) || valor is null)
        {
            return string.Empty;
        }

        var texto = Convert.ToString(valor, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        return texto;
    }

    private static string HuellaDatos(IDictionary<string, object> datos)
    {
        var texto = new StringBuilder();
        EscribirMapa(texto, datos);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString())));
    }

    private static void EscribirMapa(StringBuilder texto, IDictionary<string, object> datos)
    {
        texto.Append('{');
        foreach (var par in datos.OrderBy(par => par.Key, StringComparer.Ordinal))
        {
            if (par.Key == Marca)
            {
                continue;
            }

            texto.Append(par.Key).Append('=');
            EscribirValor(texto, par.Value);
            texto.Append(';');
        }

        texto.Append('}');
    }

    private static void EscribirValor(StringBuilder texto, object? valor)
    {
        switch (valor)
        {
            case null:
                texto.Append('n');
                return;
            case string cadena:
                texto.Append('s').Append(cadena);
                return;
            case bool bandera:
                texto.Append(bandera ? '1' : '0');
                return;
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                texto.Append(CanonNumero(Convert.ToDouble(valor, CultureInfo.InvariantCulture)));
                return;
            case IDictionary<string, object> mapa:
                EscribirMapa(texto, mapa);
                return;
            case IDictionary mapa:
                texto.Append('{');
                foreach (var clave in mapa.Keys.Cast<object>().Select(item => Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty).OrderBy(item => item, StringComparer.Ordinal))
                {
                    if (clave == Marca)
                    {
                        continue;
                    }

                    texto.Append(clave).Append('=');
                    EscribirValor(texto, mapa[clave]);
                    texto.Append(';');
                }

                texto.Append('}');
                return;
            case IEnumerable lista:
                texto.Append('[');
                foreach (var item in lista)
                {
                    EscribirValor(texto, item);
                }

                texto.Append(']');
                return;
            default:
                texto.Append(Convert.ToString(valor, CultureInfo.InvariantCulture));
                return;
        }
    }

    private static string CanonNumero(double numero)
    {
        var redondo = Math.Round(numero);
        if (Math.Abs(numero - redondo) < 0.0000001)
        {
            return ((long)redondo).ToString(CultureInfo.InvariantCulture);
        }

        return numero.ToString("G17", CultureInfo.InvariantCulture);
    }

    private sealed class IndiceHuellas
    {
        public bool Alineado { get; set; }

        private readonly Dictionary<string, EntradaHuella> _docs = new(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<string> Ids => _docs.Keys;

        public static IndiceHuellas Cargar(string coleccion)
        {
            var indice = new IndiceHuellas();
            var ruta = Ruta(coleccion);
            if (!File.Exists(ruta))
            {
                return indice;
            }

            try
            {
                var archivo = JsonSerializer.Deserialize<ArchivoHuellas>(File.ReadAllText(ruta));
                if (archivo is null)
                {
                    return indice;
                }

                indice.Alineado = archivo.Alineado;
                if (archivo.Docs is null)
                {
                    return indice;
                }

                foreach (var par in archivo.Docs)
                {
                    indice._docs[par.Key] = par.Value ?? new EntradaHuella();
                }
            }
            catch
            {
                return new IndiceHuellas();
            }

            return indice;
        }

        public void Guardar(string coleccion)
        {
            var ruta = Ruta(coleccion);
            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            var archivo = new ArchivoHuellas
            {
                Alineado = Alineado,
                Docs = _docs
            };
            File.WriteAllText(ruta, JsonSerializer.Serialize(archivo));
        }

        public bool Misma(string id, string huella) =>
            _docs.TryGetValue(id, out var entrada) && entrada.H == huella;

        public void Poner(string id, string huella, string grupo) =>
            _docs[id] = new EntradaHuella { H = huella, G = grupo };

        public void Quitar(string id) => _docs.Remove(id);

        public List<string> IdsEnGrupos(IReadOnlySet<string> grupos)
        {
            if (grupos.Count == 0)
            {
                return [];
            }

            return _docs
                .Where(par => grupos.Contains(par.Value.G))
                .Select(par => par.Key)
                .ToList();
        }

        public void Reemplazar(IEnumerable<(string Id, string Huella, string Grupo)> documentos)
        {
            _docs.Clear();
            foreach (var doc in documentos)
            {
                _docs[doc.Id] = new EntradaHuella { H = doc.Huella, G = doc.Grupo };
            }
        }

        private static string Ruta(string coleccion) =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IdermaCapilar",
                "nube-huellas-" + coleccion + ".json");

        private sealed class ArchivoHuellas
        {
            public bool Alineado { get; set; }

            public Dictionary<string, EntradaHuella>? Docs { get; set; }
        }

        private sealed class EntradaHuella
        {
            public string H { get; set; } = string.Empty;

            public string G { get; set; } = string.Empty;
        }
    }

    private static class FichasLeidas
    {
        private static readonly object Candado = new();

        public static bool Contiene(string coleccion, string id)
        {
            lock (Candado)
            {
                return Cargar(coleccion).Contains(id);
            }
        }

        public static void Marcar(string coleccion, string id)
        {
            lock (Candado)
            {
                var ids = Cargar(coleccion);
                if (!ids.Add(id))
                {
                    return;
                }

                var ruta = Ruta(coleccion);
                Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
                File.WriteAllText(ruta, JsonSerializer.Serialize(ids.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)));
            }
        }

        private static HashSet<string> Cargar(string coleccion)
        {
            var ruta = Ruta(coleccion);
            if (!File.Exists(ruta))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var ids = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(ruta));
                return new HashSet<string>(ids ?? [], StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static string Ruta(string coleccion) =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IdermaCapilar",
                "nube-fichas-" + coleccion + ".json");
    }

    private sealed class CursorHistorial
    {
        public bool Listo { get; set; }

        public Timestamp Marca { get; set; }

        public static CursorHistorial Cargar(string coleccion)
        {
            var ruta = Ruta(coleccion);
            if (!File.Exists(ruta))
            {
                return new CursorHistorial();
            }

            try
            {
                var archivo = JsonSerializer.Deserialize<ArchivoCursor>(File.ReadAllText(ruta));
                if (archivo is not { Listo: true })
                {
                    return new CursorHistorial();
                }

                return new CursorHistorial
                {
                    Listo = true,
                    Marca = Timestamp.FromDateTimeOffset(DateTimeOffset.Parse(archivo.Marca, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
                };
            }
            catch
            {
                return new CursorHistorial();
            }
        }

        public static CursorHistorial Desde(Timestamp marca) =>
            new() { Listo = true, Marca = marca };

        public void Guardar(string coleccion)
        {
            var ruta = Ruta(coleccion);
            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            var archivo = new ArchivoCursor
            {
                Listo = true,
                Marca = Marca.ToDateTimeOffset().ToString("o", CultureInfo.InvariantCulture)
            };
            File.WriteAllText(ruta, JsonSerializer.Serialize(archivo));
        }

        private static string Ruta(string coleccion) =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IdermaCapilar",
                "nube-cursor-" + coleccion + ".json");

        private sealed class ArchivoCursor
        {
            public bool Listo { get; set; }

            public string Marca { get; set; } = string.Empty;
        }
    }
}
