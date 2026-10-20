using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IdermaFichas.Pages;

public sealed partial class NotificacionesPage : Page, IVistaInventario
{
    private readonly List<string> _estados = ["Todas", "Pendiente", "Aceptada", "Cantidad acordada", "Denegada"];

    public NotificacionesPage()
    {
        InitializeComponent();
        ComboEstado.ItemsSource = _estados;
        ComboEstado.SelectedIndex = 0;
        SolicitudesClinicaNube.Cambiado += Refrescar;
        SolicitudesIncidenciasNube.Cambiado += Refrescar;
        AprobacionBorradoInventario.Cambiado += Refrescar;
        Loaded += (_, _) =>
        {
            SolicitudesClinicaNube.Iniciar(DispatcherQueue);
            SolicitudesIncidenciasNube.Iniciar(DispatcherQueue);
            AprobacionBorradoInventario.Iniciar(DispatcherQueue);
            Refrescar();
        };
        Unloaded += (_, _) =>
        {
            SolicitudesClinicaNube.Cambiado -= Refrescar;
            SolicitudesIncidenciasNube.Cambiado -= Refrescar;
            AprobacionBorradoInventario.Cambiado -= Refrescar;
        };
    }

    public void AplicarCambioInventario() => Refrescar();

    private void Refrescar()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(Refrescar);
            return;
        }

        var filtro = CajaBusqueda.Text?.Trim() ?? string.Empty;
        var estado = ComboEstado.SelectedItem as string ?? "Todas";
        var vista = SolicitudesClinicaNube.Items
            .Where(s => estado == "Todas" || s.EstadoTexto.Equals(estado, StringComparison.OrdinalIgnoreCase))
            .Where(s => string.IsNullOrWhiteSpace(filtro) ||
                        Contiene(s.Folio, filtro) ||
                        Contiene(s.Enfermera, filtro) ||
                        Contiene(s.Detalle, filtro) ||
                        Contiene(s.MotivoDenegacion, filtro))
            .ToList();

        // Forzar redibujado: x:Bind no siempre refresca al mutar el mismo objeto.
        ListaNotificaciones.ItemsSource = null;
        ListaNotificaciones.ItemsSource = vista;
        TextoVacio.Visibility = vista.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var incidencias = SolicitudesIncidenciasNube.Items
            .Where(s => estado == "Todas" || s.EstadoTexto.Equals(estado, StringComparison.OrdinalIgnoreCase))
            .Where(s => string.IsNullOrWhiteSpace(filtro) ||
                        Contiene(s.Folio, filtro) ||
                        Contiene(s.Solicitante, filtro) ||
                        Contiene(s.Nombre, filtro) ||
                        Contiene(s.Detalle, filtro) ||
                        Contiene(s.Motivo, filtro))
            .ToList();
        ListaIncidencias.ItemsSource = null;
        ListaIncidencias.ItemsSource = incidencias;
        TextoVacioIncidencias.Visibility = incidencias.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var autorizaciones = AprobacionBorradoInventario.Items
            .Where(a => CoincideEstado(estado, a))
            .Where(a => string.IsNullOrWhiteSpace(filtro) ||
                        Contiene(a.Folio, filtro) ||
                        Contiene(a.Solicitante, filtro) ||
                        Contiene(a.Detalle, filtro) ||
                        Contiene(a.Respuesta, filtro))
            .ToList();
        ListaAutorizaciones.ItemsSource = null;
        ListaAutorizaciones.ItemsSource = autorizaciones;
        TextoVacioAutorizaciones.Visibility = autorizaciones.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var pendientes = SolicitudesClinicaNube.Pendientes
            + SolicitudesIncidenciasNube.Pendientes
            + AprobacionBorradoInventario.Pendientes;
        var total = SolicitudesClinicaNube.Items.Count
            + SolicitudesIncidenciasNube.Items.Count
            + AprobacionBorradoInventario.Items.Count;
        TextoConteo.Text = pendientes == 0
            ? $"{total} notificaciones · {SolicitudesClinicaNube.Estado}"
            : $"{total} notificaciones · {pendientes} pendientes · {SolicitudesClinicaNube.Estado}";
    }

    private void CajaBusqueda_TextChanged(object sender, TextChangedEventArgs e) => Refrescar();

    private void Filtro_Changed(object sender, SelectionChangedEventArgs e) => Refrescar();

    private async void Aceptar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SolicitudClinica solicitud })
        {
            await ResponderAsync(solicitud);
        }
    }

    private async void Denegar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SolicitudClinica solicitud })
        {
            await ResponderAsync(solicitud);
        }
    }

    private async Task ResponderAsync(SolicitudClinica solicitud)
    {
        var resultado = await DialogoRespuestaSolicitud.MostrarAsync(XamlRoot, solicitud);
        if (resultado is null)
        {
            return;
        }

        try
        {
            if (resultado.Decision == DecisionRespuestaSolicitud.Denegar)
            {
                await SolicitudesClinicaNube.DenegarAsync(solicitud, resultado.Motivo, resultado.Cantidades);
                Refrescar();
                Mostrar("Solicitud denegada.", InfoBarSeverity.Informational);
                return;
            }

            var acordada = resultado.Decision == DecisionRespuestaSolicitud.CantidadAcordada;
            var resumen = await SolicitudesClinicaNube.AceptarAsync(
                solicitud,
                resultado.Cantidades,
                acordada,
                resultado.Motivo);
            Refrescar();
            Mostrar(
                acordada ? "Solicitud aceptada con cantidad acordada. " + resumen : "Solicitud aceptada. " + resumen,
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar(ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void AceptarIncidencia_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SolicitudIncidencia solicitud })
        {
            return;
        }

        var dialogo = new ContentDialog
        {
            Title = "Aceptar reporte",
            Content = $"¿Aceptar {solicitud.Folio}? Después puede agregarlo a una orden de compra.",
            PrimaryButtonText = "Aceptar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await SolicitudesIncidenciasNube.AceptarAsync(solicitud);
            Refrescar();
            Mostrar("Reporte aceptado.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar(ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void DenegarIncidencia_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SolicitudIncidencia solicitud })
        {
            return;
        }

        var motivo = new TextBox { PlaceholderText = "Motivo de la denegación", AcceptsReturn = true, Height = 80 };
        var dialogo = new ContentDialog
        {
            Title = "Denegar reporte",
            Content = motivo,
            PrimaryButtonText = "Denegar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await SolicitudesIncidenciasNube.DenegarAsync(solicitud, motivo.Text ?? string.Empty);
            Refrescar();
            Mostrar("Reporte denegado.", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            Mostrar(ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void AgregarOrden_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SolicitudIncidencia solicitud })
        {
            return;
        }

        var borradores = App.Instance.OrdenesCompra.Listar()
            .Where(o => (o.Documento?.Estado ?? string.Empty).Equals("Borrador", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var opciones = new List<string> { "Nueva orden en borrador" };
        opciones.AddRange(borradores.Select(o =>
        {
            var mes = string.IsNullOrWhiteSpace(o.Documento?.MesAnio) ? "sin mes" : o.Documento!.MesAnio.Trim();
            var productos = o.Documento?.Lineas.Count ?? 0;
            return $"{mes} · {productos} productos · {o.FechaRegistro.ToLocalTime():dd/MM/yyyy}";
        }));
        var combo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = opciones,
            SelectedIndex = 0
        };
        var dialogo = new ContentDialog
        {
            Title = "Agregar a orden de compra",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"{solicitud.Nombre} se suma a la orden elegida. Si ya está, aumenta la cantidad.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    combo
                }
            },
            PrimaryButtonText = "Agregar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var linea = OrdenCompraArmado.ParaIncidencia(solicitud);
        OrdenCompraEdicion edicion;
        if (combo.SelectedIndex > 0 && borradores[combo.SelectedIndex - 1].Documento is { } documento)
        {
            var orden = borradores[combo.SelectedIndex - 1];
            var lineas = documento.Lineas.Select(OrdenCompraArmado.DesdeGuardada).ToList();
            OrdenCompraArmado.Incorporar(lineas, linea);
            edicion = new OrdenCompraEdicion
            {
                Id = orden.Id,
                FechaRegistro = orden.FechaRegistro,
                Documento = orden.Documento,
                Lineas = lineas
            };
        }
        else
        {
            edicion = new OrdenCompraEdicion
            {
                Lineas = [linea]
            };
        }

        try
        {
            await SolicitudesIncidenciasNube.VincularOrdenAsync(
                solicitud.Id,
                edicion.Id,
                edicion.Documento?.Estado ?? "Borrador");
        }
        catch (Exception ex)
        {
            Mostrar(ex.Message, InfoBarSeverity.Error);
            return;
        }

        Frame.Navigate(typeof(OrdenCompraWindow), edicion);
    }

    private async void ExportarClinicaExcel_Click(object sender, RoutedEventArgs e) =>
        await ExportarListaAsync(
            "solicitudes de clínica",
            $"notificaciones-clinica-{DateTime.Now:yyyyMMdd}",
            true,
            ListaNotificaciones.ItemsSource as IEnumerable<SolicitudClinica>,
            SolicitudesExportService.ExportarExcel,
            SolicitudesExportService.ExportarPdf);

    private async void ExportarClinicaPdf_Click(object sender, RoutedEventArgs e) =>
        await ExportarListaAsync(
            "solicitudes de clínica",
            $"notificaciones-clinica-{DateTime.Now:yyyyMMdd}",
            false,
            ListaNotificaciones.ItemsSource as IEnumerable<SolicitudClinica>,
            SolicitudesExportService.ExportarExcel,
            SolicitudesExportService.ExportarPdf);

    private async void ExportarIncidenciasExcel_Click(object sender, RoutedEventArgs e) =>
        await ExportarListaAsync(
            "incidencias",
            $"notificaciones-incidencias-{DateTime.Now:yyyyMMdd}",
            true,
            ListaIncidencias.ItemsSource as IEnumerable<SolicitudIncidencia>,
            SolicitudesExportService.ExportarExcel,
            SolicitudesExportService.ExportarPdf);

    private async void ExportarIncidenciasPdf_Click(object sender, RoutedEventArgs e) =>
        await ExportarListaAsync(
            "incidencias",
            $"notificaciones-incidencias-{DateTime.Now:yyyyMMdd}",
            false,
            ListaIncidencias.ItemsSource as IEnumerable<SolicitudIncidencia>,
            SolicitudesExportService.ExportarExcel,
            SolicitudesExportService.ExportarPdf);

    private async void ExportarAutorizacionesExcel_Click(object sender, RoutedEventArgs e) =>
        await ExportarListaAsync(
            "autorizaciones de base de datos",
            $"notificaciones-autorizaciones-{DateTime.Now:yyyyMMdd}",
            true,
            ListaAutorizaciones.ItemsSource as IEnumerable<AvisoAutorizacion>,
            SolicitudesExportService.ExportarExcel,
            SolicitudesExportService.ExportarPdf);

    private async void ExportarAutorizacionesPdf_Click(object sender, RoutedEventArgs e) =>
        await ExportarListaAsync(
            "autorizaciones de base de datos",
            $"notificaciones-autorizaciones-{DateTime.Now:yyyyMMdd}",
            false,
            ListaAutorizaciones.ItemsSource as IEnumerable<AvisoAutorizacion>,
            SolicitudesExportService.ExportarExcel,
            SolicitudesExportService.ExportarPdf);

    private async Task ExportarListaAsync<T>(
        string nombre,
        string archivo,
        bool excel,
        IEnumerable<T>? origen,
        Action<string, IReadOnlyList<T>> escribirExcel,
        Action<string, IReadOnlyList<T>> escribirPdf)
    {
        var lista = origen?.ToList() ?? [];
        if (lista.Count == 0)
        {
            Mostrar($"No hay {nombre} para exportar.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            var resultado = excel
                ? await ExcelUi.GuardarExcelAsync(archivo, ruta => escribirExcel(ruta, lista))
                : await ExcelUi.GuardarPdfAsync(archivo, ruta => escribirPdf(ruta, lista));
            if (resultado is not null)
            {
                await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
            }
        }
        catch (Exception ex)
        {
            Mostrar("No se pudo exportar: " + ex.Message, InfoBarSeverity.Error);
        }
    }

    private void Mostrar(string mensaje, InfoBarSeverity severidad)
    {
        BarraEstado.Title = severidad == InfoBarSeverity.Error ? "Error" : "Notificaciones";
        BarraEstado.Message = mensaje;
        BarraEstado.Severity = severidad;
        BarraEstado.IsOpen = true;
    }

    private static bool CoincideEstado(string estado, AvisoAutorizacion aviso) => estado switch
    {
        "Todas" => true,
        "Pendiente" => aviso.EstaPendiente,
        "Denegada" => aviso.EstadoTexto.Equals("Denegada", StringComparison.OrdinalIgnoreCase),
        "Aceptada" => aviso.EstadoTexto.Equals("Autorizada", StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    private static bool Contiene(string? texto, string filtro) =>
        !string.IsNullOrWhiteSpace(texto) &&
        texto.Contains(filtro, StringComparison.CurrentCultureIgnoreCase);
}
