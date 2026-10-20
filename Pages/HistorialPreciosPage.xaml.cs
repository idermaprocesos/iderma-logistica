using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas.Pages;

public sealed partial class HistorialPreciosPage : Page, IVistaInventario
{
    private List<FichaPrecioListaItem> _visibles = [];
    private List<OpcionFiltroCatalogo> _opcionesArea = [];
    private bool _actualizandoFiltros;
    private Guid? _fichaSeleccionada;
    private Guid? _precioEnEdicion;
    private bool _ajustandoMonto;
    private bool _montoEditado;
    private bool _ignorarClicLista;
    private readonly DispatcherTimer _relojBusqueda = new() { Interval = TimeSpan.FromMilliseconds(180) };

    public HistorialPreciosPage()
    {
        InitializeComponent();
        CajaMonto.ValueChanged += (_, _) =>
        {
            if (!_ajustandoMonto)
            {
                _montoEditado = true;
            }
        };
        _relojBusqueda.Tick += (_, _) =>
        {
            _relojBusqueda.Stop();
            Refrescar();
        };
        Unloaded += (_, _) => _relojBusqueda.Stop();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _actualizandoFiltros = true;
        CargarOpcionesArea();
        ComboOrden.ItemsSource = OrdenFichas.Opciones.Select(o => o.Texto).ToList();
        ComboOrden.SelectedIndex = OrdenFichas.IndicePredeterminado;
        ComboStock.ItemsSource = FiltrosCatalogo.OpcionesStock;
        ComboStock.SelectedIndex = 0;
        ComboCaducidad.ItemsSource = FiltrosCatalogo.OpcionesCaducidad;
        ComboCaducidad.SelectedIndex = 0;
        ComboMoneda.ItemsSource = new[]
        {
            MonedaPrecio.Etiqueta(MonedaPrecio.Soles),
            MonedaPrecio.Etiqueta(MonedaPrecio.Dolares)
        };
        ComboMoneda.SelectedIndex = 0;
        FechaPrecio.Date = FechaCalendario.ParaPickerObligatorio(DateTimeOffset.Now);
        PrepararFichaNavegada(e.Parameter);
        _actualizandoFiltros = false;
        Refrescar();
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

    private void Filtros_Changed(object sender, TextChangedEventArgs e)
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

        Refrescar();
    }

    private void ChipArea_Click(object sender, RoutedEventArgs e)
    {
        if (_actualizandoFiltros || sender is not ToggleButton chip)
        {
            return;
        }

        var indice = int.TryParse(chip.Tag?.ToString(), out var n) ? n : 0;
        ComboArea.SelectedIndex = chip.IsChecked == true ? indice : 0;
    }

    private void ChipStock_Click(object sender, RoutedEventArgs e)
    {
        if (_actualizandoFiltros || sender is not ToggleButton chip)
        {
            return;
        }

        ComboStock.SelectedIndex = chip.IsChecked == true
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
        ComboArea.SelectedIndex = 0;
        ComboStock.SelectedIndex = 0;
        ComboCaducidad.SelectedIndex = 0;
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
        Refrescar();
    }

    public void AplicarCambioInventario()
    {
        _actualizandoFiltros = true;
        CargarOpcionesArea();
        _actualizandoFiltros = false;
        Refrescar();
        AnimacionLista.Revelar(ListaProductos);
        AnimacionLista.Revelar(ListaPrecios);
    }

    private void Refrescar()
    {
        if (ComboOrden.SelectedIndex < 0 || ComboStock.SelectedIndex < 0 || ComboCaducidad.SelectedIndex < 0)
        {
            return;
        }

        var filtro = CajaBusqueda.Text?.Trim() ?? string.Empty;
        var (area, sinArea) = FiltroAreaActual();
        var origenArea = App.Instance.Repositorio.Buscar(string.Empty, area, sinArea);
        RellenarListas(origenArea);

        var fichas = App.Instance.Repositorio.Buscar(filtro, area, sinArea)
            .Where(f => FiltrosCatalogo.Coincide(
                f,
                (FiltroStockCatalogo)ComboStock.SelectedIndex,
                (FiltroCaducidadCatalogo)ComboCaducidad.SelectedIndex,
                ComboCategoria.SelectedItem as string,
                ComboProveedor.SelectedItem as string,
                ComboUbicacion.SelectedItem as string,
                ComboLote.SelectedItem as string));
        var criterio = OrdenFichas.Opciones[ComboOrden.SelectedIndex].Criterio;
        var precios = App.Instance.HistorialPrecios;
        _visibles = OrdenFichas.Aplicar(fichas, criterio)
            .Select(f =>
            {
                var ultimo = precios.Ultimo(f.Id);
                return new FichaPrecioListaItem
                {
                    Ficha = f,
                    UltimoPrecioTexto = ultimo is null
                        ? "Sin precio"
                        : MonedaPrecio.Formato(ultimo.Monto, ultimo.Moneda)
                };
            })
            .ToList();
        _ignorarClicLista = true;
        ListaProductos.ItemsSource = _visibles;
        ResumenLista.Text = _visibles.Count == 1
            ? "1 producto. Elija uno para ver su historial de precios."
            : $"{_visibles.Count} productos. Elija uno para ver su historial de precios.";
        ActualizarChips();

        if (_fichaSeleccionada is Guid id)
        {
            var item = _visibles.FirstOrDefault(v => v.Ficha.Id == id);
            if (item is null)
            {
                LimpiarSeleccion();
            }
            else
            {
                ListaProductos.SelectedItem = item;
                ListaProductos.ScrollIntoView(item);
                MostrarProducto(item.Ficha, cargarNube: false);
            }
        }

        _ignorarClicLista = false;
    }

    private void CargarOpcionesArea()
    {
        var anterior = ComboArea.SelectedIndex >= 0 && ComboArea.SelectedIndex < _opcionesArea.Count
            ? _opcionesArea[ComboArea.SelectedIndex]
            : null;
        _opcionesArea =
        [
            new("Todas las áreas", null, false)
        ];
        _opcionesArea.AddRange(AreasOperativas.Todas.Select(a => new OpcionFiltroCatalogo(a.Nombre, a.Id, false)));
        _opcionesArea.Add(new("Sin área", null, true));
        ComboArea.ItemsSource = _opcionesArea.Select(o => o.Texto).ToList();
        var indice = anterior is null
            ? 0
            : _opcionesArea.FindIndex(o => o.SinArea == anterior.SinArea && o.Id == anterior.Id);
        ComboArea.SelectedIndex = indice >= 0 ? indice : 0;
    }

    private (Guid? Area, bool SinArea) FiltroAreaActual()
    {
        if (ComboArea.SelectedIndex <= 0 || ComboArea.SelectedIndex >= _opcionesArea.Count)
        {
            return (null, false);
        }

        var opcion = _opcionesArea[ComboArea.SelectedIndex];
        return (opcion.Id, opcion.SinArea);
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

    private void ActualizarChips()
    {
        _actualizandoFiltros = true;
        var todas = App.Instance.Repositorio.Todas;
        var repo = App.Instance.Repositorio;
        var vencidos = repo.PartidasVencidas().Count();
        var proximos = repo.PartidasProximas(30).Count();

        PanelFiltrosChips.Children.Clear();
        for (var i = 1; i < _opcionesArea.Count; i++)
        {
            var opcion = _opcionesArea[i];
            var n = opcion.SinArea ? repo.Contar(null) : repo.Contar(opcion.Id);
            PanelFiltrosChips.Children.Add(CrearChip(
                $"{opcion.Texto} ({n})",
                i,
                ComboArea.SelectedIndex == i,
                ChipArea_Click));
        }

        PanelFiltrosChips.Children.Add(CrearChip(
            $"Stock crítico ({todas.Count(f => f.EstaStockCritico)})",
            "stock",
            ComboStock.SelectedIndex == (int)FiltroStockCatalogo.Critico,
            ChipStock_Click));
        PanelFiltrosChips.Children.Add(CrearChip(
            $"Vencidos ({vencidos})",
            "1",
            ComboCaducidad.SelectedIndex == (int)FiltroCaducidadCatalogo.Vencidas,
            ChipCaducidad_Click));
        PanelFiltrosChips.Children.Add(CrearChip(
            $"Próximos 30 días ({proximos})",
            "2",
            ComboCaducidad.SelectedIndex == (int)FiltroCaducidadCatalogo.Proximas,
            ChipCaducidad_Click));
        var botonLimpiar = new Button
        {
            Content = "Limpiar filtros",
            Padding = new Thickness(10, 5, 10, 5),
            MinWidth = 0,
            MinHeight = 0
        };
        botonLimpiar.Click += LimpiarFiltros_Click;
        PanelFiltrosChips.Children.Add(botonLimpiar);
        _actualizandoFiltros = false;
    }

    private static ToggleButton CrearChip(string texto, object tag, bool marcado, RoutedEventHandler click)
    {
        var chip = new ToggleButton
        {
            Tag = tag,
            IsChecked = marcado,
            Padding = new Thickness(10, 5, 10, 5),
            MinWidth = 0,
            MinHeight = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = new TextBlock { Text = texto, TextWrapping = TextWrapping.NoWrap }
        };
        chip.Click += click;
        return chip;
    }

    private void ListaProductos_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not FichaPrecioListaItem item)
        {
            return;
        }

        if (_ignorarClicLista)
        {
            _ignorarClicLista = false;
            return;
        }

        _fichaSeleccionada = item.Ficha.Id;
        ResetearFormulario();
        MostrarProducto(item.Ficha, cargarNube: true);
    }

    private void MostrarProducto(FichaTecnica ficha, bool cargarNube)
    {
        try
        {
            PanelVacio.Visibility = Visibility.Collapsed;
            PanelProducto.Visibility = Visibility.Visible;
            PanelFormulario.Visibility = Visibility.Visible;
            ListaPrecios.Visibility = Visibility.Visible;
            TituloProducto.Text = string.IsNullOrWhiteSpace(ficha.Nombre) ? ficha.Codigo : ficha.Nombre;
            SubtituloProducto.Text = string.Join(" · ", new[] { ficha.Codigo, ficha.AreaTitulo, ficha.Resumen }
                .Where(t => !string.IsNullOrWhiteSpace(t)));
            CargarPrecios(ficha.Id);
            if (cargarNube)
            {
                _ = CompletarHistorialAsync(ficha);
            }
        }
        catch (Exception ex)
        {
            MostrarAviso($"No se pudo abrir el historial: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async Task CompletarHistorialAsync(FichaTecnica ficha)
    {
        var cola = DispatcherQueue;
        bool cambio;
        try
        {
            cambio = await ModulosNube.CompletarPreciosAsync(ficha.Id);
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
                CargarPrecios(ficha.Id);
            }
        });
    }

    private void CargarPrecios(Guid fichaId)
    {
        var historial = App.Instance.HistorialPrecios.DeFicha(fichaId);
        var cronologico = historial.OrderBy(p => p.Fecha).ThenBy(p => p.Id).ToList();
        ListaPrecios.ItemsSource = historial
            .Select(p =>
            {
                var indice = cronologico.FindIndex(c => c.Id == p.Id);
                return new PrecioProductoItem
                {
                    Precio = p,
                    VariacionTexto = indice >= 0
                        ? HistorialPreciosExportService.TextoVariacion(cronologico, indice)
                        : "—"
                };
            })
            .ToList();
        ActualizarResumenPrecio(historial);
    }

    private void ActualizarResumenPrecio(IReadOnlyList<PrecioProducto> historial)
    {
        var actual = historial.FirstOrDefault();
        if (actual is null)
        {
            TextoPrecioActual.Text = "Sin precio";
            TextoVariacionPrecio.Text = string.Empty;
            TextoVariacionPrecio.Foreground = TextoPrecioActual.Foreground;
            PanelPrecioInicial.Visibility = Visibility.Collapsed;
            return;
        }

        TextoPrecioActual.Text = MonedaPrecio.Formato(actual.Monto, actual.Moneda);
        var moneda = MonedaPrecio.Normalizar(actual.Moneda);
        var mismaMoneda = historial
            .Where(p => MonedaPrecio.Normalizar(p.Moneda) == moneda)
            .OrderBy(p => p.Fecha)
            .ThenBy(p => p.Id)
            .ToList();
        var anterior = historial.Skip(1).FirstOrDefault(p => MonedaPrecio.Normalizar(p.Moneda) == moneda);
        PintarVariacion(TextoVariacionPrecio, actual, anterior, historial.Count == 1 ? "Primer registro" : "Sin % comparable");

        var inicial = mismaMoneda.FirstOrDefault();
        if (inicial is null)
        {
            PanelPrecioInicial.Visibility = Visibility.Collapsed;
            return;
        }

        PanelPrecioInicial.Visibility = Visibility.Visible;
        TextoPrecioInicial.Text = MonedaPrecio.Formato(inicial.Monto, inicial.Moneda);
        PintarVariacion(TextoVariacionInicial, actual, inicial, "Sin cambio desde el inicio");
    }

    private static void PintarVariacion(TextBlock destino, PrecioProducto actual, PrecioProducto? referencia, string sinDato)
    {
        if (referencia is null || referencia.Monto <= 0 || referencia.Id == actual.Id)
        {
            destino.Text = sinDato;
            destino.Foreground = new SolidColorBrush(Color.FromArgb(255, 154, 160, 166));
            return;
        }

        var porcentaje = (actual.Monto - referencia.Monto) / referencia.Monto * 100;
        var cultura = System.Globalization.CultureInfo.GetCultureInfo("es-PE");
        if (porcentaje < -0.05)
        {
            destino.Text = $"↓ {Math.Abs(porcentaje).ToString("N1", cultura)} %";
            destino.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 79));
        }
        else if (porcentaje > 0.05)
        {
            destino.Text = $"↑ {porcentaje.ToString("N1", cultura)} %";
            destino.Foreground = new SolidColorBrush(Color.FromArgb(255, 196, 43, 28));
        }
        else
        {
            destino.Text = "0 %";
            destino.Foreground = new SolidColorBrush(Color.FromArgb(255, 154, 160, 166));
        }
    }

    private void LimpiarSeleccion()
    {
        _fichaSeleccionada = null;
        _precioEnEdicion = null;
        ListaProductos.SelectedItem = null;
        PanelVacio.Visibility = Visibility.Visible;
        PanelProducto.Visibility = Visibility.Collapsed;
        PanelFormulario.Visibility = Visibility.Collapsed;
        ListaPrecios.Visibility = Visibility.Collapsed;
        ListaPrecios.ItemsSource = null;
        TextoPrecioActual.Text = "Sin precio";
        TextoVariacionPrecio.Text = string.Empty;
        PanelPrecioInicial.Visibility = Visibility.Collapsed;
    }

    private void ResetearFormulario()
    {
        _precioEnEdicion = null;
        TituloFormulario.Text = "Nuevo precio";
        FechaPrecio.Date = FechaCalendario.ParaPickerObligatorio(DateTimeOffset.Now);
        PonerMonto(null);
        ComboMoneda.SelectedIndex = 0;
        CajaNota.Text = string.Empty;
        AvisoPrecios.IsOpen = false;
    }

    private async void ExportarReporte_Click(object sender, RoutedEventArgs e)
    {
        if (_fichaSeleccionada is not Guid id || App.Instance.Repositorio.Obtener(id) is not { } ficha)
        {
            MostrarAviso("Elija un producto para exportar el reporte.", InfoBarSeverity.Warning);
            return;
        }

        var precios = App.Instance.HistorialPrecios.DeFicha(ficha.Id);
        if (precios.Count == 0)
        {
            MostrarAviso("Este producto aún no tiene precios para el reporte.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            var resultado = await ExcelUi.ExportarHistorialPreciosAsync(ficha, precios);
            if (resultado is null)
            {
                return;
            }

            MostrarAviso("Reporte de historial de precios generado.", InfoBarSeverity.Success);
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            MostrarAviso($"No se pudo exportar: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void PonerMonto(double? monto)
    {
        _ajustandoMonto = true;
        _montoEditado = monto is not null;
        CajaMonto.Value = monto ?? 0;
        _ajustandoMonto = false;
    }

    private void CancelarEdicion_Click(object sender, RoutedEventArgs e) => ResetearFormulario();

    private void GuardarPrecio_Click(object sender, RoutedEventArgs e)
    {
        if (_fichaSeleccionada is not Guid fichaId)
        {
            return;
        }

        if (!_montoEditado || CajaMonto.Value < 0)
        {
            MostrarAviso("Indique un monto válido.", InfoBarSeverity.Warning);
            return;
        }

        var moneda = ComboMoneda.SelectedIndex == 1 ? MonedaPrecio.Dolares : MonedaPrecio.Soles;
        var existente = _precioEnEdicion is Guid idEdicion
            ? App.Instance.HistorialPrecios.DeFicha(fichaId).FirstOrDefault(p => p.Id == idEdicion)
            : null;
        var precio = existente ?? new PrecioProducto { FichaId = fichaId };
        precio.FichaId = fichaId;
        precio.Fecha = FechaPrecio.Date;
        precio.Monto = CajaMonto.Value;
        precio.Moneda = moneda;
        precio.Nota = CajaNota.Text ?? string.Empty;
        App.Instance.HistorialPrecios.GuardarPrecio(precio);
        ResetearFormulario();
        Refrescar();
        MostrarAviso("Precio guardado.", InfoBarSeverity.Success);
    }

    private void EditarPrecio_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button boton || boton.Tag is not Guid id || _fichaSeleccionada is not Guid fichaId)
        {
            return;
        }

        var precio = App.Instance.HistorialPrecios.DeFicha(fichaId).FirstOrDefault(p => p.Id == id);
        if (precio is null)
        {
            return;
        }

        _precioEnEdicion = precio.Id;
        TituloFormulario.Text = "Editar precio";
        FechaPrecio.Date = FechaCalendario.ParaPickerObligatorio(precio.Fecha);
        PonerMonto(precio.Monto);
        ComboMoneda.SelectedIndex = MonedaPrecio.EsDolar(precio.Moneda) ? 1 : 0;
        CajaNota.Text = precio.Nota;
        AvisoPrecios.IsOpen = false;
    }

    private async void BorrarPrecio_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button boton || boton.Tag is not Guid id)
        {
            return;
        }

        var dialogo = new ContentDialog
        {
            Title = "Borrar precio",
            Content = "¿Quitar esta entrada del historial de precios?",
            PrimaryButtonText = "Borrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        App.Instance.HistorialPrecios.Eliminar(id);
        if (_precioEnEdicion == id)
        {
            ResetearFormulario();
        }

        Refrescar();
        MostrarAviso("Entrada borrada.", InfoBarSeverity.Success);
    }

    private void MostrarAviso(string mensaje, InfoBarSeverity severidad)
    {
        AvisoPrecios.Message = mensaje;
        AvisoPrecios.Severity = severidad;
        AvisoPrecios.IsOpen = true;
    }
}
