using IdermaFichas.Models;
using IdermaFichas.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IdermaFichas.Pages;

public sealed partial class OrdenServicioPage : Page
{
    private readonly HashSet<Guid> _marcados = [];
    private List<ProveedorServicioItem> _visibles = [];

    public OrdenServicioPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Refrescar();
    }

    private void CajaBusqueda_TextChanged(object sender, TextChangedEventArgs e) => Refrescar();

    private void Filtro_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            Refrescar();
        }
    }

    private void Refrescar()
    {
        var texto = (CajaBusqueda.Text ?? string.Empty).Trim();
        var filtro = (FiltroContrato.SelectedItem as ComboBoxItem)?.Content as string ?? "Todos";
        _visibles = App.Instance.Proveedores.Listar()
            .Where(p => Coincide(p, texto, filtro))
            .Select(p => new ProveedorServicioItem(p, _marcados.Contains(p.Id)))
            .ToList();
        Lista.ItemsSource = _visibles;
        Resumen.Text = _marcados.Count == 0
            ? $"{_visibles.Count} proveedores"
            : $"{_marcados.Count} marcado(s) · {_visibles.Count} en la lista";
    }

    private static bool Coincide(ProveedorRegistro proveedor, string texto, string filtro)
    {
        var conContrato = proveedor.AcuerdoContrato && proveedor.CantidadContrato > 0;
        if (filtro == "Con contrato" && !conContrato)
        {
            return false;
        }

        if (filtro == "Sin contrato" && conContrato)
        {
            return false;
        }

        if (texto.Length == 0)
        {
            return true;
        }

        var bolsa = $"{proveedor.Nombre} {proveedor.Ruc} {proveedor.RazonSocial} {proveedor.Contacto}";
        return bolsa.Contains(texto, StringComparison.OrdinalIgnoreCase);
    }

    private void Marca_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox caja || caja.Tag is not Guid id)
        {
            return;
        }

        if (caja.IsChecked == true)
        {
            _marcados.Add(id);
        }
        else
        {
            _marcados.Remove(id);
        }

        Resumen.Text = $"{_marcados.Count} marcado(s) · {_visibles.Count} en la lista";
    }

    private void Generar_Click(object sender, RoutedEventArgs e)
    {
        var lineas = _marcados
            .Select(App.Instance.Proveedores.Obtener)
            .OfType<ProveedorRegistro>()
            .Select(p => OrdenesServicioRepository.DesdeProveedor(p))
            .ToList();
        if (lineas.Count == 0)
        {
            Aviso.Severity = InfoBarSeverity.Warning;
            Aviso.Title = "Seleccione proveedores";
            Aviso.Message = "Marque al menos un contrato para ampliarlo.";
            Aviso.IsOpen = true;
            return;
        }

        Frame.Navigate(typeof(OrdenServicioWindow), new OrdenServicioEdicion { Lineas = lineas });
    }

    private void Borradores_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(BorradoresServicioPage));

    private void Anteriores_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(RegistroServicioPage));
}

public sealed class ProveedorServicioItem
{
    public ProveedorServicioItem(ProveedorRegistro proveedor, bool marcado)
    {
        Id = proveedor.Id;
        Nombre = string.IsNullOrWhiteSpace(proveedor.Nombre) ? "Sin nombre" : proveedor.Nombre;
        Empresa = proveedor.TextoEmpresa;
        Ruc = string.IsNullOrWhiteSpace(proveedor.Ruc) ? "—" : proveedor.Ruc;
        Contrato = proveedor.TextoGestionCompra;
        Marcado = marcado;
    }

    public Guid Id { get; }
    public string Nombre { get; }
    public string Empresa { get; }
    public string Ruc { get; }
    public string Contrato { get; }
    public bool Marcado { get; set; }
}
