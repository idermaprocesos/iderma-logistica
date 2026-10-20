using System.Globalization;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IdermaFichas.Pages;

public sealed partial class MermaPage : Page
{
    public MermaPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            FechaDesde.Date ??= DateTimeOffset.Now;
            FechaHasta.Date ??= DateTimeOffset.Now;
            ModulosNube.DatosCambiados -= AlLlegarNube;
            ModulosNube.DatosCambiados += AlLlegarNube;
            Cargar();
        };
        Unloaded += (_, _) => ModulosNube.DatosCambiados -= AlLlegarNube;
    }

    private void AlLlegarNube() => DispatcherQueue.TryEnqueue(Cargar);

    private void Periodo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var personalizado = Periodo() == "Personalizado";
        FechaDesde.Visibility = personalizado ? Visibility.Visible : Visibility.Collapsed;
        FechaHasta.Visibility = personalizado ? Visibility.Visible : Visibility.Collapsed;
        Cargar();
    }

    private void FechaCustom_Changed(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (IsLoaded && Periodo() == "Personalizado")
        {
            Cargar();
        }
    }

    private async void Imprimir_Click(object sender, RoutedEventArgs e)
    {
        var rango = RangoActual();
        var lineas = Filtrar(rango);
        if (lineas.Count == 0)
        {
            Aviso.Severity = InfoBarSeverity.Warning;
            Aviso.Title = "Sin movimientos";
            Aviso.Message = $"No hay merma en {rango.Titulo.ToLowerInvariant()}.";
            Aviso.IsOpen = true;
            return;
        }

        var nombre = $"Reporte de merma {rango.Titulo}".Replace('/', '-');
        var resultado = await ExcelUi.GuardarPdfAsync(nombre, ruta => MermaReporte.ExportarPdf(ruta, rango.Titulo, lineas));
        if (resultado is not null)
        {
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
    }

    private void Cargar()
    {
        if (ComboPeriodo is null)
        {
            return;
        }

        var rango = RangoActual();
        var items = Filtrar(rango).Select(m => new MermaItem(m)).ToList();
        Lista.ItemsSource = items;
        Vacio.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Vacio.Text = $"No hay merma en {rango.Titulo.ToLowerInvariant()}.";
        TextoRango.Text = rango.Titulo;
    }

    private List<MermaRegistrada> Filtrar(RangoMerma rango) =>
        App.Instance.Mermas.Listar().Where(m => rango.Contiene(m.Fecha)).ToList();

    private RangoMerma RangoActual() =>
        RangoMerma.De(Periodo(), FechaDesde?.Date, FechaHasta?.Date);

    private string Periodo() =>
        (ComboPeriodo.SelectedItem as ComboBoxItem)?.Content as string ?? "Hoy";
}

public sealed class MermaItem
{
    public MermaItem(MermaRegistrada merma)
    {
        var cultura = CultureInfo.GetCultureInfo("es-PE");
        Titulo = $"{merma.Codigo}  {merma.Nombre} · {merma.Cantidad.ToString("0.##", cultura)} {merma.Unidad}".Trim();
        var caduca = merma.FechaCaducidad?.ToLocalTime().ToString("dd/MM/yyyy") ?? "Sin caducidad";
        var lote = string.IsNullOrWhiteSpace(merma.Lote) ? "Sin lote" : merma.Lote;
        Lote = $"{lote} · {caduca}";
        Motivo = string.IsNullOrWhiteSpace(merma.Detalle) ? merma.Motivo : $"{merma.Motivo} · {merma.Detalle}";
        Cuando = merma.Fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    }

    public string Titulo { get; }
    public string Lote { get; }
    public string Motivo { get; }
    public string Cuando { get; }
}
