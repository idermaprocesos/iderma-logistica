using System.Runtime.InteropServices;
using Iderma.Core.Services;
using IdermaFichas.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using WinRT.Interop;

namespace IdermaFichas.Services;

public static class AvisoWindowsSolicitudes
{
    private const string Accion = "action";
    private const string IdSolicitud = "id";
    private static readonly object Puerta = new();
    private static bool _registrado;
    private static DispatcherQueue? _ui;
    private static (string Accion, string Id)? _pendiente;
    private static bool _traerAlFrente;
    private static string? _ultimaClave;
    private static DateTime _ultimaUtc;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint hWnd);

    public static void Recibir(AppActivationArguments args)
    {
        if (args.Kind == ExtendedActivationKind.AppNotification
            && args.Data is AppNotificationActivatedEventArgs datos)
        {
            var accion = datos.Arguments.TryGetValue(Accion, out var a) ? a : "ver";
            var id = datos.Arguments.TryGetValue(IdSolicitud, out var i) ? i : string.Empty;
            lock (Puerta)
            {
                _pendiente = (accion, id);
            }
        }
        else
        {
            lock (Puerta)
            {
                _traerAlFrente = true;
            }
        }

        AplicarPendiente();
    }

    public static void Preparar(DispatcherQueue? ui = null)
    {
        if (ui is not null)
        {
            _ui = ui;
        }

        AplicarPendiente();

        if (_registrado)
        {
            return;
        }

        try
        {
            AppNotificationManager.Default.NotificationInvoked += AlInvocar;
            AppNotificationManager.Default.Register();
            _registrado = true;
        }
        catch
        {
            _registrado = false;
        }
    }

    public static void Mostrar(SolicitudClinica solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (!NotificationPrefs.AllowLogistica())
        {
            return;
        }

        try
        {
            Preparar();
            var titulo = solicitud.Urgente
                ? "Solicitud urgente de clínica"
                : "Solicitud nueva de clínica";
            var detalle = $"{solicitud.Folio} · {solicitud.Enfermera} · {solicitud.Resumen}";
            var aviso = new AppNotificationBuilder()
                .AddText(titulo)
                .AddText(detalle)
                .AddText(solicitud.Detalle)
                .AddArgument(Accion, "ver")
                .AddArgument(IdSolicitud, solicitud.Id)
                .AddButton(new AppNotificationButton("Aceptar")
                    .AddArgument(Accion, "aceptar")
                    .AddArgument(IdSolicitud, solicitud.Id))
                .AddButton(new AppNotificationButton("Denegar")
                    .AddArgument(Accion, "denegar")
                    .AddArgument(IdSolicitud, solicitud.Id))
                .SetAudioEvent(solicitud.Urgente
                    ? AppNotificationSoundEvent.Reminder
                    : AppNotificationSoundEvent.Default)
                .BuildNotification();
            AppNotificationManager.Default.Show(aviso);
        }
        catch
        {
        }
    }

    private static void AlInvocar(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        var accion = args.Arguments.TryGetValue(Accion, out var a) ? a : "ver";
        var id = args.Arguments.TryGetValue(IdSolicitud, out var i) ? i : string.Empty;
        EnUi(() => _ = ProcesarAsync(accion, id));
    }

    private static void AplicarPendiente()
    {
        var cola = ObtenerCola();
        if (cola is null)
        {
            return;
        }

        (string Accion, string Id)? pendiente;
        bool traer;
        lock (Puerta)
        {
            pendiente = _pendiente;
            _pendiente = null;
            traer = _traerAlFrente;
            _traerAlFrente = false;
        }

        void trabajo()
        {
            if (pendiente is { } pedido)
            {
                _ = ProcesarAsync(pedido.Accion, pedido.Id);
                return;
            }

            if (traer)
            {
                TraerAlFrente();
            }
        }

        if (cola.HasThreadAccess)
        {
            trabajo();
            return;
        }

        if (!cola.TryEnqueue(trabajo))
        {
            lock (Puerta)
            {
                if (pendiente is { } pedido)
                {
                    _pendiente = pedido;
                }

                if (traer)
                {
                    _traerAlFrente = true;
                }
            }
        }
    }

    private static async Task ProcesarAsync(string accion, string id)
    {
        var clave = accion + "|" + id;
        var ahora = DateTime.UtcNow;
        if (clave == _ultimaClave && (ahora - _ultimaUtc).TotalSeconds < 2)
        {
            TraerAlFrente();
            return;
        }

        _ultimaClave = clave;
        _ultimaUtc = ahora;

        var ventana = App.Instance.MainAppWindow;
        TraerAlFrente(ventana);

        var solicitud = SolicitudesClinicaNube.Items
            .FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        // Cualquier clic en el toast abre Notificaciones; aceptar/denegar actúa ahí mismo.
        ventana?.IrANotificaciones();

        if (accion is "aceptar" or "denegar" && solicitud is { EstaPendiente: true })
        {
            await ResponderDesdeAvisoAsync(solicitud);
        }
    }

    private static async Task ResponderDesdeAvisoAsync(SolicitudClinica solicitud)
    {
        var raiz = App.Instance.MainAppWindow?.Content?.XamlRoot;
        if (raiz is null)
        {
            App.Instance.MainAppWindow?.IrANotificaciones();
            return;
        }

        var resultado = await DialogoRespuestaSolicitud.MostrarAsync(raiz, solicitud);
        if (resultado is null)
        {
            return;
        }

        try
        {
            if (resultado.Decision == DecisionRespuestaSolicitud.Denegar)
            {
                await SolicitudesClinicaNube.DenegarAsync(solicitud, resultado.Motivo, resultado.Cantidades);
                return;
            }

            await SolicitudesClinicaNube.AceptarAsync(
                solicitud,
                resultado.Cantidades,
                resultado.Decision == DecisionRespuestaSolicitud.CantidadAcordada,
                resultado.Motivo);
        }
        catch (Exception ex)
        {
            await MostrarDialogoAsync("No se pudo responder", ex.Message);
        }
    }

    private static async Task MostrarDialogoAsync(string titulo, string mensaje)
    {
        var raiz = App.Instance.MainAppWindow?.Content?.XamlRoot;
        if (raiz is null)
        {
            return;
        }

        var dialogo = new ContentDialog
        {
            Title = titulo,
            Content = mensaje,
            CloseButtonText = "Cerrar",
            XamlRoot = raiz
        };
        await dialogo.ShowAsync();
    }

    private static void TraerAlFrente(Window? ventana = null)
    {
        ventana ??= App.Instance.MainAppWindow;
        if (ventana is null)
        {
            return;
        }

        var hwnd = WindowNative.GetWindowHandle(ventana);
        if (hwnd != 0)
        {
            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, 9);
            }

            SetForegroundWindow(hwnd);
        }

        ventana.Activate();
    }

    private static DispatcherQueue? ObtenerCola()
    {
        if (_ui is not null)
        {
            return _ui;
        }

        try
        {
            var cola = App.Instance.MainAppWindow?.DispatcherQueue;
            if (cola is not null)
            {
                return cola;
            }
        }
        catch
        {
        }

        return DispatcherQueue.GetForCurrentThread();
    }

    private static void EnUi(Action accion)
    {
        var cola = ObtenerCola();
        if (cola is null || cola.HasThreadAccess)
        {
            accion();
            return;
        }

        cola.TryEnqueue(() => accion());
    }
}
