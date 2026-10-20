using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Models;

namespace IdermaFichas.Pages;

public sealed partial class RhHorariosPage : Page
{
    private HorarioColaborador? _horario;

    public RhHorariosPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ComboColaborador.ItemsSource = RhUi.ComboColaboradores();
        if (ComboColaborador.Items.Count > 0)
        {
            ComboColaborador.SelectedIndex = 0;
        }
    }

    private void ComboColaborador_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboColaborador.SelectedItem is not Colaborador colaborador)
        {
            return;
        }

        _horario = RhUi.Repo.HorarioDe(colaborador.Id);
        ListaDias.ItemsSource = _horario.Dias;
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (_horario is null)
        {
            return;
        }

        RhUi.Repo.Guardar();
        Barra.Severity = InfoBarSeverity.Success;
        Barra.Title = "Horario guardado";
        Barra.Message = "La jornada semanal quedó registrada.";
        Barra.IsOpen = true;
    }
}
