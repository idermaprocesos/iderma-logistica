using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public sealed class FichaRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(), new AreaIdJsonConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly List<FichaTecnica> _fichas = [];
    private readonly Dictionary<Guid, FichaTecnica> _porId = [];
    private readonly Dictionary<string, int> _indiceCodigo = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, int> _conteoArea = [];
    private readonly Dictionary<Guid, int> _maximoSecuencia = [];
    private int _conteoSinArea;
    private readonly Dictionary<Guid, double> _existenciaRegistrada = [];
    private readonly IndiceBusquedaFichas _busqueda = new();

    public ObservableCollection<FichaTecnica> Fichas { get; } = [];

    public string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "fichas-tecnicas.json");

    public string CarpetaCopias { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "copias");

    public IReadOnlyList<FichaTecnica> Todas => _fichas;

    public object SyncRoot { get; } = new();

    private bool _desdeNube;

    public IInventarioNube? Nube { get; set; }

    public event EventHandler? InventarioExternoActualizado;

    public void NotificarVistas() => InventarioExternoActualizado?.Invoke(this, EventArgs.Empty);

    public void Cargar()
    {
        lock (SyncRoot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);

            if (!File.Exists(RutaArchivo))
            {
                RestaurarEjemplos();
                return;
            }

            List<FichaTecnica>? cargadas;
            try
            {
                var json = File.ReadAllText(RutaArchivo);
                cargadas = JsonSerializer.Deserialize<List<FichaTecnica>>(json, JsonOptions);
            }
            catch (Exception)
            {
                ArchivoAtomico.AislarDañado(RutaArchivo);
                _fichas.Clear();
                Guardar();
                ReconstruirIndices();
                SincronizarColeccion();
                return;
            }

            _fichas.Clear();
            if (cargadas is not null)
            {
                _fichas.AddRange(cargadas);
            }

            AlmacenCaracteristicas.Aplicar(_fichas, AlmacenCaracteristicas.Cargar());
            var identificacionActualizada = ReconstruirIndices();
            SincronizarColeccion();
            if (identificacionActualizada)
            {
                ArchivoAtomico.EscribirTexto(RutaArchivo, JsonSerializer.Serialize(_fichas, JsonOptions));
            }
        }
    }

    public void Guardar()
    {
        lock (SyncRoot)
        {
            ArchivoAtomico.EscribirTexto(RutaArchivo, JsonSerializer.Serialize(_fichas, JsonOptions));
            AlmacenCaracteristicas.Guardar(_fichas);
        }
    }

    public FichaTecnica? Obtener(Guid id) =>
        _porId.TryGetValue(id, out var ficha) ? ficha : null;

    public IEnumerable<FichaTecnica> PorArea(Guid? area) =>
        area is Guid id
            ? _fichas.Where(f => f.Area == id)
            : _fichas.Where(f => f.Area is null);

    public IReadOnlyList<FichaTecnica> Buscar(string filtro, Guid? area = null, bool soloSinArea = false)
    {
        IEnumerable<FichaTecnica> origen = soloSinArea
            ? _fichas.Where(f => f.Area is null)
            : area is Guid id
                ? _fichas.Where(f => f.Area == id)
                : _fichas;
        return _busqueda.Filtrar(origen, filtro).ToList();
    }

    public IReadOnlyList<FichaTecnica> BuscarLimitado(string filtro, int maximo)
    {
        var ids = _busqueda.FiltrarIds(filtro, maximo);
        if (ids.Count == 0)
        {
            return [];
        }

        var lista = new List<FichaTecnica>(ids.Count);
        foreach (var id in ids)
        {
            if (_porId.TryGetValue(id, out var ficha))
            {
                lista.Add(ficha);
            }
        }

        return lista;
    }

    public IReadOnlyList<ContactoProveedor> HistorialProveedores()
    {
        lock (SyncRoot)
        {
            return ContactoProveedor.Combinar(
                AlmacenHistorialProveedores.Guardados,
                _fichas,
                AlmacenHistorialProveedores.Ocultos);
        }
    }

    public IEnumerable<FichaTecnica> Vencidas() =>
        _fichas.Where(f => f.EstaVencido)
            .OrderBy(f => f.FechaCaducidad);

    public IEnumerable<AlertaCaducidad> PartidasVencidas() =>
        PartidasInventario.Vencidas(_fichas)
            .OrderBy(a => a.Partida.FechaCaducidad);

    public IEnumerable<AlertaCaducidad> PartidasProximas(int dias = 30) =>
        PartidasInventario.Proximas(_fichas, dias)
            .OrderBy(a => a.Partida.FechaCaducidad);

    public IEnumerable<AlertaCaducidad> PartidasProximasHasta(DateTimeOffset limite) =>
        PartidasInventario.ProximasHasta(_fichas, limite)
            .OrderBy(a => a.Partida.FechaCaducidad);

    public IEnumerable<FichaTecnica> ProximasAVencer(int dias = 30)
    {
        var limite = DateTimeOffset.Now.Date.AddDays(dias);
        return _fichas
            .Where(f => f.Partidas.Any(p =>
                            p.Cantidad > PartidasInventario.Epsilon
                            && p.FechaCaducidad.HasValue
                            && !p.EstaVencido
                            && p.FechaCaducidad.Value.Date <= limite)
                        || (f.Partidas.Count == 0
                            && f.FechaCaducidad.HasValue
                            && !f.EstaVencido
                            && f.FechaCaducidad.Value.Date <= limite))
            .OrderBy(f => f.FechaCaducidad);
    }

    public int Contar(Guid? area) =>
        area is Guid id
            ? _conteoArea.TryGetValue(id, out var n) ? n : 0
            : _conteoSinArea;

    public string SiguienteCodigo(Guid? area)
    {
        var prefijo = Catalogos.PrefijoCodigo(area);
        var ocupados = new HashSet<int>();
        var codigos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ficha in PorArea(area))
        {
            var codigo = ficha.Codigo?.Trim() ?? string.Empty;
            if (codigo.Length == 0)
            {
                continue;
            }

            codigos.Add(codigo);
            var secuencia = ExtraerSecuencia(codigo, prefijo);
            if (secuencia > 0)
            {
                ocupados.Add(secuencia);
            }
        }

        var n = 1;
        while (ocupados.Contains(n) ||
               codigos.Contains(FormatoCodigo(prefijo, n)) ||
               codigos.Contains($"{prefijo}-{n:000}"))
        {
            n++;
        }

        return FormatoCodigo(prefijo, n);
    }

    public string PrepararMovimientoDeArea(FichaTecnica ficha, Guid? destino)
    {
        if (Equals(ficha.Area, destino))
        {
            return ficha.Codigo;
        }

        var codigo = SiguienteCodigo(destino);
        ficha.Codigo = codigo;
        ficha.Area = destino;
        return codigo;
    }

    public int ReasignarArea(Guid origen, Guid? destino)
    {
        var n = 0;
        foreach (var ficha in _fichas.Where(f => f.Area == origen).ToList())
        {
            PrepararMovimientoDeArea(ficha, destino);
            ficha.FechaActualizacion = DateTimeOffset.Now;
            n++;
        }

        if (n > 0)
        {
            Guardar();
            ReconstruirIndices();
        }

        return n;
    }

    public void ActualizarIndiceBusqueda() => _busqueda.Reconstruir(_fichas);

    public bool CodigoEnUso(Guid? area, string codigo, Guid? excluirId = null)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return false;
        }

        var clave = ClaveCodigo(area, codigo);
        if (!_indiceCodigo.TryGetValue(clave, out var indice))
        {
            return false;
        }

        return excluirId is not Guid id || _fichas[indice].Id != id;
    }

    public void GuardarFicha(
        FichaTecnica ficha,
        string? origenIngreso = null,
        string? notaIngreso = null,
        bool registrarIngresoAutomatico = true)
    {
        GuardarFichas([ficha], origenIngreso, notaIngreso, registrarIngresoAutomatico);
    }

    public void GuardarFichas(
        IEnumerable<FichaTecnica> fichas,
        string? origenIngreso = null,
        string? notaIngreso = null,
        bool registrarIngresoAutomatico = true)
    {
        var lote = fichas.ToList();
        List<MovimientoInventario> movimientos;
        lock (SyncRoot)
        {
            movimientos = [];
            foreach (var ficha in lote)
            {
                PartidasInventario.PrepararParaGuardar(ficha);
                var movimiento = AplicarEnMemoria(ficha);
                if (movimiento is not null && registrarIngresoAutomatico)
                {
                    if (!string.IsNullOrWhiteSpace(origenIngreso))
                    {
                        movimiento.Origen = origenIngreso;
                    }

                    if (!string.IsNullOrWhiteSpace(notaIngreso))
                    {
                        movimiento.Nota = notaIngreso;
                    }

                    movimientos.Add(movimiento);
                }
            }

            Guardar();
            ReconstruirIndices();
            SincronizarColeccion();
        }

        if (movimientos.Count > 0)
        {
            App.Instance.Registro.Agregar(movimientos);
        }

        PublicarNube(lote.Select(f => f.Clonar()).ToList(), [], false);
    }

    public void Eliminar(Guid id) => EliminarVarios([id]);

    public void EliminarVarios(IEnumerable<Guid> ids)
    {
        var conjunto = ids.ToHashSet();
        if (conjunto.Count == 0)
        {
            return;
        }

        lock (SyncRoot)
        {
            _fichas.RemoveAll(f => conjunto.Contains(f.Id));
            foreach (var id in conjunto)
            {
                _existenciaRegistrada.Remove(id);
            }

            Guardar();
            ReconstruirIndices();
            SincronizarColeccion();
        }

        App.Instance.Registro.EliminarDeFichas(conjunto);
        PublicarNube([], conjunto, false);
    }

    public void RestaurarEjemplos()
    {
        lock (SyncRoot)
        {
            _fichas.Clear();
            _fichas.AddRange(DatosIniciales.Crear());
            Guardar();
            ReconstruirIndices();
            SincronizarColeccion();
        }

        App.Instance.Registro.ReconstruirDesdeExistencias();
        PublicarNube(CopiarTodas(), [], true);
    }

    public void ReemplazarTodas(IEnumerable<FichaTecnica> fichas)
    {
        lock (SyncRoot)
        {
            _fichas.Clear();
            _fichas.AddRange(fichas);
            Guardar();
            ReconstruirIndices();
            SincronizarColeccion();
        }

        PublicarNube(CopiarTodas(), [], true);
    }

    public string ExportarJson() => JsonSerializer.Serialize(_fichas, JsonOptions);

    public int ImportarJson(string json)
    {
        var importadas = JsonSerializer.Deserialize<List<FichaTecnica>>(json, JsonOptions) ?? [];
        Dictionary<Guid, double> anteriores;
        lock (SyncRoot)
        {
            anteriores = new Dictionary<Guid, double>(_existenciaRegistrada);
            foreach (var ficha in importadas)
            {
                if (ficha.Id == Guid.Empty)
                {
                    ficha.Id = Guid.NewGuid();
                }

                ficha.Caracteristicas = AlmacenCaracteristicas.NormalizarLista(ficha.Caracteristicas);
                PartidasInventario.Normalizar(ficha);
                if (_porId.TryGetValue(ficha.Id, out var anterior))
                {
                    ConservarCaracteristicas(ficha, anterior);
                    var indice = _fichas.IndexOf(anterior);
                    if (indice >= 0)
                    {
                        _fichas[indice] = ficha;
                    }
                }
                else
                {
                    _fichas.Add(ficha);
                }
            }

            Guardar();
            ReconstruirIndices();
            SincronizarColeccion();
        }

        RegistrarDeltasExistencia(anteriores);
        PublicarNube(importadas.Select(f => f.Clonar()).ToList(), [], false);
        return importadas.Count;
    }

    public ResultadoImportacion ImportarFichas(IEnumerable<FichaTecnica> fichas, Guid? areaForzada = null)
    {
        var nuevas = 0;
        var actualizadas = 0;
        var omitidas = 0;
        var publicadas = new List<FichaTecnica>();
        Dictionary<Guid, double> anteriores;
        lock (SyncRoot)
        {
            ReconstruirIndices();
            anteriores = new Dictionary<Guid, double>(_existenciaRegistrada);

            foreach (var ficha in fichas)
        {
            if (areaForzada is Guid area)
            {
                ficha.Area = area;
            }

            if (string.IsNullOrWhiteSpace(ficha.Nombre))
            {
                omitidas++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(ficha.Codigo))
            {
                ficha.Codigo = SiguienteCodigo(ficha.Area);
            }
            else
            {
                ficha.Codigo = TextoIdentificacion.Codigo(ficha.Codigo);
            }

            ficha.Nombre = TextoIdentificacion.Nombre(ficha.Nombre);
            ficha.FechaActualizacion = DateTimeOffset.Now;
            ficha.Caracteristicas = AlmacenCaracteristicas.NormalizarLista(ficha.Caracteristicas);

            var clave = ClaveCodigo(ficha.Area, ficha.Codigo);
            FichaTecnica? existente = null;
            if (ficha.Id != Guid.Empty)
            {
                _porId.TryGetValue(ficha.Id, out existente);
            }

            if (existente is null && _indiceCodigo.TryGetValue(clave, out var indiceCodigo))
            {
                existente = _fichas[indiceCodigo];
            }

            if (existente is not null)
            {
                var indice = _fichas.IndexOf(existente);
                ficha.Id = existente.Id;
                ficha.FechaCreacion = existente.FechaCreacion;
                if (ficha.Caracteristicas.Count == 0)
                {
                    ficha.Caracteristicas = existente.Caracteristicas;
                }

                var partidasPrevias = existente.Partidas.Select(p => p.Clonar()).ToList();
                PartidasInventario.Normalizar(ficha);
                PartidasInventario.ConservarLotesAusentes(ficha, partidasPrevias);
                _fichas[indice] = ficha;
                _porId[ficha.Id] = ficha;
                _indiceCodigo[clave] = indice;
                actualizadas++;
                publicadas.Add(ficha);
            }
            else
            {
                if (ficha.Id == Guid.Empty)
                {
                    ficha.Id = Guid.NewGuid();
                }

                PartidasInventario.Normalizar(ficha);
                ficha.FechaCreacion = DateTimeOffset.Now;
                _fichas.Add(ficha);
                _porId[ficha.Id] = ficha;
                _indiceCodigo[clave] = _fichas.Count - 1;
                if (ficha.Area is Guid areaId &&
                    (!_maximoSecuencia.TryGetValue(areaId, out var max) || ExtraerSecuencia(ficha) > max))
                {
                    _maximoSecuencia[areaId] = ExtraerSecuencia(ficha);
                }

                nuevas++;
                publicadas.Add(ficha);
            }
        }

        if (nuevas + actualizadas > 0)
            {
                Guardar();
                ReconstruirIndices();
                SincronizarColeccion();
            }
        }

        if (nuevas + actualizadas > 0)
        {
            RegistrarDeltasExistencia(anteriores);
            PublicarNube(publicadas.Select(f => f.Clonar()).ToList(), [], false);
        }

        return new ResultadoImportacion(nuevas, actualizadas, omitidas);
    }

    public string VaciarConCopiaSeguridad()
    {
        string carpeta;
        lock (SyncRoot)
        {
            var copia = App.Instance.Copias.Crear("antes-de-borrar");
            _fichas.Clear();
            Guardar();
            ReconstruirIndices();
            SincronizarColeccion();
            App.Instance.Registro.Vaciar();
            carpeta = copia.Carpeta;
        }

        PublicarNube([], [], true);
        return carpeta;
    }

    private void RegistrarDeltasExistencia(IReadOnlyDictionary<Guid, double> anteriores)
    {
        var lote = new List<MovimientoInventario>();
        foreach (var ficha in _fichas)
        {
            anteriores.TryGetValue(ficha.Id, out var anterior);
            var movimiento = CrearMovimientoPorDelta(ficha, anterior);
            if (movimiento is not null)
            {
                lote.Add(movimiento);
            }
        }

        if (lote.Count > 0)
        {
            App.Instance.Registro.Agregar(lote);
        }
    }

    private static void ConservarCaracteristicas(FichaTecnica destino, FichaTecnica origen)
    {
        if (destino.Caracteristicas.Count == 0 && origen.Caracteristicas.Count > 0)
        {
            destino.Caracteristicas = origen.Caracteristicas.Select(c => c.Clonar()).ToList();
        }
    }

    private MovimientoInventario? AplicarEnMemoria(FichaTecnica ficha)
    {
        ficha.FechaActualizacion = DateTimeOffset.Now;
        ficha.Caracteristicas = AlmacenCaracteristicas.NormalizarLista(ficha.Caracteristicas);
        PartidasInventario.PrepararParaGuardar(ficha);

        var existia = _existenciaRegistrada.TryGetValue(ficha.Id, out var existenciaAnterior);
        if (!existia && _porId.TryGetValue(ficha.Id, out var previa))
        {
            existenciaAnterior = previa.Existencia;
            existia = true;
        }

        if (_porId.TryGetValue(ficha.Id, out var anterior))
        {
            ConservarCaracteristicas(ficha, anterior);
            var indice = _fichas.IndexOf(anterior);
            if (indice >= 0)
            {
                _fichas[indice] = ficha;
                _porId[ficha.Id] = ficha;
                _existenciaRegistrada[ficha.Id] = ficha.Existencia;
                return CrearMovimientoPorDelta(ficha, existia ? existenciaAnterior : 0);
            }
        }

        if (ficha.FechaCreacion == default)
        {
            ficha.FechaCreacion = DateTimeOffset.Now;
        }

        _fichas.Add(ficha);
        _porId[ficha.Id] = ficha;
        _existenciaRegistrada[ficha.Id] = ficha.Existencia;
        return CrearMovimientoPorDelta(ficha, 0);
    }

    private static MovimientoInventario? CrearMovimientoPorDelta(FichaTecnica ficha, double existenciaAnterior)
    {
        var delta = ficha.Existencia - existenciaAnterior;
        if (Math.Abs(delta) <= 0.0001)
        {
            return null;
        }

        var salida = delta < 0;
        return new MovimientoInventario
        {
            FichaId = ficha.Id,
            Area = ficha.Area,
            Fecha = DateTimeOffset.Now,
            Cantidad = Math.Abs(delta),
            ExistenciaResultante = ficha.Existencia,
            Tipo = salida ? "Salida" : "Ingreso",
            Origen = salida
                ? "Actualización de ficha"
                : (existenciaAnterior <= 0.0001 ? "Alta de producto" : "Actualización de ficha"),
            Nota = salida
                ? "Salida registrada al actualizar la existencia de la ficha."
                : "Ingreso registrado al actualizar la existencia de la ficha."
        };
    }

    private bool ReconstruirIndices()
    {
        _porId.Clear();
        _indiceCodigo.Clear();
        _conteoArea.Clear();
        _maximoSecuencia.Clear();
        _existenciaRegistrada.Clear();
        _conteoSinArea = 0;
        var identificacionActualizada = false;

        for (var i = 0; i < _fichas.Count; i++)
        {
            var ficha = _fichas[i];
            ficha.Caracteristicas = AlmacenCaracteristicas.NormalizarLista(ficha.Caracteristicas);
            PartidasInventario.Normalizar(ficha);
            var codigo = TextoIdentificacion.Codigo(ficha.Codigo);
            var nombre = TextoIdentificacion.Nombre(ficha.Nombre);
            if (!string.Equals(ficha.Codigo, codigo, StringComparison.Ordinal)
                || !string.Equals(ficha.Nombre, nombre, StringComparison.Ordinal))
            {
                identificacionActualizada = true;
            }

            ficha.Codigo = codigo;
            ficha.Nombre = nombre;
            _porId[ficha.Id] = ficha;
            _existenciaRegistrada[ficha.Id] = ficha.Existencia;
            _indiceCodigo[ClaveCodigo(ficha.Area, ficha.Codigo)] = i;
            if (ficha.Area is Guid areaId)
            {
                _conteoArea[areaId] = _conteoArea.GetValueOrDefault(areaId) + 1;
                var secuencia = ExtraerSecuencia(ficha);
                if (!_maximoSecuencia.TryGetValue(areaId, out var max) || secuencia > max)
                {
                    _maximoSecuencia[areaId] = secuencia;
                }
            }
            else
            {
                _conteoSinArea++;
            }
        }

        _busqueda.Reconstruir(_fichas);
        return identificacionActualizada;
    }

    private static readonly char[] SeparadoresCodigo = ['_', '-'];

    private static string ClaveCodigo(Guid? area, string codigo) =>
        $"{area?.ToString("N") ?? "none"}|{codigo.Trim()}";

    private static string FormatoCodigo(string prefijo, int numero) =>
        $"{prefijo}_{numero:000}";

    private static int ExtraerSecuencia(FichaTecnica ficha) =>
        ExtraerSecuencia(ficha.Codigo, Catalogos.PrefijoCodigo(ficha.Area));

    private static int ExtraerSecuencia(string codigo, string prefijo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return 0;
        }

        codigo = codigo.Trim();
        foreach (var separador in SeparadoresCodigo)
        {
            var esperado = prefijo + separador;
            if (!codigo.StartsWith(esperado, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var resto = codigo[esperado.Length..];
            var digitos = 0;
            while (digitos < resto.Length && char.IsDigit(resto[digitos]))
            {
                digitos++;
            }

            if (digitos > 0 && int.TryParse(resto[..digitos], out var n))
            {
                return n;
            }
        }

        var partes = codigo.Split(SeparadoresCodigo, StringSplitOptions.RemoveEmptyEntries);
        for (var i = partes.Length - 1; i >= 0; i--)
        {
            if (int.TryParse(partes[i], out var n))
            {
                return n;
            }
        }

        return 0;
    }

    public List<FichaTecnica> CopiarTodas()
    {
        lock (SyncRoot)
        {
            return _fichas.Select(f => f.Clonar()).ToList();
        }
    }

    public void AplicarDesdeNube(IReadOnlyList<FichaTecnica> cambios, IReadOnlyCollection<Guid> eliminados)
    {
        var huboCambio = false;
        lock (SyncRoot)
        {
            _desdeNube = true;
            try
            {
                var idsBorrados = eliminados.ToHashSet();
                if (_fichas.RemoveAll(f => idsBorrados.Contains(f.Id)) > 0)
                {
                    huboCambio = true;
                    foreach (var id in idsBorrados)
                    {
                        _porId.Remove(id);
                    }
                }

                foreach (var cambio in cambios)
                {
                    PartidasInventario.Normalizar(cambio);
                    if (_porId.TryGetValue(cambio.Id, out var anterior))
                    {
                        if (InventarioDocumento.Huella(anterior) == InventarioDocumento.Huella(cambio))
                        {
                            continue;
                        }

                        var indice = _fichas.IndexOf(anterior);
                        if (indice >= 0)
                        {
                            _fichas[indice] = cambio;
                            _porId[cambio.Id] = cambio;
                        }
                    }
                    else
                    {
                        _fichas.Add(cambio);
                        _porId[cambio.Id] = cambio;
                    }

                    huboCambio = true;
                }

                if (huboCambio)
                {
                    Guardar();
                    ReconstruirIndices();
                    SincronizarColeccion();
                }
            }
            finally
            {
                _desdeNube = false;
            }
        }

        if (huboCambio)
        {
            InventarioExternoActualizado?.Invoke(this, EventArgs.Empty);
            if (!ModulosNube.ReflejoSuspendido)
            {
                ModulosNube.PublicarDesdeFichas(cambios, eliminados);
            }
        }
    }

    internal void PublicarEnInventario(IReadOnlyList<FichaTecnica> fichas)
    {
        if (Nube is null || fichas.Count == 0)
        {
            return;
        }

        Nube.Publicar(fichas, [], false);
    }

    private void PublicarNube(IReadOnlyList<FichaTecnica> fichas, IReadOnlyCollection<Guid> eliminadas, bool reemplazarColeccion)
    {
        if (_desdeNube || Nube is null)
        {
            return;
        }

        if (!reemplazarColeccion && fichas.Count == 0 && eliminadas.Count == 0)
        {
            return;
        }

        Nube.Publicar(fichas, eliminadas, reemplazarColeccion);
        if (reemplazarColeccion)
        {
            ModulosNube.ReflejarInventario();
        }
        else
        {
            ModulosNube.PublicarDesdeFichas(fichas, eliminadas);
        }
    }

    private void SincronizarColeccion()
    {
        // La lista observable no se usa en la UI; se omite reconstruir 5000+ ítems en cada guardado.
    }
}
