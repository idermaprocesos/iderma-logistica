using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Models;

namespace IdermaFichas.Pages;

public sealed partial class RhRiesgoPage : Page
{
    private RiesgoColaborador? _actual;

    public RhRiesgoPage()
    {
        InitializeComponent();
        ComboColaborador.SelectionChanged += ComboColaborador_SelectionChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ComboColaborador.ItemsSource = RhUi.ComboColaboradores();
        ComboNivel.ItemsSource = new[] { "Bajo", "Medio", "Alto" };
        Refrescar();
    }

    private void ComboColaborador_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ActualizarIndicadores();

    private void ActualizarIndicadores()
    {
        if (ComboColaborador.SelectedItem is not Colaborador colaborador)
        {
            ResumenIndicadores.Text = "";
            return;
        }

        var tardanzas = RhUi.Repo.TardanzasDelMes(colaborador.Id);
        var faltas = RhUi.Repo.FaltasDelMes(colaborador.Id, false);
        var justificadas = RhUi.Repo.FaltasDelMes(colaborador.Id, true);
        ResumenIndicadores.Text =
            $"Este mes: {tardanzas} tardanza(s), {faltas} falta(s) y {justificadas} falta(s) justificada(s).";
    }

    private void Refrescar()
    {
        var id = _actual?.Id;
        Lista.ItemsSource = RhUi.Repo.Datos.Riesgos
            .OrderByDescending(r => r.Fecha)
            .Select(r => new RhFila(
                r.Id,
                RhUi.Repo.NombreColaborador(r.ColaboradorId),
                $"{r.NivelTexto} · {r.FechaTexto}",
                r.Motivo))
            .ToList();
        if (id is Guid elegido)
        {
            Lista.SelectedItem = ((IEnumerable<RhFila>)Lista.ItemsSource).FirstOrDefault(f => f.Id == elegido);
        }
    }

    private void Lista_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Lista.SelectedItem is not RhFila fila)
        {
            return;
        }

        var item = RhUi.Repo.Datos.Riesgos.FirstOrDefault(r => r.Id == fila.Id);
        if (item is not null)
        {
            Mostrar(item);
        }
    }

    private void Nuevo_Click(object sender, RoutedEventArgs e)
    {
        Lista.SelectedItem = null;
        Mostrar(new RiesgoColaborador());
        if (ComboColaborador.Items.Count > 0)
        {
            ComboColaborador.SelectedIndex = 0;
        }
    }

    private void Mostrar(RiesgoColaborador item)
    {
        _actual = item;
        Formulario.Visibility = Visibility.Visible;
        ComboColaborador.SelectedItem = RhUi.Repo.BuscarColaborador(item.ColaboradorId);
        Fecha.Date = item.Fecha;
        ComboNivel.SelectedIndex = item.Nivel switch
        {
            NivelRiesgo.Bajo => 0,
            NivelRiesgo.Alto => 2,
            _ => 1
        };
        CajaMotivo.Text = item.Motivo;
        CajaAcciones.Text = item.Acciones;
        ActualizarIndicadores();
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (_actual is null || ComboColaborador.SelectedItem is not Colaborador colaborador)
        {
            return;
        }

        _actual.ColaboradorId = colaborador.Id;
        _actual.Fecha = Fecha.Date;
        _actual.Nivel = ComboNivel.SelectedIndex switch
        {
            0 => NivelRiesgo.Bajo,
            2 => NivelRiesgo.Alto,
            _ => NivelRiesgo.Medio
        };
        _actual.Motivo = CajaMotivo.Text.Trim();
        _actual.Acciones = CajaAcciones.Text.Trim();
        RhUi.Repo.GuardarRiesgo(_actual);
        Refrescar();
    }

    private void Borrar_Click(object sender, RoutedEventArgs e)
    {
        if (_actual is null)
        {
            return;
        }

        RhUi.Repo.BorrarRiesgo(_actual.Id);
        _actual = null;
        Formulario.Visibility = Visibility.Collapsed;
        Refrescar();
        ActualizarIndicadores();
    }
}
