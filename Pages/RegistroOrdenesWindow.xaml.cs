using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Pages;

public sealed partial class RegistroOrdenesWindow : Page
{
    private bool _exportando;

    public RegistroOrdenesWindow()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        App.Instance.OrdenesCompra.Cambio -= AlCambiar;
        App.Instance.OrdenesCompra.Cambio += AlCambiar;
        Refrescar();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e) =>
        App.Instance.OrdenesCompra.Cambio -= AlCambiar;

    private void AlCambiar(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(Refrescar);

    private void Volver_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    public void Refrescar()
    {
        var items = App.Instance.OrdenesCompra.Listar().Select(o => new OrdenRegistradaItem(o)).ToList();
        Lista.ItemsSource = items;
        var hay = items.Count > 0;
        Lista.Visibility = hay ? Visibility.Visible : Visibility.Collapsed;
        TextoVacio.Visibility = hay ? Visibility.Collapsed : Visibility.Visible;
        Subtitulo.Text = hay
            ? $"{items.Count} orden(es) guardada(s). Descargue de nuevo el PDF o el Excel."
            : "Las órdenes exportadas quedan guardadas aquí. Puede volver a descargarlas en PDF o Excel.";
    }

    private async void DescargarPdf_Click(object sender, RoutedEventArgs e) =>
        await DescargarAsync(sender, pdf: true);

    private async void DescargarExcel_Click(object sender, RoutedEventArgs e) =>
        await DescargarAsync(sender, pdf: false);

    private async void Quitar_Click(object sender, RoutedEventArgs e)
    {
        if (!TryId(sender, out var id) || App.Instance.OrdenesCompra.Obtener(id) is not { } orden)
        {
            return;
        }

        var mes = string.IsNullOrWhiteSpace(orden.Documento.MesAnio) ? "esta orden" : $"la orden de {orden.Documento.MesAnio}";
        var dialogo = new ContentDialog
        {
            Title = "Quitar orden",
            Content = $"¿Quitar {mes} del registro? El archivo que ya haya guardado en su equipo no se borra.",
            PrimaryButtonText = "Quitar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        App.Instance.OrdenesCompra.Eliminar(id);
        Refrescar();
    }

    private async Task DescargarAsync(object sender, bool pdf)
    {
        if (_exportando || !TryId(sender, out var id) || App.Instance.OrdenesCompra.Obtener(id) is not { } orden)
        {
            return;
        }

        _exportando = true;
        try
        {
            var resultado = await ExcelUi.ExportarOrdenCompraAsync(orden.Documento, pdf);
            if (resultado is null)
            {
                return;
            }

            BarraAviso.Severity = InfoBarSeverity.Success;
            BarraAviso.Message = pdf ? "Se descargó el PDF de la orden." : "Se descargó el Excel de la orden.";
            BarraAviso.IsOpen = true;
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            BarraAviso.Severity = InfoBarSeverity.Error;
            BarraAviso.Message = $"No se pudo descargar: {ex.Message}";
            BarraAviso.IsOpen = true;
        }
        finally
        {
            _exportando = false;
        }
    }

    private static bool TryId(object sender, out Guid id)
    {
        id = Guid.Empty;
        return sender is Button { Tag: string texto } && Guid.TryParse(texto, out id);
    }
}

public sealed class OrdenRegistradaItem
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    public OrdenRegistradaItem(OrdenCompraRegistrada orden)
    {
        IdTexto = orden.Id.ToString();
        var doc = orden.Documento ?? new OrdenCompraDocumento();
        var mes = string.IsNullOrWhiteSpace(doc.MesAnio) ? "sin mes" : doc.MesAnio.Trim();
        Titulo = $"Orden {mes}";
        var productos = doc.Lineas.Count == 1 ? "1 producto" : $"{doc.Lineas.Count} productos";
        Resumen = $"{orden.FechaRegistro.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Cultura)} · {productos} · {doc.Estado} · {Total(doc)}";
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(doc.ElaboradoPor))
        {
            partes.Add($"Elaboró {doc.ElaboradoPor.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(doc.RevisadoPor))
        {
            partes.Add($"Revisó {doc.RevisadoPor.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(doc.PeriodoCobertura))
        {
            partes.Add(doc.PeriodoCobertura.Trim());
        }

        Extra = partes.Count == 0 ? "Sin datos de elaboración" : string.Join(" · ", partes);
    }

    public string IdTexto { get; }
    public string Titulo { get; }
    public string Resumen { get; }
    public string Extra { get; }

    private static string Total(OrdenCompraDocumento doc)
    {
        var grupos = doc.Lineas
            .Where(l => l.TienePrecio)
            .GroupBy(l => MonedaPrecio.Normalizar(l.Moneda))
            .Select(g => MonedaPrecio.Formato(g.Sum(l => l.TotalConIgv), g.Key))
            .ToList();
        return grupos.Count == 0 ? "sin total" : string.Join(" · ", grupos);
    }
}
