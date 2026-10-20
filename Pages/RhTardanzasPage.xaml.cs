using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Models;

namespace IdermaFichas.Pages;

public sealed partial class RhTardanzasPage : Page
{
    private Tardanza? _actual;

    public RhTardanzasPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ComboColaborador.ItemsSource = RhUi.ComboColaboradores();
        Refrescar();
    }

    private void Refrescar()
    {
        var id = _actual?.Id;
        Lista.ItemsSource = RhUi.Repo.Datos.Tardanzas
            .OrderByDescending(t => t.Fecha)
            .Select(t => new RhFila(t.Id, RhUi.Repo.NombreColaborador(t.ColaboradorId), t.FechaTexto, t.MinutosTexto + (string.IsNullOrWhiteSpace(t.Motivo) ? "" : " · " + t.Motivo)))
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

        var item = RhUi.Repo.Datos.Tardanzas.FirstOrDefault(t => t.Id == fila.Id);
        if (item is not null)
        {
            Mostrar(item);
        }
    }

    private void Nuevo_Click(object sender, RoutedEventArgs e)
    {
        Lista.SelectedItem = null;
        Mostrar(new Tardanza { Minutos = 10 });
        if (ComboColaborador.Items.Count > 0)
        {
            ComboColaborador.SelectedIndex = 0;
        }
    }

    private void Mostrar(Tardanza item)
    {
        _actual = item;
        Formulario.Visibility = Visibility.Visible;
        ComboColaborador.SelectedItem = RhUi.Repo.BuscarColaborador(item.ColaboradorId);
        Fecha.Date = item.Fecha;
        Minutos.Value = item.Minutos;
        CajaMotivo.Text = item.Motivo;
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (_actual is null || ComboColaborador.SelectedItem is not Colaborador colaborador)
        {
            return;
        }

        _actual.ColaboradorId = colaborador.Id;
        _actual.Fecha = Fecha.Date;
        _actual.Minutos = (int)Math.Max(1, Minutos.Value);
        _actual.Motivo = CajaMotivo.Text.Trim();
        RhUi.Repo.GuardarTardanza(_actual);
        Refrescar();
    }

    private void Borrar_Click(object sender, RoutedEventArgs e)
    {
        if (_actual is null)
        {
            return;
        }

        RhUi.Repo.BorrarTardanza(_actual.Id);
        _actual = null;
        Formulario.Visibility = Visibility.Collapsed;
        Refrescar();
    }
}

public sealed class RhFila
{
    public RhFila(Guid id, string nombre, string detalle, string extra)
    {
        Id = id;
        Nombre = nombre;
        Detalle = detalle;
        Extra = extra;
    }

    public Guid Id { get; }
    public string Nombre { get; }
    public string Detalle { get; }
    public string Extra { get; }
}
