using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Models;

namespace IdermaFichas.Pages;

public sealed partial class RhFaltasPage : Page
{
    private bool _justificadas;
    private Falta? _actual;

    public RhFaltasPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _justificadas = e.Parameter is true;
        Titulo.Text = _justificadas ? "Faltas justificadas" : "Faltas";
        Subtitulo.Text = _justificadas
            ? "Ausencias con respaldo (médico, permiso u otro documento)."
            : "Ausencias no justificadas. Use Faltas justificadas cuando haya constancia.";
        BotonNuevo.Content = _justificadas ? "Registrar falta justificada" : "Registrar falta";
        ComboColaborador.ItemsSource = RhUi.ComboColaboradores();
        Refrescar();
    }

    private void Refrescar()
    {
        var id = _actual?.Id;
        Lista.ItemsSource = RhUi.Repo.Datos.Faltas
            .Where(f => f.Justificada == _justificadas)
            .OrderByDescending(f => f.Fecha)
            .Select(f => new RhFila(f.Id, RhUi.Repo.NombreColaborador(f.ColaboradorId), f.FechaTexto, f.Motivo))
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

        var item = RhUi.Repo.Datos.Faltas.FirstOrDefault(f => f.Id == fila.Id);
        if (item is not null)
        {
            Mostrar(item);
        }
    }

    private void Nuevo_Click(object sender, RoutedEventArgs e)
    {
        Lista.SelectedItem = null;
        Mostrar(new Falta { Justificada = _justificadas });
        if (ComboColaborador.Items.Count > 0)
        {
            ComboColaborador.SelectedIndex = 0;
        }
    }

    private void Mostrar(Falta item)
    {
        _actual = item;
        Formulario.Visibility = Visibility.Visible;
        ComboColaborador.SelectedItem = RhUi.Repo.BuscarColaborador(item.ColaboradorId);
        Fecha.Date = item.Fecha;
        CajaMotivo.Text = item.Motivo;
        CajaDocumento.Text = item.Documento;
        CajaDocumento.Visibility = _justificadas ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (_actual is null || ComboColaborador.SelectedItem is not Colaborador colaborador)
        {
            return;
        }

        _actual.ColaboradorId = colaborador.Id;
        _actual.Fecha = Fecha.Date;
        _actual.Motivo = CajaMotivo.Text.Trim();
        _actual.Documento = CajaDocumento.Text.Trim();
        _actual.Justificada = _justificadas;
        RhUi.Repo.GuardarFalta(_actual);
        Refrescar();
    }

    private void Borrar_Click(object sender, RoutedEventArgs e)
    {
        if (_actual is null)
        {
            return;
        }

        RhUi.Repo.BorrarFalta(_actual.Id);
        _actual = null;
        Formulario.Visibility = Visibility.Collapsed;
        Refrescar();
    }
}
