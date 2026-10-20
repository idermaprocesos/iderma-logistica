using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas.Pages;

public sealed partial class OrdenCompraPage : Page, IVistaInventario
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private readonly DispatcherTimer _relojBusqueda = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly HashSet<Guid> _marcadas = [];
    private readonly HashSet<int> _meses = [];
    private int? _atajoMeses;
    private List<OrdenProductoItem> _visibles = [];
    private Guid? _unidad;
    private bool _sinArea;
    private bool _actualizandoFiltros;
    private bool _modoSeleccionar;
    private bool _ignorarClicLista;
    private int _anclaSeleccion = -1;

    public OrdenCompraPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
        _relojBusqueda.Tick += (_, _) =>
        {
            _relojBusqueda.Stop();
            RefrescarLista();
        };
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        if (e.NavigationMode == Microsoft.UI.Xaml.Navigation.NavigationMode.Back)
        {
            return;
        }

        _actualizandoFiltros = true;
        ComboOrden.ItemsSource = OrdenFichas.Opciones.Select(o => o.Texto).ToList();
        ComboOrden.SelectedIndex = OrdenFichas.IndicePredeterminado;
        ComboStock.ItemsSource = FiltrosCatalogo.OpcionesStock;
        ComboStock.SelectedIndex = 0;
        ComboCaducidad.ItemsSource = FiltrosCatalogo.OpcionesCaducidad;
        ComboCaducidad.SelectedIndex = 0;
        ComboMesDesde.ItemsSource = FiltroPeriodoIngresos.NombresMesCompletos;
        ComboMesHasta.ItemsSource = FiltroPeriodoIngresos.NombresMesCompletos;
        ComboMesDesde.SelectedIndex = -1;
        ComboMesHasta.SelectedIndex = -1;
        ConstruirMeses();
        CargarAnios();
        _actualizandoFiltros = false;
        ConstruirChips();
        RefrescarLista();
    }

    private void CajaBusqueda_TextChanged(object sender, TextChangedEventArgs e)
    {
        _relojBusqueda.Stop();
        _relojBusqueda.Start();
    }

    private void Filtros_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_actualizandoFiltros)
        {
            return;
        }

        RefrescarLista();
    }

    private void Periodo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_actualizandoFiltros)
        {
            return;
        }

        QuitarAtajoMeses();
        RefrescarLista();
    }

    private void RangoMeses_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_actualizandoFiltros)
        {
            return;
        }

        var desde = ComboMesDesde.SelectedIndex;
        var hasta = ComboMesHasta.SelectedIndex;
        if (desde < 0 || hasta < 0)
        {
            return;
        }

        var inicio = Math.Min(desde, hasta) + 1;
        var fin = Math.Max(desde, hasta) + 1;
        _meses.Clear();
        _actualizandoFiltros = true;
        foreach (var chip in PanelMeses.Children.OfType<ToggleButton>())
        {
            if (chip.Tag is not int mes)
            {
                continue;
            }

            var activo = mes >= inicio && mes <= fin;
            chip.IsChecked = activo;
            if (activo)
            {
                _meses.Add(mes);
            }
        }

        _actualizandoFiltros = false;
        QuitarAtajoMeses();
        RefrescarLista();
    }

    private void ChipStock_Click(object sender, RoutedEventArgs e)
    {
        if (_actualizandoFiltros)
        {
            return;
        }

        ComboStock.SelectedIndex = ChipStockCritico.IsChecked == true
            ? (int)FiltroStockCatalogo.Critico
            : (int)FiltroStockCatalogo.Todos;
    }

    private void ChipCaducidad_Click(object sender, RoutedEventArgs e)
    {
        if (_actualizandoFiltros || sender is not ToggleButton chip)
        {
            return;
        }

        var indice = int.TryParse(chip.Tag?.ToString(), out var n) ? n : 0;
        ComboCaducidad.SelectedIndex = chip.IsChecked == true ? indice : 0;
    }

    private void LimpiarFiltros_Click(object sender, RoutedEventArgs e)
    {
        _actualizandoFiltros = true;
        CajaBusqueda.Text = string.Empty;
        _unidad = null;
        _sinArea = false;
        ComboStock.SelectedIndex = 0;
        ComboCaducidad.SelectedIndex = 0;
        ComboOrden.SelectedIndex = OrdenFichas.IndicePredeterminado;
        ComboAnio.SelectedIndex = 0;
        ComboMesDesde.SelectedIndex = -1;
        ComboMesHasta.SelectedIndex = -1;
        _meses.Clear();
        _atajoMeses = null;
        foreach (var hijo in PanelMeses.Children.OfType<ToggleButton>())
        {
            hijo.IsChecked = false;
        }

        ChipUltimos3.IsChecked = false;
        ChipUltimos6.IsChecked = false;
        SeleccionarPrimero(ComboCategoria);
        SeleccionarPrimero(ComboProveedor);
        SeleccionarPrimero(ComboUbicacion);
        SeleccionarPrimero(ComboLote);
        _actualizandoFiltros = false;
        ConstruirChips();
        RefrescarLista();
    }

    private static void SeleccionarPrimero(ComboBox combo)
    {
        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    private void ConstruirChips()
    {
        _actualizandoFiltros = true;
        PanelUnidades.Children.Clear();
        var repo = App.Instance.Repositorio;
        PanelUnidades.Children.Add(CrearChip("Todas las unidades", null, false, !_unidad.HasValue && !_sinArea, repo.Todas.Count));
        foreach (var area in AreasOperativas.Todas)
        {
            PanelUnidades.Children.Add(CrearChip(area.Nombre, area.Id, false, _unidad == area.Id && !_sinArea, repo.Contar(area.Id)));
        }

        PanelUnidades.Children.Add(CrearChip("Sin área", null, true, _sinArea, repo.Contar(null)));
        _actualizandoFiltros = false;
    }

    private ToggleButton CrearChip(string texto, Guid? area, bool sinArea, bool activo, int conteo)
    {
        var chip = new ToggleButton
        {
            Content = new TextBlock { Text = $"{texto} ({conteo})" },
            IsChecked = activo,
            Tag = new FiltroUnidadOrden(area, sinArea)
        };
        chip.Click += ChipUnidad_Click;
        return chip;
    }

    private void ChipUnidad_Click(object sender, RoutedEventArgs e)
    {
        if (_actualizandoFiltros || sender is not ToggleButton chip || chip.Tag is not FiltroUnidadOrden filtro)
        {
            return;
        }

        _unidad = filtro.SinArea ? null : filtro.Area;
        _sinArea = filtro.SinArea;
        ConstruirChips();
        RefrescarLista();
    }

    private void ConstruirMeses()
    {
        PanelMeses.Children.Clear();
        for (var mes = 1; mes <= 12; mes++)
        {
            var chip = new ToggleButton
            {
                Content = FiltroPeriodoIngresos.NombresMes[mes - 1],
                Tag = mes,
                MinWidth = 48
            };
            chip.Click += ChipMes_Click;
            PanelMeses.Children.Add(chip);
        }
    }

    private void ChipMes_Click(object sender, RoutedEventArgs e)
    {
        if (_actualizandoFiltros || sender is not ToggleButton chip || chip.Tag is not int mes)
        {
            return;
        }

        if (chip.IsChecked == true)
        {
            _meses.Add(mes);
        }
        else
        {
            _meses.Remove(mes);
        }

        _actualizandoFiltros = true;
        ComboMesDesde.SelectedIndex = -1;
        ComboMesHasta.SelectedIndex = -1;
        _actualizandoFiltros = false;
        QuitarAtajoMeses();
        RefrescarLista();
    }

    private void TodosMeses_Click(object sender, RoutedEventArgs e)
    {
        _meses.Clear();
        _actualizandoFiltros = true;
        foreach (var hijo in PanelMeses.Children.OfType<ToggleButton>())
        {
            hijo.IsChecked = false;
        }

        ComboMesDesde.SelectedIndex = -1;
        ComboMesHasta.SelectedIndex = -1;
        _actualizandoFiltros = false;
        QuitarAtajoMeses();
        RefrescarLista();
    }

    private void AtajoMeses_Click(object sender, RoutedEventArgs e)
    {
        if (_actualizandoFiltros || sender is not ToggleButton chip ||
            !int.TryParse(chip.Tag?.ToString(), out var meses))
        {
            return;
        }

        if (chip.IsChecked == true)
        {
            _atajoMeses = meses;
            _actualizandoFiltros = true;
            ComboAnio.SelectedIndex = 0;
            ComboMesDesde.SelectedIndex = -1;
            ComboMesHasta.SelectedIndex = -1;
            _meses.Clear();
            foreach (var hijo in PanelMeses.Children.OfType<ToggleButton>())
            {
                hijo.IsChecked = false;
            }

            _actualizandoFiltros = false;
        }
        else if (_atajoMeses == meses)
        {
            _atajoMeses = null;
        }

        ActualizarAtajos();
        RefrescarLista();
    }

    private void QuitarAtajoMeses()
    {
        if (_atajoMeses is null)
        {
            return;
        }

        _atajoMeses = null;
        ActualizarAtajos();
    }

    private void ActualizarAtajos()
    {
        _actualizandoFiltros = true;
        ChipUltimos3.IsChecked = _atajoMeses == 3;
        ChipUltimos6.IsChecked = _atajoMeses == 6;
        _actualizandoFiltros = false;
    }

    private void CargarAnios()
    {
        var actual = ComboAnio.SelectedItem as string;
        var opciones = new List<string> { "Todos los años" };
        opciones.AddRange(App.Instance.Registro.Anios().Select(a => a.ToString(Cultura)));
        ComboAnio.ItemsSource = opciones;
        if (actual is not null && opciones.Contains(actual))
        {
            ComboAnio.SelectedItem = actual;
        }
        else
        {
            ComboAnio.SelectedIndex = 0;
        }
    }

    private int? AnioActual()
    {
        if (ComboAnio.SelectedIndex <= 0 || ComboAnio.SelectedItem is not string texto)
        {
            return null;
        }

        return int.TryParse(texto, out var anio) ? anio : null;
    }

    private DateTimeOffset? DesdeActual() =>
        _atajoMeses is int n ? FiltroPeriodoIngresos.InicioVentanaMeses(n) : null;

    private int? AnioParaFiltro() => _atajoMeses is null ? AnioActual() : null;

    private IReadOnlySet<int> MesesParaFiltro() => _atajoMeses is null ? _meses : [];

    private string PeriodoActual() =>
        FiltroPeriodoIngresos.Describir(AnioParaFiltro(), MesesParaFiltro(), DesdeActual(), _atajoMeses);

    public void AplicarCambioInventario()
    {
        if (_marcadas.Count > 0)
        {
            return;
        }

        ConstruirChips();
        RefrescarLista();
    }

    private void RefrescarLista()
    {
        if (ComboOrden.SelectedIndex < 0 || ComboStock.SelectedIndex < 0 || ComboCaducidad.SelectedIndex < 0)
        {
            return;
        }

        var repo = App.Instance.Repositorio;
        var registro = App.Instance.Registro;
        var precios = App.Instance.HistorialPrecios;
        var filtro = CajaBusqueda.Text?.Trim() ?? string.Empty;
        var origenArea = repo.Buscar(string.Empty, _sinArea ? null : _unidad, _sinArea);
        RellenarListas(origenArea);

        _actualizandoFiltros = true;
        CargarAnios();
        _actualizandoFiltros = false;

        var anio = AnioParaFiltro();
        var meses = MesesParaFiltro();
        var desde = DesdeActual();
        var atajo = _atajoMeses;
        var fichas = repo.Buscar(filtro, _sinArea ? null : _unidad, _sinArea)
            .Where(f => FiltrosCatalogo.Coincide(
                f,
                (FiltroStockCatalogo)ComboStock.SelectedIndex,
                (FiltroCaducidadCatalogo)ComboCaducidad.SelectedIndex,
                ComboCategoria.SelectedItem as string,
                ComboProveedor.SelectedItem as string,
                ComboUbicacion.SelectedItem as string,
                ComboLote.SelectedItem as string));
        var criterio = OrdenFichas.Opciones[ComboOrden.SelectedIndex].Criterio;
        _visibles = OrdenFichas.Aplicar(fichas, criterio)
            .Select(ficha =>
            {
                var consumo = CalculoOrdenCompra.ConsumoEnPeriodo(registro.PorFicha(ficha.Id), anio, meses, desde, atajo);
                var ultimo = precios.Ultimo(ficha.Id);
                var unidad = string.IsNullOrWhiteSpace(ficha.UnidadMedida) ? "" : $" {ficha.UnidadMedida}";
                return new OrdenProductoItem
                {
                    Id = ficha.Id,
                    Ficha = ficha,
                    Codigo = string.IsNullOrWhiteSpace(ficha.Codigo) ? "—" : ficha.Codigo,
                    Nombre = ficha.Nombre,
                    Resumen = $"{ficha.AreaTitulo} · {ficha.Categoria}".Trim(' ', '·'),
                    StockTexto = $"Stock {ficha.Existencia.ToString("0.##", Cultura)}{unidad}",
                    ConsumoTexto = $"Cons. {consumo.ToString("0.##", Cultura)}{unidad}",
                    PrecioTexto = ultimo is null || ultimo.Monto <= 0
                        ? "Sin precio"
                        : MonedaPrecio.Formato(ultimo.Monto, ultimo.Moneda),
                    EstaMarcada = _marcadas.Contains(ficha.Id)
                };
            })
            .ToList();

        ListaProductos.ItemsSource = _visibles;
        _anclaSeleccion = -1;
        TituloLista.Text = _sinArea
            ? $"Productos sin área ({_visibles.Count})"
            : _unidad is Guid id
                ? $"{AreasOperativas.Titulo(id)} ({_visibles.Count})"
                : $"Todos los productos ({_visibles.Count})";
        TextoPeriodo.Text = PeriodoActual();
        ActualizarChipsEstado();
        ActualizarResumenMarcas();
    }

    private void RellenarListas(IReadOnlyList<FichaTecnica> origen)
    {
        _actualizandoFiltros = true;
        AsignarLista(ComboCategoria, FiltrosCatalogo.ValoresUnicos(origen, f => f.Categoria, FiltrosCatalogo.TodasCategorias));
        AsignarLista(ComboProveedor, FiltrosCatalogo.ValoresUnicos(origen, f => f.Proveedor, FiltrosCatalogo.TodosProveedores));
        AsignarLista(ComboUbicacion, FiltrosCatalogo.ValoresUnicos(origen, f => f.Ubicacion, FiltrosCatalogo.TodasUbicaciones));
        AsignarLista(ComboLote, FiltrosCatalogo.LotesUnicos(origen));
        _actualizandoFiltros = false;
    }

    private static void AsignarLista(ComboBox combo, IReadOnlyList<string> items)
    {
        var actual = combo.SelectedItem as string;
        combo.ItemsSource = items;
        if (actual is not null && items.Any(i => string.Equals(i, actual, StringComparison.CurrentCultureIgnoreCase)))
        {
            combo.SelectedItem = items.First(i => string.Equals(i, actual, StringComparison.CurrentCultureIgnoreCase));
        }
        else
        {
            combo.SelectedIndex = items.Count > 0 ? 0 : -1;
        }
    }

    private void ActualizarChipsEstado()
    {
        _actualizandoFiltros = true;
        ChipStockCritico.IsChecked = ComboStock.SelectedIndex == (int)FiltroStockCatalogo.Critico;
        ChipVencidos.IsChecked = ComboCaducidad.SelectedIndex == (int)FiltroCaducidadCatalogo.Vencidas;
        ChipProximos.IsChecked = ComboCaducidad.SelectedIndex == (int)FiltroCaducidadCatalogo.Proximas;
        var todas = App.Instance.Repositorio.Todas;
        TextoChipStock.Text = $"Stock crítico ({todas.Count(f => f.EstaStockCritico)})";
        TextoChipVencidos.Text = $"Vencidos ({App.Instance.Repositorio.PartidasVencidas().Count()})";
        TextoChipProximos.Text = $"Próximos 30 días ({App.Instance.Repositorio.PartidasProximas(30).Count()})";
        _actualizandoFiltros = false;
    }

    private void ListaProductos_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_ignorarClicLista)
        {
            _ignorarClicLista = false;
            return;
        }

        if (e.ClickedItem is not OrdenProductoItem item ||
            !SeleccionListaFichas.DebeMarcarSinAbrir(_modoSeleccionar))
        {
            return;
        }

        SeleccionListaFichas.AplicarClic(_visibles, _marcadas, item, ref _anclaSeleccion);
        ActualizarResumenMarcas();
    }

    private void ListaProductos_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.A && SeleccionListaFichas.CtrlIzquierdoPulsado)
        {
            SeleccionListaFichas.SeleccionarTodas(_visibles, _marcadas);
            ActualizarResumenMarcas();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            SeleccionListaFichas.DeseleccionarTodas(_visibles, _marcadas);
            ActualizarResumenMarcas();
            e.Handled = true;
        }
    }

    private void ModoSeleccionar_Click(object sender, RoutedEventArgs e)
    {
        _modoSeleccionar = BotonModoSeleccionar.IsChecked == true;
        TextoModoSeleccionar.Text = _modoSeleccionar ? "Seleccionando" : "Seleccionar";
    }

    private void SeleccionarTodas_Click(object sender, RoutedEventArgs e)
    {
        SeleccionListaFichas.SeleccionarTodas(_visibles, _marcadas);
        ActualizarResumenMarcas();
    }

    private void DeseleccionarTodas_Click(object sender, RoutedEventArgs e)
    {
        SeleccionListaFichas.DeseleccionarTodas(_visibles, _marcadas);
        ActualizarResumenMarcas();
    }

    private void SeleccionarCruzada_Click(object sender, RoutedEventArgs e)
    {
        SeleccionListaFichas.SeleccionarCruzada(_visibles, _marcadas);
        ActualizarResumenMarcas();
    }

    private void SeleccionarArmonia_Click(object sender, RoutedEventArgs e)
    {
        SeleccionListaFichas.SeleccionarArmonia(_visibles, _marcadas);
        ActualizarResumenMarcas();
    }

    private void MarcaFicha_Tapped(object sender, TappedRoutedEventArgs e) =>
        e.Handled = true;

    private void MarcaFicha_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox caja)
        {
            return;
        }

        _ignorarClicLista = true;
        var item = caja.DataContext as OrdenProductoItem
                   ?? _visibles.FirstOrDefault(f => Equals(caja.Tag, f.Id));
        if (item is null)
        {
            return;
        }

        item.EstaMarcada = caja.IsChecked == true;
        if (item.EstaMarcada)
        {
            _marcadas.Add(item.Id);
        }
        else
        {
            _marcadas.Remove(item.Id);
        }

        ActualizarResumenMarcas();
    }

    private void ActualizarResumenMarcas()
    {
        var marcadas = _visibles.Count(f => f.EstaMarcada);
        ResumenMarcas.Text = marcadas == 0
            ? "Marque productos para armar la orden. El consumo usa el periodo elegido."
            : $"{marcadas} seleccionada(s). La orden usa este periodo para el consumo promedio.";
    }

    private void VerEnCatalogo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid id })
        {
            return;
        }

        App.Instance.MainAppWindow?.IrACatalogo(id);
    }

    private void GenerarOrden_Click(object sender, RoutedEventArgs e)
    {
        var fichas = _visibles.Where(i => i.EstaMarcada).Select(i => i.Ficha).ToList();
        if (fichas.Count == 0)
        {
            Mostrar("Marque al menos un producto para generar la orden de compra.", InfoBarSeverity.Warning);
            return;
        }

        var lineas = OrdenCompraArmado.DesdeSeleccion(
            fichas, AnioParaFiltro(), MesesParaFiltro(), DesdeActual(), _atajoMeses);
        Frame.Navigate(typeof(OrdenCompraWindow), lineas);
    }

    private void EditarBorradores_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(BorradoresOrdenPage));

    private void VerAnteriores_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(RegistroOrdenesWindow));

    private void Mostrar(string mensaje, InfoBarSeverity severidad)
    {
        BarraAviso.Severity = severidad;
        BarraAviso.Message = mensaje;
        BarraAviso.IsOpen = true;
    }

    private readonly record struct FiltroUnidadOrden(Guid? Area, bool SinArea);
}

public sealed class OrdenProductoItem : INotifyPropertyChanged, IItemMarcable
{
    private bool _estaMarcada;

    public Guid Id { get; init; }
    public required FichaTecnica Ficha { get; init; }
    public required string Codigo { get; init; }
    public required string Nombre { get; init; }
    public required string Resumen { get; init; }
    public required string StockTexto { get; init; }
    public required string ConsumoTexto { get; init; }
    public required string PrecioTexto { get; init; }

    public bool EstaMarcada
    {
        get => _estaMarcada;
        set
        {
            if (_estaMarcada == value)
            {
                return;
            }

            _estaMarcada = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? nombre = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
}
