using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Models;
using Microsoft.Web.WebView2.Core;

namespace IdermaFichas.Pages;

public sealed partial class DashboardPage : Page
{
    private string? _url;
    private bool _listo;

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += DashboardPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not DashboardNavegacion dashboard)
        {
            return;
        }

        Titulo.Text = dashboard.Titulo;
        _url = dashboard.Url;
        Carga.Visibility = Visibility.Visible;
        CargarSiListo();
    }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await Visor.EnsureCoreWebView2Async();
            Visor.CoreWebView2.NewWindowRequested += ConservarEnMismaVista;
            _listo = true;
            CargarSiListo();
        }
        catch (Exception)
        {
            Carga.Visibility = Visibility.Collapsed;
            Titulo.Text = (Titulo.Text ?? "Dashboard") + " (no se pudo cargar el visor web)";
        }
    }

    private void CargarSiListo()
    {
        if (!_listo || string.IsNullOrWhiteSpace(_url))
        {
            return;
        }

        Visor.Source = new Uri(_url);
    }

    private void ConservarEnMismaVista(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
        sender.Navigate(args.Uri);
    }

    private void Visor_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        Carga.Visibility = Visibility.Collapsed;
    }
}
