using Google.Cloud.Firestore;
using Iderma.Core.Services;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class ModulosNube
{
    private static readonly SincronizadorNube Vencimientos = new(BasesFirebase.Vencimientos);
    private static readonly SincronizadorNube Registro = new(BasesFirebase.Registro);
    private static readonly SincronizadorNube Precios = new(BasesFirebase.Precios);
    private static readonly SincronizadorNube Ordenes = new(BasesFirebase.Ordenes);
    private static readonly SincronizadorNube Proveedores = new(BasesFirebase.Proveedores);
    private static readonly SincronizadorNube Merma = new(BasesFirebase.Merma);
    private static readonly SincronizadorNube Mantenimiento = new(BasesFirebase.Mantenimiento);
    private static readonly object _apertura = new();
    private static readonly Dictionary<string, Task<bool>> _conexiones = new(StringComparer.Ordinal);
    private static readonly HashSet<string> _escuchar = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, bool> _independiente = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> _estadosModulo = new(StringComparer.Ordinal);
    private static CancellationTokenSource? _reloj;
    private static int _relojVersion;
    private static int _sinReflejo;
    private static string _estado = string.Empty;
    private static string? _aviso;

    public static string Estado => _estado;

    public static event Action? EstadoCambiado;

    public static event Action? DatosCambiados;

    public static void AbrirMenu(string etiqueta)
    {
        var id = etiqueta switch
        {
            "caducidad" => BasesFirebase.Vencimientos,
            "registro" => BasesFirebase.Registro,
            "historial-precios" => BasesFirebase.Precios,
            "orden-compra" => BasesFirebase.Ordenes,
            "proveedores" => BasesFirebase.Proveedores,
            "merma" => BasesFirebase.Merma,
            "mantenimiento" => BasesFirebase.Mantenimiento,
            _ => null
        };

        if (id is not null)
        {
            Abrir(id);
        }
    }

    private static void Abrir(string id)
    {
        lock (_apertura)
        {
            _escuchar.Add(id);
        }

        _ = AbrirAsync(id);
    }

    public static void ReemplazarTodo() => ReflejarInventario();

    public static void PublicarDesdeFichas(IReadOnlyList<FichaTecnica> fichas, IReadOnlyCollection<Guid> eliminadas)
    {
        var grupos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ficha in fichas)
        {
            grupos.Add(ficha.Id.ToString("D"));
        }

        foreach (var id in eliminadas)
        {
            grupos.Add(id.ToString("D"));
        }

        if (grupos.Count > 0)
        {
            Vencimientos.PublicarAlcance(CopiarVencimientos(fichas), grupos);
        }

        var catalogo = Fichas();
        App.Instance.Proveedores.SincronizarDesdeFichas(catalogo, AlmacenHistorialProveedores.Guardados);
        Proveedores.Publicar(CopiarProveedores(catalogo), [], true);
    }

    public static void ReflejarInventario()
    {
        var version = Interlocked.Increment(ref _relojVersion);
        _reloj?.Cancel();
        var reloj = new CancellationTokenSource();
        _reloj = reloj;
        _ = ReflejarAsync(version, reloj.Token);
    }

    public static void PublicarRegistro(IReadOnlyList<MovimientoInventario> movimientos, IReadOnlyCollection<Guid> eliminados, bool reemplazarColeccion = false)
    {
        var fichas = Fichas();
        var documentos = movimientos
            .Select(m => new DocumentoNube(m.Id.ToString("D"), ModulosDocumento.CrearMovimiento(m, Buscar(fichas, m.FichaId))))
            .ToList();
        Registro.Publicar(documentos, eliminados.Select(id => id.ToString("D")).ToList(), reemplazarColeccion);
    }

    public static void PublicarPrecio(PrecioProducto precio)
    {
        var ficha = App.Instance.Repositorio.Obtener(precio.FichaId);
        Precios.Publicar([new DocumentoNube(precio.Id.ToString("D"), ModulosDocumento.CrearPrecio(precio, ficha))], [], false);
    }

    public static void EliminarPrecio(Guid id)
    {
        Precios.Publicar([], [id.ToString("D")], false);
    }

    public static Task<bool> CompletarRegistroAsync(Guid fichaId) =>
        CompletarHistorialAsync(BasesFirebase.Registro, fichaId);

    public static Task<bool> CompletarPreciosAsync(Guid fichaId) =>
        CompletarHistorialAsync(BasesFirebase.Precios, fichaId);

    private static async Task<bool> CompletarHistorialAsync(string id, Guid fichaId)
    {
        Abrir(id);
        if (!await Conexion(id))
        {
            return false;
        }

        return await Modulo(id).Sync.CargarFichaAsync(fichaId);
    }

    public static void PublicarOrden(OrdenCompraRegistrada orden)
    {
        Ordenes.Publicar([new DocumentoNube(orden.Id.ToString("D"), ModulosDocumento.CrearOrden(orden))], [], false);
    }

    public static void EliminarOrden(Guid id)
    {
        Ordenes.Publicar([], [id.ToString("D")], false);
    }

    public static void PublicarMerma(IReadOnlyList<MermaRegistrada> registros)
    {
        var documentos = registros
            .Where(m => m.Id != Guid.Empty)
            .Select(m => new DocumentoNube(m.Id.ToString("D"), ModulosDocumento.CrearMerma(m)))
            .ToList();
        if (documentos.Count == 0)
        {
            return;
        }

        Merma.Publicar(documentos, [], false);
    }

    public static void PublicarControlMantenimiento()
    {
        Mantenimiento.Publicar(
            CopiarControlMantenimiento(),
            [ControlMantenimientoRepository.IdDocumentoRangos],
            false);
    }

    public static void PublicarProveedores(IReadOnlyList<ProveedorRegistro> proveedores)
    {
        var fichas = Fichas();
        var documentos = proveedores
            .Select(p => new DocumentoNube(p.Id.ToString("D"), ModulosDocumento.CrearProveedor(p, fichas)))
            .ToList();
        Proveedores.Publicar(documentos, [], true);
    }

    internal static bool ReflejoSuspendido => Volatile.Read(ref _sinReflejo) > 0;

    internal static void SinReflejo(Action accion)
    {
        Interlocked.Increment(ref _sinReflejo);
        try
        {
            accion();
        }
        finally
        {
            Interlocked.Decrement(ref _sinReflejo);
        }
    }

    internal static void Reportar(string texto)
    {
        if (_aviso == texto)
        {
            return;
        }

        _aviso = texto;
        PublicarEstado();
    }

    private static async Task AbrirAsync(string id)
    {
        var modulo = Modulo(id);
        ReportarModulo(id, "Conectando " + modulo.Nombre + "…");
        if (!await Conexion(id))
        {
            return;
        }

        if (id is not BasesFirebase.Registro and not BasesFirebase.Precios)
        {
            await modulo.Sync.EmpujarLocalAsync();
        }

        modulo.Sync.Escuchar();
        var independiente = true;
        lock (_apertura)
        {
            _independiente.TryGetValue(id, out independiente);
        }

        ReportarModulo(id, independiente
            ? modulo.Nombre + " en vivo, solo datos modificados"
            : modulo.Nombre + " en vivo, solo datos modificados, en la colección compartida");
    }

    private static Task<bool> Conexion(string id)
    {
        lock (_apertura)
        {
            if (_conexiones.TryGetValue(id, out var existente))
            {
                return existente;
            }

            var tarea = ConectarUnoAsync(id);
            _conexiones[id] = tarea;
            return tarea;
        }
    }

    private static async Task<bool> ConectarUnoAsync(string id)
    {
        var modulo = Modulo(id);
        try
        {
            var credencial = await ConexionFirebase.CredencialAsync();
            if (credencial is null)
            {
                QuitarConexion(id);
                if (QuiereEscucha(id))
                {
                    Reportar("Módulos locales: no se encontraron las credenciales de Firebase.");
                }

                return false;
            }

            var historial = id is BasesFirebase.Registro or BasesFirebase.Precios;
            var independiente = false;
            if (!historial)
            {
                independiente = await BasesFirebase.AsegurarUnaAsync(credencial, id, QuiereEscucha(id) ? Reportar : static _ => { });
            }

            lock (_apertura)
            {
                _independiente[id] = independiente;
            }

            FirestoreDb? db = independiente
                ? await ConexionFirebase.AbrirAsync(id)
                : await ConexionFirebase.AbrirAsync();
            if (db is null)
            {
                QuitarConexion(id);
                if (QuiereEscucha(id))
                {
                    Reportar("Módulos locales: no se encontraron las credenciales de Firebase.");
                }

                return false;
            }

            IReadOnlyList<string>? campos = id switch
            {
                BasesFirebase.Registro => ModulosDocumento.CamposMovimiento,
                BasesFirebase.Precios => ModulosDocumento.CamposPrecio,
                _ => null
            };
            modulo.Sync.Preparar(db, modulo.Copiar, modulo.Aplicar, historial, campos);
            return true;
        }
        catch (Exception ex)
        {
            QuitarConexion(id);
            if (QuiereEscucha(id))
            {
                Reportar(modulo.Nombre + ": " + ex.Message);
            }

            return false;
        }
    }

    private static bool QuiereEscucha(string id)
    {
        lock (_apertura)
        {
            return _escuchar.Contains(id);
        }
    }

    private static void ReportarModulo(string id, string texto)
    {
        lock (_apertura)
        {
            _estadosModulo[id] = texto;
        }

        PublicarEstado();
    }

    private static void PublicarEstado()
    {
        string texto;
        lock (_apertura)
        {
            var partes = new List<string>();
            if (_estadosModulo.Count > 0)
            {
                partes.Add(string.Join(" · ", _estadosModulo.Values));
            }

            if (!string.IsNullOrEmpty(_aviso))
            {
                partes.Add(_aviso);
            }

            texto = string.Join("\n", partes);
        }

        if (_estado == texto)
        {
            return;
        }

        _estado = texto;
        EstadoCambiado?.Invoke();
    }

    private static void QuitarConexion(string id)
    {
        lock (_apertura)
        {
            _conexiones.Remove(id);
            _escuchar.Remove(id);
            _independiente.Remove(id);
            _estadosModulo.Remove(id);
        }

        PublicarEstado();
    }

    private static (SincronizadorNube Sync, string Nombre, Func<IReadOnlyList<DocumentoNube>> Copiar, Action<IReadOnlyList<DocumentoNube>, IReadOnlyList<string>, bool> Aplicar) Modulo(string id) =>
        id switch
        {
            BasesFirebase.Vencimientos => (Vencimientos, "Vencimientos", CopiarVencimientos, AplicarVencimientos),
            BasesFirebase.Registro => (Registro, "Registro", CopiarRegistro, AplicarRegistro),
            BasesFirebase.Precios => (Precios, "Historial de precios", CopiarPrecios, AplicarPrecios),
            BasesFirebase.Ordenes => (Ordenes, "Órdenes de compra", CopiarOrdenes, AplicarOrdenes),
            BasesFirebase.Merma => (Merma, "Merma", CopiarMerma, AplicarMerma),
            BasesFirebase.Mantenimiento => (Mantenimiento, "Mantenimiento", CopiarControlMantenimiento, AplicarControlMantenimiento),
            _ => (Proveedores, "Proveedores", CopiarProveedores, AplicarProveedores)
        };

    private static async Task ReflejarAsync(int version, CancellationToken cancelacion)
    {
        try
        {
            await Task.Delay(400, cancelacion);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (version != Volatile.Read(ref _relojVersion))
        {
            return;
        }

        var fichas = Fichas();
        Vencimientos.Publicar(CopiarVencimientos(fichas), [], true);
        App.Instance.Proveedores.SincronizarDesdeFichas(fichas, AlmacenHistorialProveedores.Guardados);
        Proveedores.Publicar(CopiarProveedores(fichas), [], true);
    }

    private static void AplicarVencimientos(IReadOnlyList<DocumentoNube> documentos, IReadOnlyList<string> eliminados, bool completo)
    {
        var repo = App.Instance.Repositorio;
        var porFicha = new Dictionary<Guid, FichaTecnica>();
        foreach (var ficha in repo.CopiarTodas())
        {
            porFicha[ficha.Id] = ficha;
        }

        var tocadas = new HashSet<Guid>();
        var presentes = new HashSet<Guid>();
        if (completo)
        {
            foreach (var documento in documentos)
            {
                if (ModulosDocumento.LeerVencimiento(documento, out var fichaId, out _))
                {
                    presentes.Add(fichaId);
                }
            }

            foreach (var fichaId in presentes)
            {
                if (porFicha.TryGetValue(fichaId, out var ficha))
                {
                    ficha.Partidas = [];
                    tocadas.Add(fichaId);
                }
            }
        }

        foreach (var id in eliminados)
        {
            if (!Guid.TryParse(id, out var partidaId))
            {
                continue;
            }

            foreach (var ficha in porFicha.Values)
            {
                if (ficha.Partidas.RemoveAll(p => p.Id == partidaId) > 0)
                {
                    tocadas.Add(ficha.Id);
                }
            }
        }

        foreach (var documento in documentos)
        {
            if (!ModulosDocumento.LeerVencimiento(documento, out var fichaId, out var partida) || !porFicha.TryGetValue(fichaId, out var ficha))
            {
                continue;
            }

            var indice = ficha.Partidas.FindIndex(p => p.Id == partida.Id);
            if (indice >= 0)
            {
                ficha.Partidas[indice] = partida;
            }
            else
            {
                ficha.Partidas.Add(partida);
            }

            tocadas.Add(ficha.Id);
        }

        var cambios = new List<FichaTecnica>();
        foreach (var id in tocadas)
        {
            var ficha = porFicha[id];
            PartidasInventario.Normalizar(ficha);
            ficha.Partidas = ficha.Partidas.OrderBy(p => p.Id).ToList();
            var actual = repo.Obtener(id);
            if (actual is not null && InventarioDocumento.Huella(actual) == InventarioDocumento.Huella(ficha))
            {
                continue;
            }

            cambios.Add(ficha);
        }

        var tocadasLista = new List<FichaTecnica>(tocadas.Count);
        foreach (var id in tocadas)
        {
            tocadasLista.Add(porFicha[id]);
        }

        Vencimientos.Recordar(CopiarVencimientos(tocadasLista), eliminados);
        if (cambios.Count == 0)
        {
            return;
        }

        SinReflejo(() => repo.AplicarDesdeNube(cambios, []));
        repo.PublicarEnInventario(cambios);
        DatosCambiados?.Invoke();
    }

    private static void AplicarRegistro(IReadOnlyList<DocumentoNube> documentos, IReadOnlyList<string> eliminados, bool completo)
    {
        var movimientos = documentos.Select(ModulosDocumento.LeerMovimiento).OfType<MovimientoInventario>().ToList();
        var ids = eliminados.Select(id => Guid.TryParse(id, out var valor) ? valor : Guid.Empty).Where(id => id != Guid.Empty).ToList();
        if (App.Instance.Registro.AplicarDesdeNube(movimientos, ids, completo))
        {
            DatosCambiados?.Invoke();
        }
    }

    private static void AplicarPrecios(IReadOnlyList<DocumentoNube> documentos, IReadOnlyList<string> eliminados, bool completo)
    {
        var precios = documentos.Select(ModulosDocumento.LeerPrecio).OfType<PrecioProducto>().ToList();
        var ids = eliminados.Select(id => Guid.TryParse(id, out var valor) ? valor : Guid.Empty).Where(id => id != Guid.Empty).ToList();
        if (App.Instance.HistorialPrecios.AplicarDesdeNube(precios, ids, completo))
        {
            DatosCambiados?.Invoke();
        }
    }

    private static void AplicarOrdenes(IReadOnlyList<DocumentoNube> documentos, IReadOnlyList<string> eliminados, bool completo)
    {
        var ordenes = documentos.Select(ModulosDocumento.LeerOrden).OfType<OrdenCompraRegistrada>().ToList();
        var ids = eliminados.Select(id => Guid.TryParse(id, out var valor) ? valor : Guid.Empty).Where(id => id != Guid.Empty).ToList();
        if (App.Instance.OrdenesCompra.AplicarDesdeNube(ordenes, ids, completo))
        {
            DatosCambiados?.Invoke();
        }
    }

    private static void AplicarMerma(IReadOnlyList<DocumentoNube> documentos, IReadOnlyList<string> eliminados, bool completo)
    {
        var registros = documentos.Select(ModulosDocumento.LeerMerma).OfType<MermaRegistrada>().ToList();
        var ids = eliminados.Select(id => Guid.TryParse(id, out var valor) ? valor : Guid.Empty).Where(id => id != Guid.Empty).ToList();
        if (App.Instance.Mermas.AplicarDesdeNube(registros, ids, completo))
        {
            DatosCambiados?.Invoke();
        }
    }

    private static void AplicarControlMantenimiento(IReadOnlyList<DocumentoNube> documentos, IReadOnlyList<string> eliminados, bool completo)
    {
        var equipos = documentos.Select(ModulosDocumento.LeerEquipoControl).OfType<EquipoControl>().ToList();
        var rangosDoc = documentos.FirstOrDefault(d => d.Id == ControlMantenimientoRepository.IdDocumentoRangos);
        var heredados = rangosDoc is null ? null : ModulosDocumento.LeerRangosControl(rangosDoc);
        foreach (var equipo in equipos)
        {
            equipo.NormalizarPlazo(heredados);
        }

        var ids = eliminados
            .Select(id => Guid.TryParse(id, out var valor) ? valor : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
        if (App.Instance.ControlMantenimiento.AplicarDesdeNube(equipos, ids, completo))
        {
            DatosCambiados?.Invoke();
        }
    }

    private static void AplicarProveedores(IReadOnlyList<DocumentoNube> documentos, IReadOnlyList<string> eliminados, bool completo)
    {
        var proveedores = documentos.Select(ModulosDocumento.LeerProveedor).OfType<ProveedorRegistro>().ToList();
        var ids = eliminados.Select(id => Guid.TryParse(id, out var valor) ? valor : Guid.Empty).Where(id => id != Guid.Empty).ToList();
        if (App.Instance.Proveedores.AplicarDesdeNube(proveedores, ids, completo))
        {
            DatosCambiados?.Invoke();
        }
    }

    private static IReadOnlyList<DocumentoNube> CopiarVencimientos() => CopiarVencimientos(Fichas());

    private static IReadOnlyList<DocumentoNube> CopiarVencimientos(IReadOnlyList<FichaTecnica> fichas)
    {
        var lista = new List<DocumentoNube>();
        foreach (var ficha in fichas)
        {
            foreach (var partida in ficha.Partidas)
            {
                if (partida.Id == Guid.Empty)
                {
                    continue;
                }

                lista.Add(new DocumentoNube(partida.Id.ToString("D"), ModulosDocumento.CrearVencimiento(ficha, partida)));
            }
        }

        return lista;
    }

    private static IReadOnlyList<DocumentoNube> CopiarRegistro() => CopiarRegistro(Fichas());

    private static IReadOnlyList<DocumentoNube> CopiarRegistro(IReadOnlyList<FichaTecnica> fichas)
    {
        var porId = fichas.ToDictionary(f => f.Id);
        return App.Instance.Registro.CopiarTodos()
            .Select(m => new DocumentoNube(
                m.Id.ToString("D"),
                ModulosDocumento.CrearMovimiento(m, porId.GetValueOrDefault(m.FichaId))))
            .ToList();
    }

    private static IReadOnlyList<DocumentoNube> CopiarPrecios() => CopiarPrecios(Fichas());

    private static IReadOnlyList<DocumentoNube> CopiarPrecios(IReadOnlyList<FichaTecnica> fichas)
    {
        var porId = fichas.ToDictionary(f => f.Id);
        return App.Instance.HistorialPrecios.CopiarTodos()
            .Select(p => new DocumentoNube(
                p.Id.ToString("D"),
                ModulosDocumento.CrearPrecio(p, porId.GetValueOrDefault(p.FichaId))))
            .ToList();
    }

    private static IReadOnlyList<DocumentoNube> CopiarOrdenes() =>
        App.Instance.OrdenesCompra.Listar()
            .Select(o => new DocumentoNube(o.Id.ToString("D"), ModulosDocumento.CrearOrden(o)))
            .ToList();

    private static IReadOnlyList<DocumentoNube> CopiarMerma() =>
        App.Instance.Mermas.Listar()
            .Select(m => new DocumentoNube(m.Id.ToString("D"), ModulosDocumento.CrearMerma(m)))
            .ToList();

    private static IReadOnlyList<DocumentoNube> CopiarControlMantenimiento()
    {
        return App.Instance.ControlMantenimiento.Listar()
            .Where(e => e.Id != Guid.Empty)
            .Select(e => new DocumentoNube(e.Id.ToString("D"), ModulosDocumento.CrearEquipoControl(e)))
            .ToList();
    }

    private static IReadOnlyList<DocumentoNube> CopiarProveedores() => CopiarProveedores(Fichas());

    private static IReadOnlyList<DocumentoNube> CopiarProveedores(IReadOnlyList<FichaTecnica> fichas) =>
        App.Instance.Proveedores.Listar()
            .Select(p => new DocumentoNube(p.Id.ToString("D"), ModulosDocumento.CrearProveedor(p, fichas)))
            .ToList();

    private static List<FichaTecnica> Fichas() => App.Instance.Repositorio.CopiarTodas();

    private static FichaTecnica? Buscar(IReadOnlyList<FichaTecnica> fichas, Guid id) =>
        fichas.FirstOrDefault(f => f.Id == id);
}
