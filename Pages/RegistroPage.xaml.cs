using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Windows.Storage.Pickers;

namespace IdermaFichas.Pages;

public sealed partial class RegistroPage : Page, IVistaInventario
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-MX");
    private readonly DispatcherTimer _relojBusqueda = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly HashSet<Guid> _marcadas = [];
    private readonly HashSet<int> _meses = [];
    private int? _atajoMeses;
    private List<ProductoRegistroItem> _visibles = [];
    private Guid? _unidad;
    private bool _sinArea;
    private Guid? _fichaSeleccionada;
    private bool _actualizandoFiltros;
    private bool _modoSeleccionar;
    private bool _ignorarClicLista;
    private int _anclaSeleccion = -1;
    private string? _reciboPendiente;
    private string _reciboNombre = string.Empty;
    private readonly HashSet<Guid> _marcadasMovimientos = [];
    private List<MovimientoRegistroItem> _movimientosVisibles = [];
    private bool _modoEditarMovimientos;
    private bool _ignorarClicHistorial;
    private int _anclaMovimientos = -1;
    private Guid? _movimientoEnEdicion;
    private bool _actualizandoPartida;
    private bool _ajustandoFecha;
    private bool _caducidadVacia = true;
    private bool _quitarReciboExistente;
    public RegistroPage()
    {
        InitializeComponent();
        FechaCaducidadMovimiento.DateChanged += (_, _) =>
        {
            if (!_ajustandoFecha)
            {
                _caducidadVacia = false;
            }
        };
        _relojBusqueda.Tick += (_, _) =>
        {
            _relojBusqueda.Stop();
            RefrescarLista();
        };
        Unloaded += (_, _) =>
        {
            _relojBusqueda.Stop();
            QuitarArchivoPendiente();
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _actualizandoFiltros = true;
        ComboOrden.ItemsSource = OrdenFichas.Opciones.Select(o => o.Texto).ToList();
        ComboOrden.SelectedIndex = OrdenFichas.IndicePredeterminado;
        ComboStock.ItemsSource = FiltrosCatalogo.OpcionesStock;
        ComboStock.SelectedIndex = 0;
        ComboCaducidad.ItemsSource = FiltrosCatalogo.OpcionesCaducidad;
        ComboCaducidad.SelectedIndex = 0;
        ComboTipoMovimiento.ItemsSource = new[] { "Ingreso", "Salida" };
        ComboTipoMovimiento.SelectedIndex = 0;
        var alcances = new[] { "Todos", "Solo ingresos", "Solo egresos" };
        ComboAlcanceSeleccion.ItemsSource = alcances;
        ComboAlcanceSeleccion.SelectedIndex = 0;
        ComboAlcanceHistorial.ItemsSource = alcances;
        ComboAlcanceHistorial.SelectedIndex = 0;
        ComboTipoDocumento.ItemsSource = new[] { "Boleta", "Factura" };
        ComboTipoDocumento.SelectedIndex = 0;
        ActualizarFormularioMovimiento();
        ConstruirMeses();
        CargarAnios();
        PrepararFichaNavegada(e.Parameter);
        _actualizandoFiltros = false;
        ConstruirChips();
        RefrescarLista();
    }

    private void PrepararFichaNavegada(object? parameter)
    {
        _fichaSeleccionada = parameter is Guid id ? id : null;
        if (_fichaSeleccionada is not Guid fichaId || App.Instance.Repositorio.Obtener(fichaId) is not { } ficha)
        {
            return;
        }

        CajaBusqueda.Text = string.IsNullOrWhiteSpace(ficha.Codigo) ? ficha.Nombre : ficha.Codigo;
        _relojBusqueda.Stop();
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
        _meses.Clear();
        _atajoMeses = null;
        foreach (var hijo in PanelMeses.Children.OfType<ToggleButton>())
        {
            hijo.IsChecked = false;
        }

        ChipUltimos3.IsChecked = false;
        ChipUltimos6.IsChecked = false;

        if (ComboCategoria.Items.Count > 0)
        {
            ComboCategoria.SelectedIndex = 0;
        }

        if (ComboProveedor.Items.Count > 0)
        {
            ComboProveedor.SelectedIndex = 0;
        }

        if (ComboUbicacion.Items.Count > 0)
        {
            ComboUbicacion.SelectedIndex = 0;
        }

        if (ComboLote.Items.Count > 0)
        {
            ComboLote.SelectedIndex = 0;
        }

        _actualizandoFiltros = false;
        ConstruirChips();
        RefrescarLista();
    }

    private void ConstruirChips()
    {
        _actualizandoFiltros = true;
        PanelUnidades.Children.Clear();
        var repo = App.Instance.Repositorio;

        PanelUnidades.Children.Add(CrearChip("Todas las unidades", null, false, !_unidad.HasValue && !_sinArea, repo.Todas.Count));
        foreach (var area in AreasOperativas.Todas)
        {
            PanelUnidades.Children.Add(CrearChip(
                area.Nombre,
                area.Id,
                false,
                _unidad == area.Id && !_sinArea,
                repo.Contar(area.Id)));
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
            Tag = new FiltroUnidad(area, sinArea)
        };
        chip.Click += ChipUnidad_Click;
        return chip;
    }

    private void ChipUnidad_Click(object sender, RoutedEventArgs e)
    {
        if (_actualizandoFiltros || sender is not ToggleButton chip || chip.Tag is not FiltroUnidad filtro)
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
        ConstruirChips();
        RefrescarLista();
        AnimacionLista.Revelar(ListaProductos);
        AnimacionLista.Revelar(ListaHistorial);
    }

    private void RefrescarLista()
    {
        if (ComboOrden.SelectedIndex < 0 || ComboStock.SelectedIndex < 0 || ComboCaducidad.SelectedIndex < 0)
        {
            return;
        }

        var repo = App.Instance.Repositorio;
        var registro = App.Instance.Registro;
        var filtro = CajaBusqueda.Text?.Trim() ?? string.Empty;
        var origenArea = repo.Buscar(string.Empty, _sinArea ? null : _unidad, _sinArea);
        RellenarListas(origenArea);

        _actualizandoFiltros = true;
        CargarAnios();
        _actualizandoFiltros = false;

        var anio = AnioParaFiltro();
        var meses = MesesParaFiltro();
        var desde = DesdeActual();
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
                var movimientos = registro.PorFicha(ficha.Id, anio, meses, desde);
                var ultimo = movimientos.FirstOrDefault();
                var ingresos = movimientos.Where(m => !m.EsSalida).Sum(m => m.Cantidad);
                var salidas = movimientos.Where(m => m.EsSalida).Sum(m => m.Cantidad);
                return new ProductoRegistroItem
                {
                    Id = ficha.Id,
                    Ficha = ficha,
                    Codigo = ficha.Codigo,
                    Nombre = ficha.Nombre,
                    Resumen = $"{ficha.AreaTitulo} · exist. {ficha.Existencia.ToString("0.##", Cultura)} {ficha.UnidadMedida}".Trim(),
                    TotalIngresos = $"+{ingresos.ToString("0.##", Cultura)}",
                    TotalSalidas = $"-{salidas.ToString("0.##", Cultura)}",
                    UltimoIngreso = ultimo is null
                        ? "Sin movimientos en el periodo"
                        : TextoFecha(ultimo.Fecha)[..10],
                    EstaMarcada = _marcadas.Contains(ficha.Id)
                };
            })
            .ToList();

        _ignorarClicLista = true;
        ListaProductos.ItemsSource = _visibles;
        _anclaSeleccion = -1;
        TituloLista.Text = _sinArea
            ? $"Productos sin área ({_visibles.Count})"
            : _unidad is Guid id
                ? $"{AreasOperativas.Titulo(id)} ({_visibles.Count})"
                : $"Todos los productos ({_visibles.Count})";
        SubtituloUnidad.Text = _sinArea
            ? "Productos que no pertenecen a ninguna unidad."
            : _unidad is Guid unidad
                ? $"Historial de ingresos de {AreasOperativas.Titulo(unidad)}."
                : "Busque por código, nombre o categoría y pulse un producto para ver su histórico.";

        ActualizarChipsEstado();
        ActualizarResumenMarcas();

        if (_fichaSeleccionada is Guid seleccion)
        {
            var coincide = _visibles.FirstOrDefault(i => i.Id == seleccion);
            MostrarDetalle(coincide?.Ficha, cargarNube: false);
            if (coincide is not null)
            {
                ListaProductos.ScrollIntoView(coincide);
            }
        }
        else
        {
            MostrarDetalle(null);
        }

        _ignorarClicLista = false;
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
        if (actual is not null &&
            items.Any(i => string.Equals(i, actual, StringComparison.CurrentCultureIgnoreCase)))
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
        var vencidos = App.Instance.Repositorio.PartidasVencidas().Count();
        var proximos = App.Instance.Repositorio.PartidasProximas(30).Count();
        TextoChipStock.Text = $"Stock crítico ({todas.Count(f => f.EstaStockCritico)})";
        TextoChipVencidos.Text = $"Vencidos ({vencidos})";
        TextoChipProximos.Text = $"Próximos 30 días ({proximos})";
        _actualizandoFiltros = false;
    }

    private void ListaProductos_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_ignorarClicLista)
        {
            _ignorarClicLista = false;
            return;
        }

        if (e.ClickedItem is not ProductoRegistroItem item)
        {
            return;
        }

        if (SeleccionListaFichas.DebeMarcarSinAbrir(_modoSeleccionar))
        {
            SeleccionListaFichas.AplicarClic(_visibles, _marcadas, item, ref _anclaSeleccion);
            ActualizarResumenMarcas();
            return;
        }

        _fichaSeleccionada = item.Id;
        MostrarDetalle(item.Ficha, cargarNube: true);
    }

    private void VerEnCatalogo_Click(object sender, RoutedEventArgs e)
    {
        var item = ProductoDesdeMenu(sender);
        if (item is null)
        {
            return;
        }

        App.Instance.MainAppWindow?.IrACatalogo(item.Id);
    }

    private ProductoRegistroItem? ProductoDesdeMenu(object sender)
    {
        if (sender is FrameworkElement elemento)
        {
            if (elemento.DataContext is ProductoRegistroItem desdeContexto)
            {
                return desdeContexto;
            }

            if (elemento.Tag is Guid id)
            {
                return _visibles.FirstOrDefault(p => p.Id == id);
            }
        }

        return null;
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
        var item = caja.DataContext as ProductoRegistroItem
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
            ? "Marque productos para exportar su historial del periodo."
            : $"{marcadas} seleccionada(s). La exportación usa el año o los meses filtrados.";
    }

    private void MostrarDetalle(FichaTecnica? ficha, bool cargarNube = false)
    {
        try
        {
            PintarDetalle(ficha, cargarNube);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo abrir el historial: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void PintarDetalle(FichaTecnica? ficha, bool cargarNube)
    {
        TextoPeriodo.Text = $"Periodo: {PeriodoActual()}";
        if (_movimientoEnEdicion is Guid enEdicion)
        {
            var editado = App.Instance.Registro.Obtener(enEdicion);
            if (ficha is null || editado is null || editado.FichaId != ficha.Id)
            {
                CancelarEdicion();
            }
        }

        if (ficha is null)
        {
            TituloProducto.Text = "Elija un producto";
            ResumenProducto.Text = "Aparece la lista completa de la unidad. Al pulsar un ítem se abre su histórico.";
            TotalesProducto.Text = string.Empty;
            TextoIngresosProducto.Text = string.Empty;
            TextoSalidasProducto.Text = string.Empty;
            _movimientosVisibles = [];
            ListaHistorial.ItemsSource = _movimientosVisibles;
            PanelIngreso.Visibility = Visibility.Collapsed;
            ActualizarAccionesMovimientos();
            ActualizarBotonBorrarLotes(null);
            CargarOpcionesPartida(null);
            return;
        }

        var registro = App.Instance.Registro;
        var movimientos = registro.PorFicha(ficha.Id, AnioParaFiltro(), MesesParaFiltro(), DesdeActual());
        var unidad = string.IsNullOrWhiteSpace(ficha.UnidadMedida) ? "" : $" {ficha.UnidadMedida}";
        var ingresos = movimientos.Where(m => !m.EsSalida).Sum(m => m.Cantidad);
        var salidas = movimientos.Where(m => m.EsSalida).Sum(m => m.Cantidad);
        TituloProducto.Text = string.IsNullOrWhiteSpace(ficha.Nombre) ? ficha.Codigo : ficha.Nombre;
        ResumenProducto.Text = $"{ficha.Codigo} · {ficha.AreaTitulo}";
        var lotes = ficha.Partidas.Count(p => p.Cantidad > 0.0001);
        TotalesProducto.Text = lotes > 1
            ? $"Existencia actual: {ficha.Existencia.ToString("0.##", Cultura)}{unidad} en {lotes} lotes"
            : $"Existencia actual: {ficha.Existencia.ToString("0.##", Cultura)}{unidad}";
        TextoIngresosProducto.Text = $"+{ingresos.ToString("0.##", Cultura)}{unidad}";
        TextoSalidasProducto.Text = $"-{salidas.ToString("0.##", Cultura)}{unidad}";
        if (cargarNube)
        {
            _ = CompletarHistorialAsync(ficha);
        }

        if (movimientos.Count == 0)
        {
            _movimientosVisibles =
            [
                new MovimientoRegistroItem
                {
                    FechaTexto = "Sin movimientos en el periodo",
                    Origen = "No hay ingresos ni salidas con el año o los meses elegidos.",
                    Nota = "Cambie el filtro de periodo o registre un movimiento.",
                    ReciboTexto = "",
                    CantidadTexto = "—",
                    ResultadoTexto = "",
                    VisibilidadIngreso = Visibility.Collapsed,
                    VisibilidadSalida = Visibility.Collapsed,
                    VisibilidadNeutro = Visibility.Visible
                }
            ];
            ListaHistorial.ItemsSource = _movimientosVisibles;
            ActualizarAccionesMovimientos();
            ActualizarBotonBorrarLotes(ficha);
            CargarOpcionesPartida(ficha);
            PanelIngreso.Visibility = Visibility.Visible;
            return;
        }

        var marca = _modoEditarMovimientos ? Visibility.Visible : Visibility.Collapsed;
        _movimientosVisibles = movimientos.Select(m => new MovimientoRegistroItem
        {
            Id = m.Id,
            EsMarcable = true,
            FechaTexto = TextoFecha(m.Fecha),
            Origen = m.DocumentoResumen,
            Nota = m.Nota,
            ReciboTexto = string.IsNullOrWhiteSpace(m.NombreRecibo) && ReciboMovimientoService.RutaEfectiva(m) is null
                ? ""
                : $"Recibo: {(string.IsNullOrWhiteSpace(m.NombreRecibo) ? Path.GetFileName(ReciboMovimientoService.RutaEfectiva(m)) : m.NombreRecibo)}",
            CantidadTexto = $"{(m.EsSalida ? "-" : "+")}{m.Cantidad.ToString("0.##", Cultura)}{unidad}",
            ResultadoTexto = $"Quedaron {m.ExistenciaResultante.ToString("0.##", Cultura)}{unidad}",
            VisibilidadIngreso = m.EsSalida ? Visibility.Collapsed : Visibility.Visible,
            VisibilidadSalida = m.EsSalida ? Visibility.Visible : Visibility.Collapsed,
            VisibilidadNeutro = Visibility.Collapsed,
            VisibilidadMarca = marca,
            EstaMarcada = _marcadasMovimientos.Contains(m.Id)
        }).ToList();
        ListaHistorial.ItemsSource = _movimientosVisibles;
        ActualizarAccionesMovimientos();
        ActualizarBotonBorrarLotes(ficha);
        CargarOpcionesPartida(ficha);
        PanelIngreso.Visibility = Visibility.Visible;
    }

    private static string TextoFecha(DateTimeOffset fecha)
    {
        try
        {
            return fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Cultura);
        }
        catch (ArgumentException)
        {
            return fecha.ToString("dd/MM/yyyy HH:mm", Cultura);
        }
    }

    private async Task CompletarHistorialAsync(FichaTecnica ficha)
    {
        var cola = DispatcherQueue;
        bool cambio;
        try
        {
            cambio = await ModulosNube.CompletarRegistroAsync(ficha.Id);
        }
        catch
        {
            return;
        }

        if (!cambio || _fichaSeleccionada != ficha.Id)
        {
            return;
        }

        cola.TryEnqueue(() =>
        {
            if (_fichaSeleccionada == ficha.Id)
            {
                MostrarDetalle(App.Instance.Repositorio.Obtener(ficha.Id) ?? ficha);
            }
        });
    }

    private async void ExportarHistorial_Click(object sender, RoutedEventArgs e)
    {
        if (_fichaSeleccionada is not Guid id || App.Instance.Repositorio.Obtener(id) is not { } ficha)
        {
            Mostrar("Elija un producto para exportar su historial.", InfoBarSeverity.Warning);
            return;
        }

        var alcance = AlcanceExportacion(ComboAlcanceHistorial);
        var lineas = FiltrarPorAlcance(LineasDe([ficha]), alcance);
        await ExportarLineasAsync($"{PrefijoExportacion(alcance)}-{Sanitizar(ficha.Codigo)}", lineas, alcance);
    }

    private async void ExportarSeleccionadas_Click(object sender, RoutedEventArgs e)
    {
        var fichas = _visibles.Where(i => i.EstaMarcada).Select(i => i.Ficha).ToList();
        if (fichas.Count == 0)
        {
            Mostrar("Marque al menos un producto para exportar su historial.", InfoBarSeverity.Warning);
            return;
        }

        var alcance = AlcanceExportacion(ComboAlcanceSeleccion);
        await ExportarLineasAsync(
            $"{PrefijoExportacion(alcance)}-seleccion-{DateTime.Now:yyyyMMdd}",
            FiltrarPorAlcance(LineasDe(fichas), alcance),
            alcance);
    }

    private static string AlcanceExportacion(ComboBox combo) =>
        combo.SelectedItem as string ?? "Todos";

    private static string PrefijoExportacion(string alcance) => alcance switch
    {
        "Solo ingresos" => "ingresos",
        "Solo egresos" => "egresos",
        _ => "movimientos"
    };

    private static List<LineaRegistroIngreso> FiltrarPorAlcance(
        List<LineaRegistroIngreso> lineas,
        string alcance) => alcance switch
    {
        "Solo ingresos" => lineas.Where(l => !l.Movimiento.EsSalida).ToList(),
        "Solo egresos" => lineas.Where(l => l.Movimiento.EsSalida).ToList(),
        _ => lineas
    };

    private string DescribirExportacion(string alcance)
    {
        var periodo = PeriodoActual();
        return alcance is "Solo ingresos" or "Solo egresos"
            ? $"{periodo} · {alcance.ToLower(Cultura)}"
            : periodo;
    }

    private List<LineaRegistroIngreso> LineasDe(IEnumerable<FichaTecnica> fichas)
    {
        var registro = App.Instance.Registro;
        var anio = AnioParaFiltro();
        var meses = MesesParaFiltro();
        var desde = DesdeActual();
        var lineas = new List<LineaRegistroIngreso>();
        foreach (var ficha in fichas)
        {
            foreach (var movimiento in registro.PorFicha(ficha.Id, anio, meses, desde).OrderBy(m => m.Fecha))
            {
                lineas.Add(new LineaRegistroIngreso(ficha, movimiento));
            }
        }

        return lineas;
    }

    private async Task ExportarLineasAsync(
        string nombre,
        IReadOnlyList<LineaRegistroIngreso> lineas,
        string alcance)
    {
        if (lineas.Count == 0)
        {
            var vacio = alcance switch
            {
                "Solo ingresos" => "No hay ingresos en el periodo elegido para exportar.",
                "Solo egresos" => "No hay egresos en el periodo elegido para exportar.",
                _ => "No hay movimientos en el periodo elegido para exportar."
            };
            Mostrar(vacio, InfoBarSeverity.Warning);
            return;
        }

        try
        {
            var descripcion = DescribirExportacion(alcance);
            var resultado = await ExcelUi.ExportarIngresosAsync(nombre, lineas, descripcion);
            if (resultado is null)
            {
                return;
            }

            var carpetaRecibos = RegistroIngresosExportService.CopiarRecibos(resultado.Ruta, lineas);
            var extra = carpetaRecibos is null
                ? ""
                : $" Recibos copiados en «{Path.GetFileName(carpetaRecibos)}».";
            Mostrar($"Se exportaron {lineas.Count} movimiento(s) ({descripcion}).{extra}", InfoBarSeverity.Success);
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo exportar: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private static string Sanitizar(string valor)
    {
        var limpio = new string(valor.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(limpio) ? "producto" : limpio;
    }

    private void ComboTipoMovimiento_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ActualizarFormularioMovimiento();
        if (_fichaSeleccionada is Guid id && App.Instance.Repositorio.Obtener(id) is { } ficha)
        {
            CargarOpcionesPartida(ficha);
        }
    }

    private void ComboPartidaMovimiento_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_actualizandoPartida || ComboPartidaMovimiento.SelectedItem is not OpcionPartidaMovimiento opcion)
        {
            return;
        }

        if (opcion.EsNueva)
        {
            CajaLoteMovimiento.Text = string.Empty;
            AsignarFechaCalendario(null);
            return;
        }

        CajaLoteMovimiento.Text = opcion.Lote;
        AsignarFechaCalendario(opcion.Fecha);
    }

    private void CargarOpcionesPartida(FichaTecnica? ficha, Guid? seleccionar = null)
    {
        if (ComboPartidaMovimiento is null)
        {
            return;
        }

        _actualizandoPartida = true;
        if (ficha is null)
        {
            ComboPartidaMovimiento.ItemsSource = null;
            _actualizandoPartida = false;
            return;
        }

        PartidasInventario.Normalizar(ficha);
        var salida = EsSalidaSeleccionada();
        var opciones = new List<OpcionPartidaMovimiento>();
        if (salida)
        {
            var preferida = PartidasInventario.PreferidaParaSalida(ficha);
            opciones.Add(new OpcionPartidaMovimiento
            {
                EsAutomatico = true,
                Lote = preferida?.Lote ?? string.Empty,
                Fecha = preferida?.FechaCaducidad,
                Disponible = ficha.Existencia,
                Texto = "Automático · caducidad más próxima"
            });
        }
        else
        {
            opciones.Add(new OpcionPartidaMovimiento { Texto = "Nueva partida…", EsNueva = true });
        }

        foreach (var partida in PartidasInventario.OrdenFefo(ficha)
                     .Concat(ficha.Partidas.Where(p => p.Cantidad <= PartidasInventario.Epsilon)))
        {
            var unidad = string.IsNullOrWhiteSpace(ficha.UnidadMedida) ? "" : $" {ficha.UnidadMedida}";
            opciones.Add(new OpcionPartidaMovimiento
            {
                Id = partida.Id,
                Lote = partida.Lote,
                Fecha = partida.FechaCaducidad,
                Disponible = partida.Cantidad,
                Texto = $"{partida.LoteTexto} · {partida.FechaCaducidadTexto} · {partida.Cantidad.ToString("0.##", Cultura)}{unidad}"
            });
        }

        ComboPartidaMovimiento.ItemsSource = opciones;
        OpcionPartidaMovimiento? elegida = null;
        if (seleccionar is Guid id)
        {
            elegida = opciones.FirstOrDefault(o => o.Id == id);
        }

        ComboPartidaMovimiento.SelectedItem = elegida ?? opciones.FirstOrDefault();
        if (ComboPartidaMovimiento.SelectedItem is OpcionPartidaMovimiento actual && !actual.EsNueva)
        {
            CajaLoteMovimiento.Text = actual.Lote;
            AsignarFechaCalendario(actual.Fecha);
        }
        else if (!salida)
        {
            CajaLoteMovimiento.Text = string.Empty;
            AsignarFechaCalendario(null);
        }

        _actualizandoPartida = false;
        ActualizarFormularioMovimiento();
    }

    private void AsignarFechaCalendario(DateTimeOffset? fecha)
    {
        if (FechaCaducidadMovimiento is null)
        {
            return;
        }

        var visible = FechaCalendario.ParaPicker(fecha);
        _caducidadVacia = visible is null;
        _ajustandoFecha = true;
        try
        {
            FechaCaducidadMovimiento.Date = visible ?? FechaCalendario.ParaPickerObligatorio(null);
        }
        catch (Exception)
        {
        }
        finally
        {
            _ajustandoFecha = false;
        }
    }

    private (Guid? Id, string Lote, DateTimeOffset? Fecha, bool Automatico) DatosPartidaFormulario()
    {
        if (ComboPartidaMovimiento.SelectedItem is OpcionPartidaMovimiento { EsAutomatico: true })
        {
            return (null, string.Empty, null, true);
        }

        var lote = CajaLoteMovimiento.Text?.Trim() ?? string.Empty;
        var fecha = _caducidadVacia ? null : FechaCaducidadMovimiento.Date;
        Guid? id = ComboPartidaMovimiento.SelectedItem is OpcionPartidaMovimiento { EsNueva: false } opcion
            ? opcion.Id
            : null;
        return (id, lote, fecha, false);
    }

    private void ActualizarFormularioMovimiento()
    {
        if (TituloFormularioMovimiento is null || CajaCantidad is null || BotonRegistrarMovimiento is null)
        {
            return;
        }

        var salida = EsSalidaSeleccionada();
        var editando = _movimientoEnEdicion is not null;
        TituloFormularioMovimiento.Text = editando
            ? (salida ? "Editar una salida" : "Editar un ingreso")
            : (salida ? "Registrar una salida" : "Registrar un ingreso");
        CajaCantidad.PlaceholderText = salida ? "Cantidad de salida" : "Cantidad ingresada";
        BotonRegistrarMovimiento.Content = editando
            ? "Guardar cambios"
            : (salida ? "Registrar salida" : "Registrar ingreso");
        if (BotonCancelarEdicion is not null)
        {
            BotonCancelarEdicion.Visibility = editando ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private bool EsSalidaSeleccionada() =>
        ComboTipoMovimiento.SelectedItem as string == "Salida";

    private async void AdjuntarRecibo_Click(object sender, RoutedEventArgs e)
    {
        var selector = new FileOpenPicker();
        VentanaHelper.AsociarSelector(selector);
        VentanaHelper.ConfigurarInicio(selector);
        selector.FileTypeFilter.Add(".jpg");
        selector.FileTypeFilter.Add(".jpeg");
        selector.FileTypeFilter.Add(".png");
        selector.FileTypeFilter.Add(".bmp");
        selector.FileTypeFilter.Add(".webp");
        selector.FileTypeFilter.Add(".pdf");

        var archivo = await selector.PickSingleFileAsync();
        if (archivo is null)
        {
            return;
        }

        try
        {
            var temporal = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{Path.GetExtension(archivo.Name)}");
            await using (var origen = await archivo.OpenStreamForReadAsync())
            await using (var destino = File.Create(temporal))
            {
                await origen.CopyToAsync(destino);
            }

            QuitarArchivoPendiente();
            _reciboPendiente = temporal;
            _reciboNombre = archivo.Name;
            _quitarReciboExistente = false;
            TextoRecibo.Text = $"Recibo de este movimiento: {archivo.Name}";
            BotonQuitarRecibo.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo adjuntar el recibo: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void QuitarRecibo_Click(object sender, RoutedEventArgs e)
    {
        if (_movimientoEnEdicion is not null)
        {
            _quitarReciboExistente = true;
        }

        LimpiarReciboPendiente();
    }

    private void LimpiarReciboPendiente()
    {
        QuitarArchivoPendiente();
        _reciboPendiente = null;
        _reciboNombre = string.Empty;
        if (TextoRecibo is not null)
        {
            TextoRecibo.Text = "Sin recibo adjunto a este movimiento.";
        }

        if (BotonQuitarRecibo is not null)
        {
            BotonQuitarRecibo.Visibility = Visibility.Collapsed;
        }
    }

    private void QuitarArchivoPendiente()
    {
        if (_reciboPendiente is not null && File.Exists(_reciboPendiente))
        {
            try
            {
                File.Delete(_reciboPendiente);
            }
            catch
            {
            }
        }
    }

    private void RegistrarIngreso_Click(object sender, RoutedEventArgs e)
    {
        if (_fichaSeleccionada is not Guid id)
        {
            return;
        }

        var cantidad = CajaCantidad.Value;
        if (double.IsNaN(cantidad) || cantidad <= 0)
        {
            Mostrar("Indique una cantidad mayor a cero.", InfoBarSeverity.Warning);
            return;
        }

        var tipoDocumento = ComboTipoDocumento.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(tipoDocumento))
        {
            Mostrar("Elija si el documento es boleta o factura.", InfoBarSeverity.Warning);
            return;
        }

        var numero = CajaNumeroDocumento.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(numero))
        {
            Mostrar("Indique el número de boleta o factura.", InfoBarSeverity.Warning);
            return;
        }

        var original = App.Instance.Repositorio.Obtener(id);
        if (original is null)
        {
            Mostrar("El producto ya no está en el catálogo.", InfoBarSeverity.Error);
            return;
        }

        var salida = EsSalidaSeleccionada();
        var (partidaId, lote, fecha, automatico) = DatosPartidaFormulario();
        var ficha = original.Clonar();
        PartidasInventario.Normalizar(ficha);
        var tipo = salida ? "Salida" : "Ingreso";
        var nota = CajaNota.Text?.Trim() ?? string.Empty;

        if (_movimientoEnEdicion is Guid idMovimiento)
        {
            GuardarEdicionMovimiento(original, ficha, idMovimiento, cantidad, tipo, tipoDocumento, numero, nota, partidaId, lote, fecha, automatico);
            return;
        }

        List<MovimientoInventario> movimientos;
        if (salida)
        {
            if (!PartidasInventario.IntentarSalidaFefo(
                    ficha, cantidad, partidaId, lote, fecha, automatico, out var consumos, out var error)
                || consumos.Count == 0)
            {
                Mostrar(error ?? "No hay existencia suficiente.", InfoBarSeverity.Warning);
                return;
            }

            movimientos = CrearMovimientosSalida(ficha, consumos, tipoDocumento, numero, nota);
        }
        else
        {
            var partida = PartidasInventario.AplicarIngreso(ficha, lote, fecha, cantidad, partidaId);
            movimientos =
            [
                new MovimientoInventario
                {
                    FichaId = ficha.Id,
                    PartidaId = partida.Id,
                    Lote = partida.Lote,
                    FechaCaducidad = partida.FechaCaducidad,
                    Area = ficha.Area,
                    Fecha = DateTimeOffset.Now,
                    Cantidad = cantidad,
                    ExistenciaResultante = ficha.Existencia,
                    Tipo = tipo,
                    Origen = tipo,
                    TipoDocumento = tipoDocumento,
                    NumeroDocumento = numero,
                    Nota = nota
                }
            ];
        }

        if (_reciboPendiente is not null && File.Exists(_reciboPendiente))
        {
            var primero = movimientos[0];
            primero.RutaRecibo = ReciboMovimientoService.Guardar(
                primero.Id,
                _reciboPendiente,
                Path.GetExtension(_reciboNombre));
            primero.NombreRecibo = _reciboNombre;
        }

        App.Instance.Repositorio.GuardarFicha(ficha, registrarIngresoAutomatico: false);
        App.Instance.Registro.Agregar(movimientos);
        CajaNota.Text = string.Empty;
        CajaNumeroDocumento.Text = string.Empty;
        CajaCantidad.Value = 1;
        LimpiarReciboPendiente();
        var detalleLotes = movimientos.Count > 1
            ? $" en {movimientos.Count} lotes (primero el de caducidad más próxima)"
            : string.Empty;
        Mostrar($"Se registró una {tipo.ToLowerInvariant()} de {cantidad.ToString("0.##", Cultura)}{detalleLotes} ({tipoDocumento} {numero}).", InfoBarSeverity.Success);
        ConstruirChips();
        RefrescarLista();
    }

    private static List<MovimientoInventario> CrearMovimientosSalida(
        FichaTecnica ficha,
        List<PartidasInventario.ConsumoPartida> consumos,
        string tipoDocumento,
        string numero,
        string nota)
    {
        var ahora = DateTimeOffset.Now;
        var resultante = ficha.Existencia + consumos.Sum(c => c.Cantidad);
        var lista = new List<MovimientoInventario>();
        foreach (var consumo in consumos)
        {
            resultante -= consumo.Cantidad;
            lista.Add(new MovimientoInventario
            {
                FichaId = ficha.Id,
                PartidaId = consumo.Partida.Id,
                Lote = consumo.Partida.Lote,
                FechaCaducidad = consumo.Partida.FechaCaducidad,
                Area = ficha.Area,
                Fecha = ahora,
                Cantidad = consumo.Cantidad,
                ExistenciaResultante = resultante,
                Tipo = "Salida",
                Origen = "Salida",
                TipoDocumento = tipoDocumento,
                NumeroDocumento = numero,
                Nota = nota
            });
        }

        return lista;
    }

    private void GuardarEdicionMovimiento(
        FichaTecnica original,
        FichaTecnica ficha,
        Guid idMovimiento,
        double cantidad,
        string tipo,
        string tipoDocumento,
        string numero,
        string nota,
        Guid? partidaId,
        string lote,
        DateTimeOffset? fecha,
        bool automatico)
    {
        var actual = App.Instance.Registro.Obtener(idMovimiento);
        if (actual is null || actual.FichaId != original.Id)
        {
            Mostrar("Ese movimiento ya no está en el historial.", InfoBarSeverity.Error);
            CancelarEdicion();
            return;
        }

        PartidasInventario.Revertir(ficha, actual);
        PartidaInventario? partida;
        if (string.Equals(tipo, "Salida", StringComparison.OrdinalIgnoreCase))
        {
            if (!PartidasInventario.IntentarSalidaFefo(
                    ficha, cantidad, partidaId, lote, fecha, automatico, out var consumos, out var errorPartida)
                || consumos.Count == 0)
            {
                Mostrar(errorPartida ?? "No hay existencia suficiente.", InfoBarSeverity.Warning);
                return;
            }

            if (consumos.Count > 1)
            {
                Mostrar("Al editar una salida elija un lote concreto, o registre de nuevo para descontar varios lotes por caducidad.", InfoBarSeverity.Warning);
                return;
            }

            partida = consumos[0].Partida;
            cantidad = consumos[0].Cantidad;
        }
        else
        {
            partida = PartidasInventario.AplicarIngreso(ficha, lote, fecha, cantidad, partidaId);
        }

        var actualizado = new MovimientoInventario
        {
            Id = actual.Id,
            FichaId = actual.FichaId,
            PartidaId = partida.Id,
            Lote = partida.Lote,
            FechaCaducidad = partida.FechaCaducidad,
            Area = ficha.Area,
            Fecha = actual.Fecha,
            Cantidad = cantidad,
            ExistenciaResultante = ficha.Existencia,
            Tipo = tipo,
            Origen = tipo,
            TipoDocumento = tipoDocumento,
            NumeroDocumento = numero,
            Nota = nota,
            RutaRecibo = actual.RutaRecibo,
            NombreRecibo = actual.NombreRecibo
        };

        if (_reciboPendiente is not null && File.Exists(_reciboPendiente))
        {
            actualizado.RutaRecibo = ReciboMovimientoService.Guardar(
                actualizado.Id,
                _reciboPendiente,
                Path.GetExtension(_reciboNombre));
            actualizado.NombreRecibo = _reciboNombre;
        }
        else if (_quitarReciboExistente)
        {
            ReciboMovimientoService.Eliminar(actualizado.Id);
            actualizado.RutaRecibo = string.Empty;
            actualizado.NombreRecibo = string.Empty;
        }

        if (!App.Instance.Registro.IntentarMutar(
                ficha.Id,
                original.Existencia,
                lista =>
                {
                    var i = lista.FindIndex(m => m.Id == idMovimiento);
                    if (i >= 0)
                    {
                        lista[i] = actualizado;
                    }
                },
                out var existencia,
                out var error))
        {
            Mostrar(error ?? "No se pudo guardar el movimiento.", InfoBarSeverity.Warning);
            return;
        }

        ficha.Existencia = existencia;
        PartidasInventario.PrepararParaGuardar(ficha);
        App.Instance.Repositorio.GuardarFicha(ficha, registrarIngresoAutomatico: false);
        CancelarEdicion();
        Mostrar($"Se actualizó la {tipo.ToLowerInvariant()} ({tipoDocumento} {numero}).", InfoBarSeverity.Success);
        ConstruirChips();
        RefrescarLista();
    }

    private void ModoEditarMovimientos_Click(object sender, RoutedEventArgs e)
    {
        _modoEditarMovimientos = BotonModoEditarMovimientos.IsChecked == true;
        var marca = _modoEditarMovimientos ? Visibility.Visible : Visibility.Collapsed;
        foreach (var item in _movimientosVisibles.Where(m => m.EsMarcable))
        {
            item.VisibilidadMarca = marca;
        }

        if (!_modoEditarMovimientos)
        {
            foreach (var item in _movimientosVisibles)
            {
                item.EstaMarcada = false;
            }

            _marcadasMovimientos.Clear();
        }

        ActualizarAccionesMovimientos();
    }

    private void ListaHistorial_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_ignorarClicHistorial)
        {
            _ignorarClicHistorial = false;
            return;
        }

        if (e.ClickedItem is not MovimientoRegistroItem item || !item.EsMarcable)
        {
            return;
        }

        if (SeleccionListaFichas.DebeMarcarSinAbrir(_modoEditarMovimientos))
        {
            SeleccionListaFichas.AplicarClic(_movimientosVisibles, _marcadasMovimientos, item, ref _anclaMovimientos);
            ActualizarAccionesMovimientos();
        }
    }

    private void MarcaMovimiento_Tapped(object sender, TappedRoutedEventArgs e) =>
        e.Handled = true;

    private void MarcaMovimiento_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox caja)
        {
            return;
        }

        _ignorarClicHistorial = true;
        var item = caja.DataContext as MovimientoRegistroItem
                   ?? _movimientosVisibles.FirstOrDefault(m => Equals(caja.Tag, m.Id));
        if (item is null || !item.EsMarcable)
        {
            return;
        }

        item.EstaMarcada = caja.IsChecked == true;
        if (item.EstaMarcada)
        {
            _marcadasMovimientos.Add(item.Id);
        }
        else
        {
            _marcadasMovimientos.Remove(item.Id);
        }

        ActualizarAccionesMovimientos();
    }

    private void EditarMovimiento_Click(object sender, RoutedEventArgs e)
    {
        var ids = _movimientosVisibles.Where(m => m.EsMarcable && m.EstaMarcada).Select(m => m.Id).ToList();
        if (ids.Count != 1)
        {
            Mostrar("Marque un solo movimiento para editarlo.", InfoBarSeverity.Warning);
            return;
        }

        var actual = App.Instance.Registro.Obtener(ids[0]);
        if (actual is null)
        {
            Mostrar("Ese movimiento ya no está en el historial.", InfoBarSeverity.Error);
            return;
        }

        _movimientoEnEdicion = actual.Id;
        _quitarReciboExistente = false;
        QuitarArchivoPendiente();
        _reciboPendiente = null;
        _reciboNombre = string.Empty;
        ComboTipoMovimiento.SelectedItem = actual.EsSalida ? "Salida" : "Ingreso";
        CajaCantidad.Value = actual.Cantidad;
        ComboTipoDocumento.SelectedItem = string.IsNullOrWhiteSpace(actual.TipoDocumento) ? "Boleta" : actual.TipoDocumento;
        CajaNumeroDocumento.Text = actual.NumeroDocumento;
        CajaNota.Text = actual.Nota;
        if (_fichaSeleccionada is Guid fichaId && App.Instance.Repositorio.Obtener(fichaId) is { } ficha)
        {
            CargarOpcionesPartida(ficha, actual.PartidaId);
        }

        CajaLoteMovimiento.Text = actual.Lote;
        AsignarFechaCalendario(actual.FechaCaducidad);
        if (!string.IsNullOrWhiteSpace(actual.NombreRecibo) || ReciboMovimientoService.RutaEfectiva(actual) is not null)
        {
            TextoRecibo.Text = $"Recibo de este movimiento: {(string.IsNullOrWhiteSpace(actual.NombreRecibo) ? "archivo adjunto" : actual.NombreRecibo)}";
            BotonQuitarRecibo.Visibility = Visibility.Visible;
        }
        else
        {
            TextoRecibo.Text = "Sin recibo adjunto a este movimiento.";
            BotonQuitarRecibo.Visibility = Visibility.Collapsed;
        }

        ActualizarFormularioMovimiento();
        Mostrar("Edite los datos abajo y pulse Guardar cambios.", InfoBarSeverity.Informational);
    }

    private async void BorrarMovimientos_Click(object sender, RoutedEventArgs e)
    {
        var ids = _movimientosVisibles.Where(m => m.EsMarcable && m.EstaMarcada).Select(m => m.Id).ToList();
        if (ids.Count == 0)
        {
            Mostrar("Marque al menos un movimiento para borrarlo.", InfoBarSeverity.Warning);
            return;
        }

        if (_fichaSeleccionada is not Guid fichaId || App.Instance.Repositorio.Obtener(fichaId) is not { } original)
        {
            return;
        }

        var dialogo = new ContentDialog
        {
            Title = ids.Count == 1 ? "Borrar movimiento" : "Borrar movimientos",
            Content = ids.Count == 1
                ? "Se eliminará el ingreso o la salida marcado y se recalculará la existencia."
                : $"Se eliminarán {ids.Count} movimientos y se recalculará la existencia.",
            PrimaryButtonText = "Borrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var conjunto = ids.ToHashSet();
        var ficha = original.Clonar();
        PartidasInventario.Normalizar(ficha);
        foreach (var movimiento in App.Instance.Registro.PorFicha(fichaId).Where(m => conjunto.Contains(m.Id)))
        {
            PartidasInventario.Revertir(ficha, movimiento);
        }

        if (!App.Instance.Registro.IntentarMutar(
                fichaId,
                original.Existencia,
                lista => lista.RemoveAll(m => conjunto.Contains(m.Id)),
                out var existencia,
                out var error))
        {
            Mostrar(error ?? "No se pudieron borrar los movimientos.", InfoBarSeverity.Warning);
            return;
        }

        ficha.Existencia = existencia;
        PartidasInventario.PrepararParaGuardar(ficha);
        App.Instance.Repositorio.GuardarFicha(ficha, registrarIngresoAutomatico: false);
        foreach (var id in ids)
        {
            _marcadasMovimientos.Remove(id);
        }

        if (_movimientoEnEdicion is Guid enEdicion && conjunto.Contains(enEdicion))
        {
            CancelarEdicion();
        }

        Mostrar(ids.Count == 1 ? "Se borró el movimiento." : $"Se borraron {ids.Count} movimientos.", InfoBarSeverity.Success);
        ConstruirChips();
        RefrescarLista();
    }

    private async void BorrarLotes_Click(object sender, RoutedEventArgs e)
    {
        if (_fichaSeleccionada is not Guid fichaId || App.Instance.Repositorio.Obtener(fichaId) is not { } original)
        {
            Mostrar("Elija un producto para borrar sus lotes.", InfoBarSeverity.Warning);
            return;
        }

        var ficha = original.Clonar();
        PartidasInventario.Normalizar(ficha);
        if (ficha.Partidas.Count == 0)
        {
            Mostrar("Este producto no tiene lotes para borrar.", InfoBarSeverity.Warning);
            return;
        }

        var casillas = new List<CheckBox>();
        var lista = new StackPanel { Spacing = 8 };
        lista.Children.Add(new TextBlock
        {
            Opacity = 0.78,
            Text = "Marque los lotes que desea quitar. Su cantidad se descuenta de la existencia; si había stock, se registra una salida.",
            TextWrapping = TextWrapping.Wrap
        });
        foreach (var partida in ficha.Partidas
                     .OrderBy(p => p.FechaCaducidad ?? DateTimeOffset.MaxValue)
                     .ThenBy(p => p.Lote, StringComparer.CurrentCultureIgnoreCase))
        {
            var unidad = string.IsNullOrWhiteSpace(ficha.UnidadMedida) ? "" : $" {ficha.UnidadMedida}";
            var caja = new CheckBox
            {
                Content = $"{partida.LoteTexto} · {partida.FechaCaducidadTexto} · {partida.Cantidad.ToString("0.##", Cultura)}{unidad}",
                IsChecked = true,
                Tag = partida.Id
            };
            casillas.Add(caja);
            lista.Children.Add(caja);
        }

        var dialogo = new ContentDialog
        {
            Title = "Borrar lotes",
            Content = new ScrollViewer
            {
                Content = lista,
                MaxHeight = 360,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            },
            PrimaryButtonText = "Borrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var ids = casillas
            .Where(c => c.IsChecked == true && c.Tag is Guid)
            .Select(c => (Guid)c.Tag)
            .ToHashSet();
        if (ids.Count == 0)
        {
            Mostrar("Marque al menos un lote para borrarlo.", InfoBarSeverity.Warning);
            return;
        }

        var quitados = PartidasInventario.Quitar(ficha, ids);
        if (quitados == 0)
        {
            Mostrar("No se pudo quitar ningún lote.", InfoBarSeverity.Warning);
            return;
        }

        App.Instance.Repositorio.GuardarFicha(
            ficha,
            "Borrado de lotes",
            quitados == 1
                ? "Se borró un lote del producto."
                : $"Se borraron {quitados} lotes del producto.",
            registrarIngresoAutomatico: true);
        Mostrar(
            quitados == 1 ? "Se borró el lote." : $"Se borraron {quitados} lotes.",
            InfoBarSeverity.Success);
        ConstruirChips();
        RefrescarLista();
    }

    private void CancelarEdicion_Click(object sender, RoutedEventArgs e) => CancelarEdicion();

    private void CancelarEdicion()
    {
        _movimientoEnEdicion = null;
        _quitarReciboExistente = false;
        if (CajaNota is null)
        {
            return;
        }

        CajaNota.Text = string.Empty;
        CajaNumeroDocumento.Text = string.Empty;
        CajaCantidad.Value = 1;
        ComboTipoMovimiento.SelectedIndex = 0;
        ComboTipoDocumento.SelectedIndex = 0;
        if (CajaLoteMovimiento is not null)
        {
            CajaLoteMovimiento.Text = string.Empty;
        }

        AsignarFechaCalendario(null);

        LimpiarReciboPendiente();
        ActualizarFormularioMovimiento();
    }

    private void ActualizarAccionesMovimientos()
    {
        if (BotonEditarMovimiento is null)
        {
            return;
        }

        var marcadas = _movimientosVisibles.Count(m => m.EsMarcable && m.EstaMarcada);
        BotonEditarMovimiento.IsEnabled = marcadas == 1;
        BotonBorrarMovimientos.IsEnabled = marcadas > 0;
    }

    private void ActualizarBotonBorrarLotes(FichaTecnica? ficha)
    {
        if (BotonBorrarLotes is null)
        {
            return;
        }

        BotonBorrarLotes.IsEnabled = ficha is not null && ficha.Partidas.Count > 0;
    }

    private void Mostrar(string mensaje, InfoBarSeverity severidad)
    {
        BarraAviso.Severity = severidad;
        BarraAviso.Message = mensaje;
        BarraAviso.IsOpen = true;
    }

    private readonly record struct FiltroUnidad(Guid? Area, bool SinArea);
}

public sealed class OpcionPartidaMovimiento
{
    public Guid Id { get; init; }
    public bool EsNueva { get; init; }
    public bool EsAutomatico { get; init; }
    public string Lote { get; init; } = string.Empty;
    public DateTimeOffset? Fecha { get; init; }
    public double Disponible { get; init; }
    public required string Texto { get; init; }

    public override string ToString() => Texto;
}

public sealed class ProductoRegistroItem : INotifyPropertyChanged, IItemMarcable
{
    private bool _estaMarcada;

    public Guid Id { get; init; }
    public required FichaTecnica Ficha { get; init; }
    public required string Codigo { get; init; }
    public required string Nombre { get; init; }
    public required string Resumen { get; init; }
    public required string TotalIngresos { get; init; }
    public required string TotalSalidas { get; init; }
    public required string UltimoIngreso { get; init; }

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

public sealed class MovimientoRegistroItem : INotifyPropertyChanged, IItemMarcable
{
    private bool _estaMarcada;
    private Visibility _visibilidadMarca = Visibility.Collapsed;

    public Guid Id { get; init; }
    public bool EsMarcable { get; init; }
    public required string FechaTexto { get; init; }
    public required string Origen { get; init; }
    public string Nota { get; init; } = string.Empty;
    public string ReciboTexto { get; init; } = string.Empty;
    public required string CantidadTexto { get; init; }
    public required string ResultadoTexto { get; init; }
    public Visibility VisibilidadIngreso { get; init; } = Visibility.Collapsed;

    public Visibility VisibilidadSalida { get; init; } = Visibility.Collapsed;

    public Visibility VisibilidadNeutro { get; init; } = Visibility.Collapsed;

    public Visibility VisibilidadMarca
    {
        get => _visibilidadMarca;
        set
        {
            if (_visibilidadMarca == value)
            {
                return;
            }

            _visibilidadMarca = value;
            OnPropertyChanged();
        }
    }

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

    public string DetalleTexto =>
        string.Join(" · ", new[] { Origen, Nota, ReciboTexto }.Where(p => !string.IsNullOrWhiteSpace(p)));

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? nombre = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
}
