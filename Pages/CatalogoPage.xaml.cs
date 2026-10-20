using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas.Pages;

public sealed partial class CatalogoPage : Page, IVistaInventario
{
    private readonly HashSet<Guid> _marcadas = [];
    private List<FichaListaItem> _visibles = [];
    private bool _ignorarClicLista;
    private int _anclaSeleccion = -1;
    private bool _modoSeleccionar;
    private bool _actualizandoFiltros;
    private List<OpcionFiltroCatalogo> _opcionesArea = [];
    private readonly DispatcherTimer _relojBusqueda = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private Guid? _fichaNavegada;

    public CatalogoPage()
    {
        InitializeComponent();
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
        ComboCaducidad.ItemsSource = FiltrosCatalogo.OpcionesCaducidad;
        AplicarNavegacion(e.Parameter);
        _actualizandoFiltros = false;
        Refrescar();
        ResaltarFichaNavegada();
    }

    private void AplicarNavegacion(object? parameter)
    {
        var stock = FiltroStockCatalogo.Todos;
        var caducidad = FiltroCaducidadCatalogo.Todas;
        object? fichaParametro = parameter;
        if (parameter is CatalogoNavegacion nav)
        {
            stock = nav.Stock;
            caducidad = nav.Caducidad;
            fichaParametro = nav.FichaId;
        }

        ComboStock.SelectedIndex = (int)stock;
        ComboCaducidad.SelectedIndex = (int)caducidad;
        PrepararFichaNavegada(fichaParametro);
    }

    private void PrepararFichaNavegada(object? parameter)
    {
        _fichaNavegada = parameter is Guid id ? id : null;
        if (_fichaNavegada is not Guid fichaId || App.Instance.Repositorio.Obtener(fichaId) is not { } ficha)
        {
            return;
        }

        CajaBusqueda.Text = string.IsNullOrWhiteSpace(ficha.Codigo) ? ficha.Nombre : ficha.Codigo;
        _relojBusqueda.Stop();
    }

    private void ResaltarFichaNavegada()
    {
        if (_fichaNavegada is not Guid id)
        {
            return;
        }

        var item = _visibles.FirstOrDefault(v => v.Ficha.Id == id);
        if (item is null)
        {
            return;
        }

        ListaCatalogo.ScrollIntoView(item);
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
        AnimacionLista.Revelar(ListaCatalogo);
    }

    private bool _actualizandoBase;

    private async void Actualizar_Click(object sender, RoutedEventArgs e)
    {
        if (_actualizandoBase)
        {
            return;
        }

        _actualizandoBase = true;
        BotonActualizar.IsEnabled = false;
        ResumenLista.Text = "Actualizando la base de datos…";
        try
        {
            var aviso = await InventarioNube.ActualizarAsync();
            Refrescar();
            ResumenLista.Text = aviso;
        }
        finally
        {
            _actualizandoBase = false;
            BotonActualizar.IsEnabled = true;
        }
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
        _visibles = OrdenFichas.Aplicar(fichas, criterio)
            .Select(f => new FichaListaItem
            {
                Ficha = f,
                EstaMarcada = _marcadas.Contains(f.Id)
            })
            .ToList();
        ListaCatalogo.ItemsSource = _visibles;
        _anclaSeleccion = -1;
        ActualizarResumenMarcas();
        ActualizarChips();
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

    private void MenuMoverArea_Opening(object sender, object e)
    {
        if (sender is not MenuFlyout menu)
        {
            return;
        }

        menu.Items.Clear();
        foreach (var area in AreasOperativas.Todas)
        {
            var destino = area.Id;
            var item = new MenuFlyoutItem { Text = area.Nombre };
            item.Click += (_, _) => MoverMarcadas(destino);
            menu.Items.Add(item);
        }

        var sinArea = new MenuFlyoutItem { Text = "Sin área" };
        sinArea.Click += (_, _) => MoverMarcadas(null);
        menu.Items.Add(sinArea);
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
        ChipStockCritico.IsChecked = ComboStock.SelectedIndex == (int)FiltroStockCatalogo.Critico;
        ChipVencidos.IsChecked = ComboCaducidad.SelectedIndex == (int)FiltroCaducidadCatalogo.Vencidas;
        ChipProximos.IsChecked = ComboCaducidad.SelectedIndex == (int)FiltroCaducidadCatalogo.Proximas;

        var todas = App.Instance.Repositorio.Todas;
        var repo = App.Instance.Repositorio;
        PanelChipsArea.Children.Clear();
        for (var i = 1; i < _opcionesArea.Count; i++)
        {
            var opcion = _opcionesArea[i];
            var n = opcion.SinArea ? repo.Contar(null) : repo.Contar(opcion.Id);
            var chip = new ToggleButton
            {
                Tag = i,
                IsChecked = ComboArea.SelectedIndex == i,
                Content = new TextBlock { Text = $"{opcion.Texto} ({n})" }
            };
            chip.Click += ChipArea_Click;
            PanelChipsArea.Children.Add(chip);
        }

        var vencidos = repo.PartidasVencidas().Count();
        var proximos = repo.PartidasProximas(30).Count();
        TextoChipStock.Text = $"Stock crítico ({todas.Count(f => f.EstaStockCritico)})";
        TextoChipVencidos.Text = $"Vencidos ({vencidos})";
        TextoChipProximos.Text = $"Próximos 30 días ({proximos})";
        _actualizandoFiltros = false;
    }

    private void ListaCatalogo_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_ignorarClicLista)
        {
            _ignorarClicLista = false;
            return;
        }

        if (e.ClickedItem is not FichaListaItem item)
        {
            return;
        }

        if (SeleccionListaFichas.DebeMarcarSinAbrir(_modoSeleccionar))
        {
            SeleccionListaFichas.AplicarClic(_visibles, _marcadas, item, ref _anclaSeleccion);
            ActualizarResumenMarcas();
            return;
        }

        App.Instance.MainAppWindow?.IrAArea(item.Ficha.Area, item.Ficha.Id);
    }

    private void ModoSeleccionar_Click(object sender, RoutedEventArgs e)
    {
        _modoSeleccionar = BotonModoSeleccionar.IsChecked == true;
        TextoModoSeleccionar.Text = _modoSeleccionar
            ? "Seleccionando"
            : "Seleccionar";
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

    private void ListaCatalogo_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
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

    private void MarcaFicha_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e) =>
        e.Handled = true;

    private void ActualizarResumenMarcas()
    {
        var marcadas = _visibles.Count(f => f.EstaMarcada);
        ResumenLista.Text = marcadas == 0
            ? $"{_visibles.Count} ficha(s) con los filtros actuales. Pulse un recuento o use los desplegables para acotar."
            : $"{_visibles.Count} ficha(s), {marcadas} seleccionada(s). Puede exportar, borrar o cambiar de área los seleccionados.";
    }

    private void MarcaFicha_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox caja)
        {
            return;
        }

        var item = caja.DataContext as FichaListaItem
                   ?? _visibles.FirstOrDefault(f => Equals(caja.Tag, f.Ficha.Id));
        if (item is null)
        {
            return;
        }

        item.EstaMarcada = caja.IsChecked == true;
        if (item.EstaMarcada)
        {
            _marcadas.Add(item.Ficha.Id);
        }
        else
        {
            _marcadas.Remove(item.Ficha.Id);
        }

        ActualizarResumenMarcas();
    }

    private async void ImportarExcel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var resultado = await ExcelUi.ImportarAsync();
            if (resultado is null)
            {
                return;
            }

            Refrescar();
            await Aviso("Importación de Excel", resultado.Mensaje);
        }
        catch (Exception ex)
        {
            await Aviso("No se pudo importar", ex.Message);
        }
    }

    private async void MoverMarcadas(Guid? destino)
    {
        var seleccionadas = _visibles.Where(f => f.EstaMarcada).Select(f => f.Ficha).ToList();
        if (seleccionadas.Count == 0)
        {
            await Aviso("Cambiar área", "Marque al menos un producto de la lista para cambiarlo de área.");
            return;
        }

        var aMover = seleccionadas.Where(f => !Equals(f.Area, destino)).ToList();
        if (aMover.Count == 0)
        {
            await Aviso("Cambiar área", "Los productos seleccionados ya están en esa área.");
            return;
        }

        var repo = App.Instance.Repositorio;
        var prefijo = Catalogos.PrefijoCodigo(destino);
        var vivas = aMover.Select(f => repo.Obtener(f.Id) ?? f).ToList();
        foreach (var ficha in vivas)
        {
            repo.PrepararMovimientoDeArea(ficha, destino);
            _marcadas.Remove(ficha.Id);
        }

        repo.GuardarFichas(vivas);

        Refrescar();
        await Aviso(
            "Cambiar área",
            vivas.Count == 1
                ? $"El producto se movió a {Catalogos.Titulo(destino)} con código {vivas[0].Codigo}."
                : $"{vivas.Count} productos se movieron a {Catalogos.Titulo(destino)} con códigos nuevos {prefijo}_…");
    }

    private async void EnviarMerma_Click(object sender, RoutedEventArgs e)
    {
        var seleccionadas = _visibles.Where(f => f.EstaMarcada).Select(f => f.Ficha).ToList();
        if (seleccionadas.Count == 0)
        {
            await Aviso("Enviar a merma", "Marque al menos un producto del inventario.");
            return;
        }

        var sinStock = seleccionadas.Where(f => f.Existencia <= 0.0001).Select(f => f.Nombre).ToList();
        if (sinStock.Count == seleccionadas.Count)
        {
            await Aviso("Enviar a merma", "Los productos marcados no tienen existencia.");
            return;
        }

        Frame.Navigate(typeof(MermaEnvioPage), seleccionadas.Select(f => f.Id).ToList());
    }

    private async void EliminarSeleccionadas_Click(object sender, RoutedEventArgs e)
    {
        var seleccionadas = _visibles.Where(f => f.EstaMarcada).Select(f => f.Ficha).ToList();
        if (seleccionadas.Count == 0)
        {
            await Aviso("Borrar seleccionados", "Marque al menos un producto de la lista para borrarlo.");
            return;
        }

        var resumen = seleccionadas.Count == 1
            ? $"Se eliminará «{seleccionadas[0].Nombre}» ({seleccionadas[0].Codigo})."
            : $"Se eliminarán {seleccionadas.Count} productos del catálogo.";
        var dialogo = new ContentDialog
        {
            Title = "Borrar productos seleccionados",
            Content = resumen,
            PrimaryButtonText = "Borrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await AprobacionBorradoInventario.InterceptarAsync(XamlRoot, seleccionadas))
        {
            return;
        }

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        foreach (var ficha in seleccionadas)
        {
            ImagenFichaService.EliminarDeFicha(ficha.Id);
            _marcadas.Remove(ficha.Id);
        }

        App.Instance.Repositorio.EliminarVarios(seleccionadas.Select(f => f.Id));

        Refrescar();
        await Aviso(
            "Borrar seleccionados",
            seleccionadas.Count == 1
                ? "El producto se eliminó del catálogo."
                : $"{seleccionadas.Count} productos se eliminaron del catálogo.");
    }

    private async void ExportarConsolidado_Click(object sender, RoutedEventArgs e)
    {
        var seleccionadas = _visibles.Where(f => f.EstaMarcada).Select(f => f.Ficha).ToList();
        if (seleccionadas.Count == 0)
        {
            await Aviso("Consolidado", "Marque al menos una ficha de la lista para exportar.");
            return;
        }

        try
        {
            seleccionadas = (await InventarioNube.CargarCompletasAsync(seleccionadas)).ToList();
            var resultado = await ExcelUi.ExportarConsolidadoAsync(seleccionadas);
            if (resultado is not null)
            {
                await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
            }
        }
        catch (Exception ex)
        {
            await Aviso("Exportar", $"No se pudo exportar: {ex.Message}");
        }
    }

    private async void ExportarFicha_Click(object sender, RoutedEventArgs e)
    {
        var seleccionadas = _visibles.Where(f => f.EstaMarcada).Select(f => f.Ficha).ToList();
        if (seleccionadas.Count != 1)
        {
            await Aviso(
                "Exportar ficha",
                "Marque una sola ficha para exportarla, o pulse Exportar en su fila.");
            return;
        }

        await ExportarIndividualAsync(seleccionadas[0]);
    }

    private async void ExportarFila_Click(object sender, RoutedEventArgs e)
    {
        _ignorarClicLista = true;
        if (sender is not FrameworkElement elemento)
        {
            return;
        }

        var ficha = (elemento.DataContext as FichaListaItem)?.Ficha
                    ?? _visibles.FirstOrDefault(f => Equals(elemento.Tag, f.Ficha.Id))?.Ficha;
        if (ficha is null)
        {
            return;
        }

        await ExportarIndividualAsync(ficha);
    }

    private async Task ExportarIndividualAsync(FichaTecnica ficha)
    {
        try
        {
            ficha = (await InventarioNube.CargarCompletaAsync(ficha.Id)) ?? ficha;
            var resultado = await ExcelUi.ExportarFichaAsync(ficha);
            if (resultado is not null)
            {
                await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
            }
        }
        catch (Exception ex)
        {
            await Aviso("Exportar", $"No se pudo exportar: {ex.Message}");
        }
    }

    private async void Plantilla_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (await ExcelUi.GuardarPlantillaAsync())
            {
                await Aviso("Plantilla Excel", "Se guardó la plantilla con los productos que hay ahora en el inventario.");
            }
        }
        catch (Exception ex)
        {
            await Aviso("Plantilla Excel", $"No se pudo guardar la plantilla: {ex.Message}");
        }
    }

    private async Task Aviso(string titulo, string mensaje)
    {
        var dialogo = new ContentDialog
        {
            Title = titulo,
            Content = mensaje,
            CloseButtonText = "Aceptar",
            XamlRoot = XamlRoot
        };
        await dialogo.ShowAsync();
    }
}

public sealed record OpcionFiltroCatalogo(string Texto, Guid? Id, bool SinArea);
