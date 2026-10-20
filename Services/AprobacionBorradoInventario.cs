using System.Collections.ObjectModel;
using System.Globalization;
using Google.Cloud.Firestore;
using Iderma.Core.Services;
using IdermaFichas.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace IdermaFichas.Services;

/// <summary>
/// Si el borrado supera el 10 % del inventario, logística pide la contraseña
/// del usuario en sesión y deja la eliminación pendiente hasta que Base de datos la autorice.
/// </summary>
public static class AprobacionBorradoInventario
{
    public const string Coleccion = "aprobaciones-datos";
    public const string Tipo = "borrado-inventario";

    private static readonly object Candado = new();
    private static readonly HashSet<string> EnCurso = new(StringComparer.OrdinalIgnoreCase);
    private static FirestoreDb? _base;
    private static FirestoreChangeListener? _escucha;
    private static DispatcherQueue? _ui;
    private static int _iniciado;
    private static bool _primeraCarga = true;

    public static ObservableCollection<AvisoAutorizacion> Items { get; } = [];

    public static int Pendientes => Items.Count(a => a.EstaPendiente);

    public static event Action? Cambiado;

    public static bool Requiere(int cantidad, int total) =>
        total > 0 && cantidad * 100m > total * 10m;

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

        foreach (var aviso in AutorizacionesBorradoLocales.Cargar())
        {
            Items.Add(aviso);
        }

        Cambiado?.Invoke();
        _ = EscucharAsync();
    }

    /// <summary>
    /// Devuelve true cuando el borrado no debe continuar en logística:
    /// se canceló, faltó la sesión o la solicitud quedó en Base de datos.
    /// </summary>
    public static async Task<bool> InterceptarAsync(XamlRoot raiz, IReadOnlyList<FichaTecnica> fichas)
    {
        var lista = fichas.Where(f => f is not null).ToList();
        var total = App.Instance.Repositorio.Todas.Count;
        if (!Requiere(lista.Count, total))
        {
            return false;
        }

        using var sesiones = new SessionStore();
        var sesion = sesiones.Snapshot();
        if (string.IsNullOrWhiteSpace(sesion.Username))
        {
            await AvisoAsync(
                raiz,
                "Hace falta iniciar sesión",
                "Para borrar más del 10 % del inventario tienes que entrar con tu usuario desde el launcher.");
            return true;
        }

        var porcentaje = lista.Count * 100m / total;
        var nombre = string.IsNullOrWhiteSpace(sesion.DisplayName) ? sesion.Username : sesion.DisplayName;
        while (true)
        {
            var clave = new PasswordBox
            {
                Header = "Contraseña",
                PlaceholderText = "La de tu usuario en el launcher",
                PasswordRevealMode = PasswordRevealMode.Peek
            };
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text =
                    $"Vas a borrar {lista.Count} de {total} productos ({porcentaje:0.#} %). " +
                    "Como supera el 10 % del inventario, logística no los elimina todavía. " +
                    $"Ingresa la contraseña de {nombre}. Base de datos tiene que autorizar el borrado."
            });
            panel.Children.Add(clave);
            var dialogo = new ContentDialog
            {
                Title = "Autorización de Base de datos",
                Content = panel,
                PrimaryButtonText = "Enviar solicitud",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = raiz
            };

            if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            {
                return true;
            }

            if (string.IsNullOrEmpty(clave.Password))
            {
                await AvisoAsync(raiz, "Contraseña", "Escribe la contraseña de tu usuario.");
                continue;
            }

            using var usuarios = new UserStore();
            if (usuarios.Authenticate(sesion.Username, clave.Password) is null)
            {
                await AvisoAsync(raiz, "Contraseña", "La contraseña no coincide con tu usuario.");
                continue;
            }

            try
            {
                await PublicarAsync(lista, total, sesion.UserId ?? string.Empty, sesion.Username, nombre);
            }
            catch (Exception ex)
            {
                await AvisoAsync(raiz, "No se envió la solicitud", ex.Message);
                return true;
            }

            await AvisoAsync(
                raiz,
                "Solicitud enviada",
                "Base de datos recibió el pedido. Los productos siguen en el inventario hasta que allí lo autoricen.");
            return true;
        }
    }

    private static async Task PublicarAsync(
        IReadOnlyList<FichaTecnica> fichas,
        int total,
        string userId,
        string usuario,
        string nombre)
    {
        await AsegurarBaseAsync();
        if (_base is null)
        {
            throw new InvalidOperationException("No se encontraron las credenciales de Firebase.");
        }

        var id = Guid.NewGuid().ToString("N");
        var ahora = DateTimeOffset.Now;
        var porcentaje = fichas.Count * 100m / total;
        var datos = new Dictionary<string, object>
        {
            ["Tipo"] = Tipo,
            ["Origen"] = "logistica",
            ["Folio"] = "BD-" + ahora.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture),
            ["Solicitante"] = nombre,
            ["Usuario"] = usuario,
            ["UserId"] = userId,
            ["Fecha"] = ahora.ToString("o", CultureInfo.InvariantCulture),
            ["Estado"] = "Pendiente",
            ["Total inventario"] = total,
            ["Cantidad"] = fichas.Count,
            ["Porcentaje"] = decimal.ToDouble(decimal.Round(porcentaje, 1)),
            ["Motivo denegación"] = string.Empty,
            ["Respondido por"] = string.Empty,
            ["Fecha respuesta"] = string.Empty,
            ["Aplicada"] = false,
            ["Productos"] = fichas.Select(f => (object)new Dictionary<string, object>
            {
                ["Id"] = f.Id.ToString("N"),
                ["Código"] = f.Codigo,
                ["Nombre"] = f.Nombre
            }).ToList(),
            ["Actualizado"] = FieldValue.ServerTimestamp
        };

        await _base.Collection(Coleccion).Document(id).SetAsync(datos);
    }

    private static async Task EscucharAsync()
    {
        try
        {
            await AsegurarBaseAsync();
            if (_base is null)
            {
                return;
            }

            await RecuperarHistorialAsync();
            var guardado = CursorNube.Cargar(Coleccion + "-" + Tipo);
            _primeraCarga = !guardado.HasValue;
            var desde = guardado ?? CursorNube.Epoca;
            _escucha = _base.Collection(Coleccion)
                .WhereGreaterThan("Actualizado", desde)
                .OrderBy("Actualizado")
                .Listen(snapshot =>
                {
                    AlRecibir(snapshot);
                    CursorNube.GuardarSiAvanza(Coleccion + "-" + Tipo, snapshot.ReadTime);
                });
        }
        catch
        {
        }
    }

    private static void AlRecibir(QuerySnapshot snapshot)
    {
        try
        {
            var avisos = new List<AvisoAutorizacion>();
            var eliminadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cambios = new List<CambioBorrado>();
            foreach (var cambio in snapshot.Changes)
            {
                if (cambio.ChangeType == DocumentChange.Type.Removed || !cambio.Document.Exists)
                {
                    eliminadas.Add(cambio.Document.Id);
                    continue;
                }

                var datos = cambio.Document.ToDictionary();
                if (!Texto(datos, "Tipo").Equals(Tipo, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                avisos.Add(LeerAviso(cambio.Document.Id, datos));
                var estado = Texto(datos, "Estado");
                if (estado is not ("Autorizada" or "Denegada"))
                {
                    continue;
                }

                cambios.Add(new CambioBorrado(
                    cambio.Document.Id,
                    estado,
                    Bool(datos, "Aplicada"),
                    Texto(datos, "Motivo denegación", "MotivoDenegacion"),
                    Texto(datos, "Respondido por"),
                    Entero(datos, "Cantidad"),
                    LeerIds(datos)));
            }

            if (avisos.Count == 0 && eliminadas.Count == 0 && cambios.Count == 0)
            {
                _primeraCarga = false;
                return;
            }

            var inicial = _primeraCarga;
            _primeraCarga = false;
            EnUi(() =>
            {
                ActualizarItems(avisos, eliminadas);
                if (cambios.Count > 0)
                {
                    _ = AplicarAsync(cambios, inicial);
                }
            });
        }
        catch
        {
        }
    }

    private static async Task AplicarAsync(IReadOnlyList<CambioBorrado> cambios, bool inicial)
    {
        var aplicadasAhora = 0;
        foreach (var cambio in cambios)
        {
            if (cambio.Estado.Equals("Denegada", StringComparison.OrdinalIgnoreCase))
            {
                if (!inicial)
                {
                    Avisar(
                        "Base de datos denegó el borrado",
                        string.IsNullOrWhiteSpace(cambio.Motivo)
                            ? "Los productos siguen en el inventario."
                            : cambio.Motivo);
                }

                continue;
            }

            if (!cambio.Estado.Equals("Autorizada", StringComparison.OrdinalIgnoreCase) || cambio.Aplicada)
            {
                continue;
            }

            lock (Candado)
            {
                if (!EnCurso.Add(cambio.Id))
                {
                    continue;
                }
            }

            try
            {
                var ids = new List<Guid>();
                foreach (var texto in cambio.Ids)
                {
                    if (Guid.TryParse(texto, out var id))
                    {
                        ids.Add(id);
                    }
                }

                foreach (var id in ids)
                {
                    ImagenFichaService.EliminarDeFicha(id);
                }

                App.Instance.Repositorio.EliminarVarios(ids);
                App.Instance.Repositorio.NotificarVistas();
                await MarcarAplicadaAsync(cambio.Id);
                aplicadasAhora++;
                if (!inicial)
                {
                    var quien = string.IsNullOrWhiteSpace(cambio.RespondidoPor) ? "Base de datos" : cambio.RespondidoPor;
                    Avisar(
                        "Base de datos autorizó el borrado",
                        $"{quien} autorizó eliminar {cambio.Cantidad} productos. Ya se quitaron del inventario.");
                }
            }
            catch (Exception ex)
            {
                Avisar("No se pudo aplicar el borrado", ex.Message);
            }
            finally
            {
                lock (Candado)
                {
                    EnCurso.Remove(cambio.Id);
                }
            }
        }

        if (inicial && aplicadasAhora > 0)
        {
            Avisar(
                "Borrados autorizados",
                aplicadasAhora == 1
                    ? "Se aplicó un borrado que Base de datos ya había autorizado."
                    : $"Se aplicaron {aplicadasAhora} borrados que Base de datos ya había autorizado.");
        }
    }

    private static void ActualizarItems(IReadOnlyList<AvisoAutorizacion> avisos, IReadOnlySet<string> eliminadas)
    {
        foreach (var id in eliminadas)
        {
            var existente = Items.FirstOrDefault(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (existente is not null)
            {
                Items.Remove(existente);
            }
        }

        foreach (var aviso in avisos)
        {
            var indice = -1;
            for (var i = 0; i < Items.Count; i++)
            {
                if (Items[i].Id.Equals(aviso.Id, StringComparison.OrdinalIgnoreCase))
                {
                    indice = i;
                    break;
                }
            }

            if (indice < 0)
            {
                Items.Add(aviso);
            }
            else
            {
                Items[indice] = aviso;
            }
        }

        var orden = Items.OrderByDescending(a => a.EstaPendiente).ThenByDescending(a => a.Fecha).ToList();
        Items.Clear();
        foreach (var aviso in orden)
        {
            Items.Add(aviso);
        }

        AutorizacionesBorradoLocales.Guardar(Items);
        Cambiado?.Invoke();
    }

    private static async Task RecuperarHistorialAsync()
    {
        if (_base is null)
        {
            return;
        }

        var snapshot = await _base.Collection(Coleccion).GetSnapshotAsync();
        var avisos = new List<AvisoAutorizacion>();
        foreach (var documento in snapshot.Documents)
        {
            if (!documento.Exists)
            {
                continue;
            }

            var datos = documento.ToDictionary();
            if (!Texto(datos, "Tipo").Equals(Tipo, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            avisos.Add(LeerAviso(documento.Id, datos));
        }

        if (avisos.Count == 0)
        {
            return;
        }

        EnUi(() => ActualizarItems(avisos, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
    }

    private static AvisoAutorizacion LeerAviso(string id, IDictionary<string, object> datos)
    {
        var estado = Texto(datos, "Estado");
        if (string.IsNullOrWhiteSpace(estado))
        {
            estado = "Pendiente";
        }

        var cantidad = Entero(datos, "Cantidad");
        var total = Entero(datos, "Total inventario");
        var porcentaje = Doble(datos, "Porcentaje");
        var solicitante = Texto(datos, "Solicitante");
        var nombres = LeerNombres(datos);
        var detalle = nombres.Count == 0
            ? "Sin detalle de productos."
            : string.Join(", ", nombres.Take(8)) + (nombres.Count > 8 ? $" y {nombres.Count - 8} más." : string.Empty);
        var respondido = Texto(datos, "Respondido por");
        var motivo = Texto(datos, "Motivo denegación", "MotivoDenegacion");
        var pendiente = estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase);
        var respuesta = pendiente
            ? "Esperando autorización de base de datos."
            : estado.Equals("Denegada", StringComparison.OrdinalIgnoreCase)
                ? (string.IsNullOrWhiteSpace(respondido) ? "Base de datos" : respondido) + " denegó"
                    + (string.IsNullOrWhiteSpace(motivo) ? "." : ": " + motivo)
                : (string.IsNullOrWhiteSpace(respondido) ? "Base de datos" : respondido)
                    + (Bool(datos, "Aplicada") ? " autorizó y el borrado ya se aplicó." : " autorizó el borrado.");

        return new AvisoAutorizacion
        {
            Id = id,
            Folio = Texto(datos, "Folio"),
            Solicitante = solicitante,
            Fecha = Fecha(datos, "Fecha") ?? DateTimeOffset.Now,
            EstadoTexto = estado,
            Resumen = $"{solicitante} · {cantidad} de {total} productos ({porcentaje.ToString("0.#", CultureInfo.CurrentCulture)} %)",
            Detalle = detalle,
            Respuesta = respuesta,
            EstaPendiente = pendiente,
            EstaRespondida = !pendiente
        };
    }

    private static List<string> LeerNombres(IDictionary<string, object> datos)
    {
        var nombres = new List<string>();
        if (!datos.TryGetValue("Productos", out var raw) || raw is not IEnumerable<object> lista)
        {
            return nombres;
        }

        foreach (var item in lista)
        {
            if (item is not IDictionary<string, object> mapa)
            {
                continue;
            }

            var nombre = Texto(mapa, "Nombre");
            if (string.IsNullOrWhiteSpace(nombre))
            {
                nombre = Texto(mapa, "Código", "Codigo");
            }

            if (!string.IsNullOrWhiteSpace(nombre))
            {
                nombres.Add(nombre);
            }
        }

        return nombres;
    }

    private static double Doble(IDictionary<string, object> datos, string clave)
    {
        if (!datos.TryGetValue(clave, out var valor) || valor is null)
        {
            return 0;
        }

        return valor switch
        {
            double n => n,
            float n => n,
            int n => n,
            long n => n,
            _ => double.TryParse(Convert.ToString(valor, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0
        };
    }

    private static DateTimeOffset? Fecha(IDictionary<string, object> datos, params string[] claves)
    {
        foreach (var clave in claves)
        {
            if (!datos.TryGetValue(clave, out var valor) || valor is null)
            {
                continue;
            }

            if (valor is Timestamp marca)
            {
                return marca.ToDateTimeOffset();
            }

            if (valor is string texto &&
                DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fecha))
            {
                return fecha;
            }
        }

        return null;
    }

    private static async Task MarcarAplicadaAsync(string id)
    {
        await AsegurarBaseAsync();
        if (_base is null)
        {
            return;
        }

        await _base.Collection(Coleccion).Document(id).UpdateAsync(new Dictionary<string, object>
        {
            ["Aplicada"] = true,
            ["Actualizado"] = FieldValue.ServerTimestamp
        });
    }

    private static async Task AsegurarBaseAsync()
    {
        if (_base is not null)
        {
            return;
        }

        _base = await ConexionFirebase.AbrirAsync();
    }

    private static void Avisar(string titulo, string detalle)
    {
        try
        {
            if (!NotificationPrefs.AllowLogistica())
            {
                return;
            }

            AvisoWindowsSolicitudes.Preparar(_ui);
            var aviso = new AppNotificationBuilder()
                .AddText(titulo)
                .AddText(detalle)
                .BuildNotification();
            AppNotificationManager.Default.Show(aviso);
        }
        catch
        {
        }
    }

    private static async Task AvisoAsync(XamlRoot raiz, string titulo, string mensaje)
    {
        var dialogo = new ContentDialog
        {
            Title = titulo,
            Content = mensaje,
            CloseButtonText = "Cerrar",
            XamlRoot = raiz
        };
        await dialogo.ShowAsync();
    }

    private static void EnUi(Action accion)
    {
        var cola = _ui ?? App.Instance.MainAppWindow?.DispatcherQueue;
        if (cola is null || cola.HasThreadAccess)
        {
            accion();
            return;
        }

        cola.TryEnqueue(() => accion());
    }

    private static List<string> LeerIds(IDictionary<string, object> datos)
    {
        var ids = new List<string>();
        if (!datos.TryGetValue("Productos", out var raw) || raw is not IEnumerable<object> lista)
        {
            return ids;
        }

        foreach (var item in lista)
        {
            if (item is not IDictionary<string, object> mapa)
            {
                continue;
            }

            var id = Texto(mapa, "Id");
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static string Texto(IDictionary<string, object> datos, params string[] claves)
    {
        foreach (var clave in claves)
        {
            if (datos.TryGetValue(clave, out var valor) && valor is not null)
            {
                return Convert.ToString(valor, CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static int Entero(IDictionary<string, object> datos, string clave)
    {
        if (!datos.TryGetValue(clave, out var valor) || valor is null)
        {
            return 0;
        }

        return valor switch
        {
            int n => n,
            long n => (int)n,
            double n => (int)n,
            _ => int.TryParse(Convert.ToString(valor, CultureInfo.InvariantCulture), out var n) ? n : 0
        };
    }

    private static bool Bool(IDictionary<string, object> datos, string clave)
    {
        if (!datos.TryGetValue(clave, out var valor) || valor is null)
        {
            return false;
        }

        return valor switch
        {
            bool b => b,
            string s => s.Equals("true", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private sealed record CambioBorrado(
        string Id,
        string Estado,
        bool Aplicada,
        string Motivo,
        string RespondidoPor,
        int Cantidad,
        IReadOnlyList<string> Ids);
}
