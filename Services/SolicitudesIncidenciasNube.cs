using System.Collections.ObjectModel;
using System.Globalization;
using Google.Cloud.Firestore;
using Iderma.Core.Services;
using IdermaFichas.Models;
using Microsoft.UI.Dispatching;

namespace IdermaFichas.Services;

public static class SolicitudesIncidenciasNube
{
    public const string Coleccion = "solicitudes-incidencias";

    private static FirestoreDb? _base;
    private static FirestoreChangeListener? _escucha;
    private static int _iniciado;
    private static DispatcherQueue? _ui;
    private static readonly HashSet<string> Vistas = new(StringComparer.OrdinalIgnoreCase);
    private static bool _escuchaInicial = true;

    public static ObservableCollection<SolicitudIncidencia> Items { get; } = [];

    public static int Pendientes => Items.Count(s => s.EstaPendiente);

    public static event Action? Cambiado;

    public static event Action<SolicitudIncidencia>? NuevaPendiente;

    public static void Iniciar(DispatcherQueue? ui = null)
    {
        if (ui is not null)
        {
            _ui = ui;
        }

        if (Interlocked.Exchange(ref _iniciado, 1) == 1)
        {
            return;
        }

        foreach (var item in SolicitudesIncidenciasLocales.Cargar())
        {
            Items.Add(item);
            Vistas.Add(item.Id);
        }

        Cambiado?.Invoke();
        _ = ConectarAsync();
    }

    public static async Task AceptarAsync(SolicitudIncidencia solicitud)
    {
        if (!solicitud.EstaPendiente)
        {
            throw new InvalidOperationException("Esa solicitud ya fue respondida.");
        }

        await ResponderAsync(solicitud, EstadoIncidencia.Aceptada, string.Empty);
    }

    public static async Task VincularOrdenAsync(string solicitudId, Guid ordenId, string estadoOrden)
    {
        await AsegurarBaseAsync();
        if (_base is null)
        {
            throw new InvalidOperationException("No hay conexión con Firebase.");
        }

        var estado = TextoEstadoOrden(estadoOrden);
        if (estado.Length == 0)
        {
            estado = "Borrador";
        }

        await _base.Collection(Coleccion).Document(solicitudId).UpdateAsync(new Dictionary<string, object>
        {
            ["Estado"] = EstadoIncidencia.Aceptada.ToString(),
            ["Orden id"] = ordenId.ToString("D"),
            ["Estado orden"] = estado,
            ["Fecha respuesta"] = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture),
            ["Actualizado"] = FieldValue.ServerTimestamp
        });
        await EnUiAsync(() => MarcarOrdenLocal(solicitudId, ordenId, estado));
    }

    public static async Task SincronizarEstadoOrdenAsync(Guid ordenId, string estadoOrden)
    {
        if (ordenId == Guid.Empty)
        {
            return;
        }

        await AsegurarBaseAsync();
        if (_base is null)
        {
            return;
        }

        var estado = TextoEstadoOrden(estadoOrden);
        var id = ordenId.ToString("D");
        var consulta = await _base.Collection(Coleccion).WhereEqualTo("Orden id", id).GetSnapshotAsync();
        foreach (var documento in consulta.Documents)
        {
            await documento.Reference.UpdateAsync(new Dictionary<string, object>
            {
                ["Estado orden"] = estado,
                ["Actualizado"] = FieldValue.ServerTimestamp
            });
            await EnUiAsync(() => MarcarOrdenLocal(documento.Id, ordenId, estado));
        }
    }

    private static void MarcarOrdenLocal(string solicitudId, Guid ordenId, string estado)
    {
        var actual = Items.FirstOrDefault(s => s.Id.Equals(solicitudId, StringComparison.OrdinalIgnoreCase));
        if (actual is null)
        {
            return;
        }

        if (actual.Estado == EstadoIncidencia.Pendiente)
        {
            actual.Estado = EstadoIncidencia.Aceptada;
        }

        actual.OrdenId = ordenId.ToString("D");
        actual.EstadoOrden = estado;
        SolicitudesIncidenciasLocales.Guardar(Items);
        Cambiado?.Invoke();
    }

    public static string TextoEstadoOrden(string texto) => texto.Trim().ToLowerInvariant() switch
    {
        "borrador" => "Borrador",
        "en revisión" or "en revision" or "revisión" or "revision" => "En revisión",
        "aprobada" or "aprobado" => "Aprobada",
        _ => string.Empty
    };

    public static async Task DenegarAsync(SolicitudIncidencia solicitud, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new InvalidOperationException("Escribe el motivo de la denegación.");
        }

        await ResponderAsync(solicitud, EstadoIncidencia.Denegada, motivo.Trim());
    }

    private static async Task ResponderAsync(SolicitudIncidencia solicitud, EstadoIncidencia estado, string motivo)
    {
        await AsegurarBaseAsync();
        if (_base is null)
        {
            throw new InvalidOperationException("No hay conexión con Firebase.");
        }

        var ahora = DateTimeOffset.Now;
        await _base.Collection(Coleccion).Document(solicitud.Id).UpdateAsync(new Dictionary<string, object>
        {
            ["Estado"] = estado.ToString(),
            ["Motivo"] = motivo,
            ["Fecha respuesta"] = ahora.ToString("o", CultureInfo.InvariantCulture),
            ["Actualizado"] = FieldValue.ServerTimestamp
        });

        await EnUiAsync(() =>
        {
            solicitud.Estado = estado;
            solicitud.Motivo = motivo;
            solicitud.FechaRespuesta = ahora;
            SolicitudesIncidenciasLocales.Guardar(Items);
            Cambiado?.Invoke();
        });
    }

    private static async Task ConectarAsync()
    {
        try
        {
            await AsegurarBaseAsync();
            if (_base is null)
            {
                return;
            }

            await RecuperarHistorialAsync();
            var guardado = CursorNube.Cargar(Coleccion + "-fichas");
            _escuchaInicial = !guardado.HasValue;
            var desde = guardado ?? CursorNube.Epoca;
            _escucha = _base.Collection(Coleccion)
                .WhereGreaterThan("Actualizado", desde)
                .OrderBy("Actualizado")
                .Listen(snapshot =>
                {
                    AlRecibir(snapshot);
                    CursorNube.GuardarSiAvanza(Coleccion + "-fichas", snapshot.ReadTime);
                });
        }
        catch
        {
        }
    }

    private static async Task RecuperarHistorialAsync()
    {
        if (_base is null)
        {
            return;
        }

        var snapshot = await _base.Collection(Coleccion).GetSnapshotAsync();
        var recibidas = new List<SolicitudIncidencia>();
        foreach (var documento in snapshot.Documents)
        {
            var solicitud = Leer(documento);
            if (solicitud is not null)
            {
                recibidas.Add(solicitud);
            }
        }

        if (recibidas.Count == 0)
        {
            return;
        }

        EnUi(() =>
        {
            foreach (var llegada in recibidas)
            {
                var actual = Items.FirstOrDefault(s => s.Id.Equals(llegada.Id, StringComparison.OrdinalIgnoreCase));
                if (actual is null)
                {
                    Items.Insert(0, llegada);
                }
                else
                {
                    Copiar(actual, llegada);
                }

                Vistas.Add(llegada.Id);
            }

            SolicitudesIncidenciasLocales.Guardar(Items);
            Cambiado?.Invoke();
        });
    }

    private static void AlRecibir(QuerySnapshot snapshot)
    {
        try
        {
            var recibidas = new List<(SolicitudIncidencia Solicitud, bool EsNueva)>();
            var eliminadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cambio in snapshot.Changes)
            {
                if (cambio.ChangeType == DocumentChange.Type.Removed)
                {
                    eliminadas.Add(cambio.Document.Id);
                    continue;
                }

                var solicitud = Leer(cambio.Document);
                if (solicitud is not null)
                {
                    recibidas.Add((solicitud, cambio.ChangeType == DocumentChange.Type.Added));
                }
            }

            if (recibidas.Count == 0 && eliminadas.Count == 0)
            {
                _escuchaInicial = false;
                return;
            }

            EnUi(() =>
            {
                var inicial = _escuchaInicial;
                _escuchaInicial = false;
                foreach (var id in eliminadas)
                {
                    var existente = Items.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                    if (existente is not null)
                    {
                        Items.Remove(existente);
                        Vistas.Remove(id);
                    }
                }

                foreach (var (llegada, esNuevaDoc) in recibidas)
                {
                    var actual = Items.FirstOrDefault(s => s.Id.Equals(llegada.Id, StringComparison.OrdinalIgnoreCase));
                    var esNueva = !Vistas.Contains(llegada.Id);
                    if (actual is null)
                    {
                        Items.Insert(0, llegada);
                    }
                    else
                    {
                        Copiar(actual, llegada);
                    }

                    Vistas.Add(llegada.Id);
                    if (!inicial && (esNueva || esNuevaDoc) && llegada.EstaPendiente)
                    {
                        NuevaPendiente?.Invoke(actual ?? llegada);
                    }
                }

                SolicitudesIncidenciasLocales.Guardar(Items);
                Cambiado?.Invoke();
            });
        }
        catch
        {
        }
    }

    private static void Copiar(SolicitudIncidencia destino, SolicitudIncidencia origen)
    {
        var localAtendida = destino.Estado != EstadoIncidencia.Pendiente
            || !string.IsNullOrWhiteSpace(destino.OrdenId);
        var llegadaPendiente = origen.Estado == EstadoIncidencia.Pendiente
            && string.IsNullOrWhiteSpace(origen.OrdenId);
        if (localAtendida && llegadaPendiente)
        {
            return;
        }

        destino.Folio = origen.Folio;
        destino.Solicitante = origen.Solicitante;
        destino.Area = origen.Area;
        destino.Tipo = origen.Tipo;
        destino.Nombre = origen.Nombre;
        destino.Enlace = origen.Enlace;
        destino.DetalleTexto = origen.DetalleTexto;
        destino.Unidad = origen.Unidad;
        destino.Cantidad = origen.Cantidad;
        destino.PrecioReposicion = origen.PrecioReposicion;
        destino.Urgente = origen.Urgente;
        destino.OrdenId = origen.OrdenId;
        destino.EstadoOrden = origen.EstadoOrden;
        destino.Fecha = origen.Fecha;
        destino.Estado = origen.Estado;
        destino.Motivo = origen.Motivo;
        destino.FechaRespuesta = origen.FechaRespuesta;
    }

    private static SolicitudIncidencia? Leer(DocumentSnapshot doc)
    {
        if (!doc.Exists)
        {
            return null;
        }

        var datos = doc.ToDictionary();
        return new SolicitudIncidencia
        {
            Id = doc.Id,
            Folio = Texto(datos, "Folio"),
            Solicitante = Texto(datos, "Solicitante"),
            Area = Texto(datos, "Área", "Area"),
            Tipo = ParseTipo(Texto(datos, "Tipo")),
            Nombre = Texto(datos, "Nombre"),
            Enlace = Texto(datos, "Enlace"),
            DetalleTexto = Texto(datos, "Detalle"),
            Unidad = Texto(datos, "Unidad") is { Length: > 0 } unidad ? unidad : "und",
            Cantidad = Numero(datos, "Cantidad"),
            PrecioReposicion = Numero(datos, "Precio reposición", "PrecioReposicion"),
            Urgente = Bool(datos, "Urgente"),
            OrdenId = Texto(datos, "Orden id"),
            EstadoOrden = TextoEstadoOrden(Texto(datos, "Estado orden")),
            Fecha = Fecha(datos, "Fecha") ?? DateTimeOffset.Now,
            Estado = ParseEstado(Texto(datos, "Estado")),
            Motivo = Texto(datos, "Motivo"),
            FechaRespuesta = Fecha(datos, "Fecha respuesta", "FechaRespuesta")
        };
    }

    private static async Task AsegurarBaseAsync()
    {
        if (_base is not null)
        {
            return;
        }

        _base = await ConexionFirebase.AbrirAsync();
    }

    private static void EnUi(Action accion)
    {
        var cola = _ui ?? DispatcherQueue.GetForCurrentThread();
        if (cola is null || cola.HasThreadAccess)
        {
            accion();
            return;
        }

        cola.TryEnqueue(() => accion());
    }

    private static Task EnUiAsync(Action accion)
    {
        var cola = _ui ?? DispatcherQueue.GetForCurrentThread();
        if (cola is null || cola.HasThreadAccess)
        {
            accion();
            return Task.CompletedTask;
        }

        var listo = new TaskCompletionSource();
        cola.TryEnqueue(() =>
        {
            try
            {
                accion();
                listo.SetResult();
            }
            catch (Exception ex)
            {
                listo.SetException(ex);
            }
        });
        return listo.Task;
    }

    private static TipoIncidencia ParseTipo(string texto) => texto.ToLowerInvariant() switch
    {
        "equipoelectronico" or "equipo electrónico" or "equipo electronico" => TipoIncidencia.EquipoElectronico,
        "equipo" => TipoIncidencia.Equipo,
        "recurso" => TipoIncidencia.Recurso,
        _ => TipoIncidencia.Insumo
    };

    private static EstadoIncidencia ParseEstado(string texto) => texto.ToLowerInvariant() switch
    {
        "aceptada" => EstadoIncidencia.Aceptada,
        "denegada" => EstadoIncidencia.Denegada,
        _ => EstadoIncidencia.Pendiente
    };

    private static string Texto(IDictionary<string, object> datos, params string[] claves)
    {
        foreach (var clave in claves)
        {
            if (datos.TryGetValue(clave, out var valor) && valor is not null)
            {
                var texto = Convert.ToString(valor, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
                if (texto.Length > 0)
                {
                    return texto;
                }
            }
        }

        return string.Empty;
    }

    private static double Numero(IDictionary<string, object> datos, params string[] claves)
    {
        var texto = Texto(datos, claves);
        return double.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out var valor) ? valor : 0;
    }

    private static bool Bool(IDictionary<string, object> datos, string clave) =>
        datos.TryGetValue(clave, out var valor) && valor is bool activo && activo;

    private static DateTimeOffset? Fecha(IDictionary<string, object> datos, params string[] claves)
    {
        var texto = Texto(datos, claves);
        return DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fecha)
            ? fecha
            : null;
    }
}
