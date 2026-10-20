using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas.Pages;

public sealed partial class BorradoresOrdenPage : Page
{
    public BorradoresOrdenPage()
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

    private void Refrescar()
    {
        var items = App.Instance.OrdenesCompra.Listar()
            .Where(o => (o.Documento?.Estado ?? string.Empty).Equals("Borrador", StringComparison.OrdinalIgnoreCase))
            .Select(o => new OrdenRegistradaItem(o))
            .ToList();
        Lista.ItemsSource = items;
        var hay = items.Count > 0;
        Lista.Visibility = hay ? Visibility.Visible : Visibility.Collapsed;
        TextoVacio.Visibility = hay ? Visibility.Collapsed : Visibility.Visible;
        Subtitulo.Text = hay
            ? $"{items.Count} borrador(es). Ábralos para sumar productos del inventario."
            : "No hay órdenes en borrador. Al guardar una orden con estado Borrador aparece aquí.";
    }

    private void Editar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string texto } || !Guid.TryParse(texto, out var id))
        {
            return;
        }

        var orden = App.Instance.OrdenesCompra.Obtener(id);
        if (orden?.Documento is null)
        {
            return;
        }

        Frame.Navigate(typeof(OrdenCompraWindow), new OrdenCompraEdicion
        {
            Id = orden.Id,
            FechaRegistro = orden.FechaRegistro,
            Documento = orden.Documento,
            Lineas = orden.Documento.Lineas.Select(OrdenCompraArmado.DesdeGuardada).ToList()
        });
    }
}
