using IdermaFichas.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IdermaFichas.Pages;

public sealed partial class RegistroServicioPage : Page
{
    public RegistroServicioPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Cargar();
    }

    private void Cargar()
    {
        var items = App.Instance.OrdenesServicio.Listar().Select(o => new OrdenServicioItem(o)).ToList();
        Lista.ItemsSource = items;
        Vacio.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Volver_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private async void Pdf_Click(object sender, RoutedEventArgs e) => await Exportar(sender, pdf: true);

    private async void Excel_Click(object sender, RoutedEventArgs e) => await Exportar(sender, pdf: false);

    private void Quitar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button boton && boton.Tag is string texto && Guid.TryParse(texto, out var id))
        {
            App.Instance.OrdenesServicio.Eliminar(id);
            Cargar();
        }
    }

    private static async Task Exportar(object sender, bool pdf)
    {
        if (sender is not Button boton || boton.Tag is not string texto || !Guid.TryParse(texto, out var id))
        {
            return;
        }

        var orden = App.Instance.OrdenesServicio.Obtener(id);
        if (orden is null)
        {
            return;
        }

        var resultado = await ExcelUi.ExportarOrdenServicioAsync(orden.Documento, pdf);
        if (resultado is not null && sender is FrameworkElement elemento)
        {
            await ExcelUi.OfrecerAbrirAsync(elemento.XamlRoot, resultado);
        }
    }
}
