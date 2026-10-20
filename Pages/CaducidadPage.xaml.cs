using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas.Pages;

public sealed partial class CaducidadPage : Page, IVistaInventario
{
    private static readonly OpcionHorizonte[] Horizontes =
    [
        OpcionHorizonte.Dias(7),
        OpcionHorizonte.Dias(15),
        OpcionHorizonte.Dias(30),
        OpcionHorizonte.Dias(60),
        OpcionHorizonte.Dias(90),
        OpcionHorizonte.Meses(1),
        OpcionHorizonte.Meses(3),
        OpcionHorizonte.Meses(6),
        OpcionHorizonte.Meses(9),
        OpcionHorizonte.Meses(12)
    ];

    private const int IndicePorDefecto = 2;
    private Guid? _fichaNavegada;
    private bool _enfocarProximos;
    private bool _ajustandoHorizonte;

    public CaducidadPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _fichaNavegada = e.Parameter switch
        {
            CaducidadNavegacion nav => nav.FichaId,
            Guid id => id,
            _ => null
        };
        _enfocarProximos = e.Parameter is CaducidadNavegacion { Proximos: true };
        ComboHorizonte.ItemsSource = Horizontes.Select(h => h.Etiqueta).ToArray();
        _ajustandoHorizonte = true;
        ComboHorizonte.SelectedIndex = IndiceHorizonte(_fichaNavegada);
        _ajustandoHorizonte = false;
        Refrescar();
    }

    private void ComboHorizonte_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ajustandoHorizonte)
        {
            return;
        }

        Refrescar();
    }

    public void AplicarCambioInventario()
    {
        Refrescar();
        AnimacionLista.Revelar(ListaVencidos);
        AnimacionLista.Revelar(ListaProximos);
    }

    private void Refrescar()
    {
        var horizonte = HorizonteActual();
        var repo = App.Instance.Repositorio;
        var vencidos = repo.PartidasVencidas().ToList();
        var proximos = repo.PartidasProximasHasta(horizonte.Limite).ToList();

        ListaVencidos.ItemsSource = vencidos;
        ListaProximos.ItemsSource = proximos;
        ConteoVencidos.Text = vencidos.Count.ToString();
        ConteoProximos.Text = proximos.Count.ToString();
        DetalleHorizonte.Text = horizonte.Detalle;
        ResaltarFicha(vencidos, proximos);
        if (_enfocarProximos && proximos.Count > 0)
        {
            ListaProximos.ScrollIntoView(proximos[0]);
        }
    }

    private OpcionHorizonte HorizonteActual()
    {
        var indice = ComboHorizonte.SelectedIndex;
        return indice is >= 0 && indice < Horizontes.Length
            ? Horizontes[indice]
            : Horizontes[IndicePorDefecto];
    }

    private void ResaltarFicha(List<AlertaCaducidad> vencidos, List<AlertaCaducidad> proximos)
    {
        if (_fichaNavegada is not Guid id)
        {
            return;
        }

        var vencido = vencidos.FirstOrDefault(a => a.Ficha.Id == id);
        if (vencido is not null)
        {
            ListaVencidos.ScrollIntoView(vencido);
            return;
        }

        var proximo = proximos.FirstOrDefault(a => a.Ficha.Id == id);
        if (proximo is not null)
        {
            ListaProximos.ScrollIntoView(proximo);
        }
    }

    private static int IndiceHorizonte(Guid? fichaId)
    {
        if (fichaId is not Guid id)
        {
            return IndicePorDefecto;
        }

        var repo = App.Instance.Repositorio;
        if (repo.PartidasVencidas().Any(a => a.Ficha.Id == id))
        {
            return IndicePorDefecto;
        }

        foreach (var (horizonte, indice) in Horizontes.Select((h, i) => (h, i)).OrderBy(x => x.h.Limite))
        {
            if (repo.PartidasProximasHasta(horizonte.Limite).Any(a => a.Ficha.Id == id))
            {
                return indice;
            }
        }

        return Horizontes.Length - 1;
    }

    private void Lista_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AlertaCaducidad alerta)
        {
            App.Instance.MainAppWindow?.IrAArea(alerta.Ficha.Area, alerta.Ficha.Id);
        }
    }

    private readonly record struct OpcionHorizonte(string Etiqueta, string Detalle, bool PorMeses, int Valor)
    {
        public DateTimeOffset Limite => PorMeses
            ? DateTimeOffset.Now.Date.AddMonths(Valor)
            : DateTimeOffset.Now.Date.AddDays(Valor);

        public static OpcionHorizonte Dias(int dias) =>
            new($"Próximos {dias} días", $"Caducan en los próximos {dias} días.", false, dias);

        public static OpcionHorizonte Meses(int meses)
        {
            var etiqueta = meses == 1 ? "Próximo mes" : $"Próximos {meses} meses";
            var detalle = meses == 1
                ? "Caducan en el próximo mes."
                : $"Caducan en los próximos {meses} meses.";
            return new(etiqueta, detalle, true, meses);
        }
    }
}
