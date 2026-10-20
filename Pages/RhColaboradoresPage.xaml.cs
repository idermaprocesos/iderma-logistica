using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Models;

namespace IdermaFichas.Pages;

public sealed partial class RhColaboradoresPage : Page
{
    private Colaborador? _actual;

    public RhColaboradoresPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => Refrescar();

    private void Refrescar()
    {
        var id = _actual?.Id;
        Lista.ItemsSource = RhUi.Repo.Datos.Colaboradores.OrderBy(c => c.Nombre).ToList();
        if (id is Guid elegido)
        {
            Lista.SelectedItem = RhUi.Repo.Datos.Colaboradores.FirstOrDefault(c => c.Id == elegido);
        }
    }

    private void Lista_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Lista.SelectedItem is Colaborador colaborador)
        {
            Mostrar(colaborador);
        }
    }

    private void Nuevo_Click(object sender, RoutedEventArgs e)
    {
        Mostrar(new Colaborador());
        Lista.SelectedItem = null;
    }

    private void Mostrar(Colaborador colaborador)
    {
        _actual = colaborador;
        Formulario.Visibility = Visibility.Visible;
        CajaNombre.Text = colaborador.Nombre;
        CajaPuesto.Text = colaborador.Puesto;
        CajaArea.Text = colaborador.Area;
        CajaTelefono.Text = colaborador.Telefono;
        FechaIngreso.Date = colaborador.FechaIngreso;
        SwitchActivo.IsOn = colaborador.Activo;
        CajaNotas.Text = colaborador.Notas;
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (_actual is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(CajaNombre.Text))
        {
            Barra.Severity = InfoBarSeverity.Error;
            Barra.Message = "El nombre es obligatorio.";
            Barra.IsOpen = true;
            return;
        }

        _actual.Nombre = CajaNombre.Text.Trim();
        _actual.Puesto = CajaPuesto.Text.Trim();
        _actual.Area = CajaArea.Text.Trim();
        _actual.Telefono = CajaTelefono.Text.Trim();
        _actual.FechaIngreso = FechaIngreso.Date;
        _actual.Activo = SwitchActivo.IsOn;
        _actual.Notas = CajaNotas.Text.Trim();
        RhUi.Repo.GuardarColaborador(_actual);
        Barra.Severity = InfoBarSeverity.Success;
        Barra.Message = "Colaborador guardado.";
        Barra.IsOpen = true;
        Refrescar();
    }

    private async void Borrar_Click(object sender, RoutedEventArgs e)
    {
        if (_actual is null || !RhUi.Repo.Datos.Colaboradores.Any(c => c.Id == _actual.Id))
        {
            return;
        }

        var dialogo = new ContentDialog
        {
            Title = "Eliminar colaborador",
            Content = "Se borrarán también sus horarios, tardanzas, faltas y riesgos.",
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        RhUi.Repo.BorrarColaborador(_actual.Id);
        _actual = null;
        Formulario.Visibility = Visibility.Collapsed;
        Refrescar();
    }
}
