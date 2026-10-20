    using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Pages;
using IdermaFichas.Services;

namespace IdermaFichas;

public sealed partial class MainWindow : Window
{
    private Guid? _fichaPendiente;
    private object? _parametroPendiente;
    private object? _navegacionAnterior;
    private bool _navegandoInterno;
    private bool _cerrarConfirmado;
    private DispatcherQueueTimer? _relojBusquedaGlobal;
    private int _secuenciaBusquedaGlobal;
    private int _indiceTour = -1;
    private bool _tourEnCurso;
    private bool _avanzandoTour;
    private int _secuenciaTour;

    public MainWindow()
    {
        InitializeComponent();
        _relojBusquedaGlobal = DispatcherQueue.CreateTimer();
        _relojBusquedaGlobal.Interval = TimeSpan.FromMilliseconds(80);
        _relojBusquedaGlobal.IsRepeating = false;
        _relojBusquedaGlobal.Tick += RelojBusquedaGlobal_Tick;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        Iderma.Shell.BotonLauncher.Colocar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("img/logo.ico");
        if (AppWindow.Presenter is OverlappedPresenter presentador)
        {
            presentador.Maximize();
        }
        AppWindow.Closing += AppWindow_Closing;
        _navegacionAnterior = NavView.SelectedItem;
        ActualizarMenuAreas();
        NavFrame.Navigate(typeof(HomePage));
        App.Instance.Repositorio.InventarioExternoActualizado += (_, _) =>
            DispatcherQueue.TryEnqueue(RefrescarVistaInventario);
        InventarioNube.EstadoCambiado += () =>
            DispatcherQueue.TryEnqueue(RefrescarVistaInventario);
        ModulosNube.EstadoCambiado += () =>
            DispatcherQueue.TryEnqueue(RefrescarVistaInventario);
        ModulosNube.DatosCambiados += () =>
            DispatcherQueue.TryEnqueue(RefrescarVistaInventario);
        SolicitudesClinicaNube.NuevaPendiente += solicitud =>
            DispatcherQueue.TryEnqueue(() =>
            {
                ActualizarBadgeNotificaciones();
                AvisoWindowsSolicitudes.Mostrar(solicitud);
            });
        SolicitudesClinicaNube.Cambiado += () =>
            DispatcherQueue.TryEnqueue(ActualizarBadgeNotificaciones);
        SolicitudesIncidenciasNube.Cambiado += () =>
            DispatcherQueue.TryEnqueue(ActualizarBadgeNotificaciones);
        SolicitudesIncidenciasNube.NuevaPendiente += _ =>
            DispatcherQueue.TryEnqueue(ActualizarBadgeNotificaciones);
        AprobacionBorradoInventario.Cambiado += () =>
            DispatcherQueue.TryEnqueue(ActualizarBadgeNotificaciones);
        AreasOperativas.Cambiaron += () =>
            DispatcherQueue.TryEnqueue(AplicarAreasRemotas);
        Catalogos.Cambiaron += () =>
            DispatcherQueue.TryEnqueue(AplicarCatalogosRemotos);
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, AreasOperativasNube.Iniciar);
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, CatalogosNube.Iniciar);
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, IniciarCopiasTrasAbrir);
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            AvisoWindowsSolicitudes.Preparar(DispatcherQueue);
            SolicitudesClinicaNube.Iniciar(DispatcherQueue);
            SolicitudesIncidenciasNube.Iniciar(DispatcherQueue);
            AprobacionBorradoInventario.Iniciar(DispatcherQueue);
            ActualizarBadgeNotificaciones();
        });
    }

    private void RefrescarVistaInventario()
    {
        if (NavFrame.Content is IVistaInventario vista)
        {
            vista.AplicarCambioInventario();
        }
    }

    private void AplicarAreasRemotas()
    {
        ActualizarMenuAreas();
        RefrescarVistaInventario();
        if (NavFrame.Content is SettingsPage ajustes)
        {
            ajustes.RecargarAreas();
            ajustes.RecargarDesplegables();
        }
    }

    private void AplicarCatalogosRemotos()
    {
        if (NavFrame.Content is AreaPage area)
        {
            area.RecargarDesplegables();
        }

        if (NavFrame.Content is SettingsPage ajustes)
        {
            ajustes.RecargarDesplegables();
        }
    }

    private static void IniciarCopiasTrasAbrir()
    {
        try
        {
            App.Instance.Autoguardado.AplicarPreferencias();
        }
        catch
        {
        }
    }

    public object? PaginaActual => NavFrame.Content;

    public async void IrAArea(Guid? area, Guid? fichaId = null)
    {
        _fichaPendiente = fichaId;
        _parametroPendiente = null;
        await SeleccionarAsync(area is Guid id
            ? AreasOperativas.EtiquetaMenu(id)
            : AreasOperativas.EtiquetaSinArea);
    }

    public async void IrACaducidad(CaducidadNavegacion? destino = null)
    {
        _parametroPendiente = destino;
        await SeleccionarAsync("caducidad");
    }

    public async void IrACatalogo(Guid? fichaId = null)
    {
        _fichaPendiente = fichaId;
        _parametroPendiente = null;
        await SeleccionarAsync("catalogo");
    }

    public async void IrACatalogo(CatalogoNavegacion destino)
    {
        _fichaPendiente = destino.FichaId;
        _parametroPendiente = destino;
        await SeleccionarAsync("catalogo");
    }

    public async void IrAConfiguracion() => await SeleccionarAsync("configuracion");

    public async void IrANotificaciones() => await SeleccionarAsync("notificaciones");

    private void ActualizarBadgeNotificaciones()
    {
        var pendientes = SolicitudesClinicaNube.Pendientes
            + SolicitudesIncidenciasNube.Pendientes
            + AprobacionBorradoInventario.Pendientes;
        ItemNotificaciones.InfoBadge = pendientes > 0
            ? new InfoBadge { Value = pendientes }
            : null;
    }

    public async void IniciarTourTrabajadorNuevo()
    {
        _secuenciaTour++;
        _tourEnCurso = true;
        _indiceTour = 0;
        NavView.IsPaneOpen = true;
        await MostrarPasoTourAsync();
    }

    public void ActualizarMenuAreas()
    {
        var items = NavView.MenuItems;
        var indiceEncabezado = -1;
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is NavigationViewItemHeader { Content: string texto } &&
                texto.Equals("Áreas operativas", StringComparison.CurrentCultureIgnoreCase))
            {
                indiceEncabezado = i;
                break;
            }
        }

        if (indiceEncabezado < 0)
        {
            return;
        }

        var seleccionado = (NavView.SelectedItem as NavigationViewItem)?.Tag as string;
        var quitar = indiceEncabezado + 1;
        while (quitar < items.Count && EsItemArea(items[quitar]))
        {
            items.RemoveAt(quitar);
        }

        var insertar = indiceEncabezado + 1;
        foreach (var area in AreasOperativas.Todas)
        {
            items.Insert(insertar++, CrearItemArea(area.Nombre, AreasOperativas.EtiquetaMenu(area.Id), area.Glifo));
        }

        items.Insert(insertar, CrearItemArea("Sin área", AreasOperativas.EtiquetaSinArea, "\uE8F1"));

        if (!string.IsNullOrEmpty(seleccionado))
        {
            foreach (var elemento in items.OfType<NavigationViewItem>())
            {
                if (Equals(elemento.Tag, seleccionado))
                {
                    _navegandoInterno = true;
                    NavView.SelectedItem = elemento;
                    _navegandoInterno = false;
                    break;
                }
            }
        }
    }

    private static bool EsItemArea(object elemento) =>
        elemento is NavigationViewItem { Tag: string etiqueta } &&
        (etiqueta.StartsWith(AreasOperativas.PrefijoEtiqueta, StringComparison.OrdinalIgnoreCase) ||
         etiqueta is "clinica" or "oficina" or "limpieza" ||
         etiqueta == AreasOperativas.EtiquetaSinArea);

    private static NavigationViewItem CrearItemArea(string nombre, string etiqueta, string glifo) =>
        new()
        {
            Content = nombre,
            Tag = etiqueta,
            Icon = new FontIcon { Glyph = GlifosWinUi.Caracter(glifo) }
        };

    private async Task SeleccionarAsync(string etiqueta)
    {
        if (etiqueta == "configuracion")
        {
            if (!await ConfirmarEdicionPendienteAsync())
            {
                return;
            }

            NavView.SelectedItem = NavView.SettingsItem;
            return;
        }

        foreach (var elemento in RecorrerMenu(NavView.MenuItems))
        {
            if (!Equals(elemento.Tag, etiqueta))
            {
                continue;
            }

            if (Equals(NavView.SelectedItem, elemento))
            {
                if (!await ConfirmarEdicionPendienteAsync())
                {
                    return;
                }

                Navegar(etiqueta);
                return;
            }

            NavView.SelectedItem = elemento;
            return;
        }
    }

    private static IEnumerable<NavigationViewItem> RecorrerMenu(IList<object> items)
    {
        foreach (var item in items.OfType<NavigationViewItem>())
        {
            yield return item;
            foreach (var hijo in RecorrerMenu(item.MenuItems))
            {
                yield return hijo;
            }
        }
    }

    private void CajaBusquedaGlobal_TextChanged(object sender, TextChangedEventArgs e)
    {
        _relojBusquedaGlobal?.Stop();
        if (string.IsNullOrWhiteSpace(CajaBusquedaGlobal.Text))
        {
            OcultarBusquedaGlobal();
            return;
        }

        _relojBusquedaGlobal?.Start();
    }

    private void RelojBusquedaGlobal_Tick(DispatcherQueueTimer sender, object args)
    {
        ActualizarSugerenciasBusqueda(CajaBusquedaGlobal.Text);
    }

    private void NavView_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (PopupBusquedaGlobal.IsOpen)
        {
            OcultarBusquedaGlobal();
        }
    }

    private void ListaBusquedaGlobal_GettingFocus(UIElement sender, GettingFocusEventArgs args)
    {
        args.TryCancel();
    }

    private async void CajaBusquedaGlobal_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            OcultarBusquedaGlobal();
            e.Handled = true;
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Down && ListaBusquedaGlobal.Items.Count > 0)
        {
            ListaBusquedaGlobal.SelectedIndex = 0;
            e.Handled = true;
            return;
        }

        if (e.Key != Windows.System.VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        _relojBusquedaGlobal?.Stop();
        ActualizarSugerenciasBusqueda(CajaBusquedaGlobal.Text);
        if (ListaBusquedaGlobal.Items.OfType<ResultadoBusquedaGlobal>().FirstOrDefault() is { } primero)
        {
            await AbrirResultadoAsync(primero);
        }
    }

    private async void ListaBusquedaGlobal_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ResultadoBusquedaGlobal resultado)
        {
            await AbrirResultadoAsync(resultado);
        }
    }

    private void ActualizarSugerenciasBusqueda(string texto)
    {
        var secuencia = ++_secuenciaBusquedaGlobal;
        IReadOnlyList<ResultadoBusquedaGlobal> resultados;
        try
        {
            resultados = BusquedaGlobalService.Buscar(texto);
        }
        catch
        {
            resultados = [];
        }

        if (secuencia != _secuenciaBusquedaGlobal)
        {
            return;
        }

        ListaBusquedaGlobal.ItemsSource = resultados;
        if (resultados.Count > 0)
        {
            MostrarBusquedaGlobal();
        }
        else
        {
            PopupBusquedaGlobal.IsOpen = false;
        }
    }

    private void MostrarBusquedaGlobal()
    {
        var ancho = CajaBusquedaGlobal.ActualWidth > 200 ? CajaBusquedaGlobal.ActualWidth : 520;
        PanelBusquedaGlobal.Width = ancho;
        if (!PopupBusquedaGlobal.IsOpen)
        {
            try
            {
                var punto = CajaBusquedaGlobal.TransformToVisual(RaizVentana)
                    .TransformPoint(new Windows.Foundation.Point(0, CajaBusquedaGlobal.ActualHeight + 6));
                PopupBusquedaGlobal.HorizontalOffset = punto.X;
                PopupBusquedaGlobal.VerticalOffset = punto.Y;
            }
            catch
            {
                PopupBusquedaGlobal.HorizontalOffset = Math.Max(0, (RaizVentana.ActualWidth - ancho) / 2);
                PopupBusquedaGlobal.VerticalOffset = 52;
            }

            PopupBusquedaGlobal.IsOpen = true;
        }
    }

    private void OcultarBusquedaGlobal()
    {
        PopupBusquedaGlobal.IsOpen = false;
        ListaBusquedaGlobal.ItemsSource = null;
    }

    private async Task AbrirResultadoAsync(ResultadoBusquedaGlobal resultado)
    {
        CajaBusquedaGlobal.Text = string.Empty;
        OcultarBusquedaGlobal();

        _fichaPendiente = resultado.FichaId;
        if (resultado.Destino == "producto")
        {
            IrAArea(resultado.AreaId, resultado.FichaId);
            return;
        }

        await SeleccionarAsync(resultado.Destino);
    }

    private Guid? ConsumirFichaPendiente()
    {
        var id = _fichaPendiente;
        _fichaPendiente = null;
        return id;
    }

    private object? ConsumirParametroPendiente()
    {
        var parametro = _parametroPendiente;
        _parametroPendiente = null;
        return parametro;
    }

    private object? ConsumirNavegacionCatalogo()
    {
        if (ConsumirParametroPendiente() is CatalogoNavegacion nav)
        {
            _fichaPendiente = null;
            return nav;
        }

        return ConsumirFichaPendiente();
    }

    private object? ConsumirNavegacionCaducidad()
    {
        if (ConsumirParametroPendiente() is CaducidadNavegacion nav)
        {
            _fichaPendiente = null;
            return nav;
        }

        var ficha = ConsumirFichaPendiente();
        return ficha is Guid id ? new CaducidadNavegacion(id) : null;
    }

    private static VistaLecturaInventario? VistaDe(string etiqueta) => etiqueta switch
    {
        "catalogo" => VistaLecturaInventario.Inventario,
        "caducidad" => VistaLecturaInventario.Vencimientos,
        "registro" => VistaLecturaInventario.Registro,
        "historial-precios" => VistaLecturaInventario.Precios,
        _ => null
    };

    private void Navegar(string etiqueta)
    {
        if (VistaDe(etiqueta) is VistaLecturaInventario vista)
        {
            InventarioNube.Iniciar(App.Instance.Repositorio, vista);
        }

        ModulosNube.AbrirMenu(etiqueta);
        switch (etiqueta)
        {
            case "inicio":
                NavFrame.Navigate(typeof(HomePage));
                break;
            case "catalogo":
                NavFrame.Navigate(typeof(CatalogoPage), ConsumirNavegacionCatalogo());
                break;
            case "caducidad":
                NavFrame.Navigate(typeof(CaducidadPage), ConsumirNavegacionCaducidad());
                break;
            case "registro": 
                NavFrame.Navigate(typeof(RegistroPage), ConsumirFichaPendiente());
                break;
            case "historial-precios":
                NavFrame.Navigate(typeof(HistorialPreciosPage), ConsumirFichaPendiente());
                break;
            case "orden-compra":
                ModulosNube.AbrirMenu("proveedores");
                NavFrame.Navigate(typeof(OrdenCompraPage));
                break;
            case "orden-servicio":
                ModulosNube.AbrirMenu("proveedores");
                NavFrame.Navigate(typeof(OrdenServicioPage));
                break;
            case "proveedores":
                NavFrame.Navigate(typeof(ProveedoresPage));
                break;
            case "notificaciones":
                NavFrame.Navigate(typeof(NotificacionesPage));
                break;
            case "merma":
                NavFrame.Navigate(typeof(MermaPage));
                break;
            case "mantenimiento":
                NavFrame.Navigate(typeof(MantenimientoPage));
                break;
            case "rh-colaboradores":
                NavFrame.Navigate(typeof(RhColaboradoresPage));
                break;
            case "rh-horarios":
                NavFrame.Navigate(typeof(RhHorariosPage));
                break;
            case "rh-tardanzas":
                NavFrame.Navigate(typeof(RhTardanzasPage));
                break;
            case "rh-riesgo":
                NavFrame.Navigate(typeof(RhRiesgoPage));
                break;
            case "rh-faltas":
                NavFrame.Navigate(typeof(RhFaltasPage), false);
                break;
            case "rh-faltas-justificadas":
                NavFrame.Navigate(typeof(RhFaltasPage), true);
                break;
            case "etiquetas":
                NavFrame.Navigate(typeof(EtiquetasPage));
                break;
            case "recetas":
                NavFrame.Navigate(typeof(RecetasPage));
                break;
            case "dash-reporte":
                NavFrame.Navigate(typeof(DashboardPage), new DashboardNavegacion(
                    "REPORTE DE EVALUACIÓN DE ÁREAS IDERMA",
                    "https://docs.google.com/spreadsheets/d/1MNRwYuXhlj1it1MaaCif2YGCuPdIce7sq1yOm2hmiR4/edit?rm=minimal#gid=2140873895"));
                break;
            case "dash-inventario":
                NavFrame.Navigate(typeof(DashboardPage), new DashboardNavegacion(
                    "INVENTARIO IDERMA",
                    "https://docs.google.com/spreadsheets/d/1dpERk5YaeFIu1Rinj-JzY1_m9x-81mZBOXlCN8pSGPk/edit?rm=minimal#gid=1812462040"));
                break;
            case "acerca":
                NavFrame.Navigate(typeof(AcercaDePage));
                break;
        }
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private async void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (!NavFrame.CanGoBack)
        {
            return;
        }

        if (!await ConfirmarEdicionPendienteAsync())
        {
            return;
        }

        NavFrame.GoBack();
    }

    private async void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_navegandoInterno)
        {
            return;
        }

        if (!await ConfirmarEdicionPendienteAsync())
        {
            _navegandoInterno = true;
            NavView.SelectedItem = _navegacionAnterior;
            _navegandoInterno = false;
            return;
        }

        _navegacionAnterior = NavView.SelectedItem;

        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem item && item.Tag is string etiqueta)
        {
            if (etiqueta == AreasOperativas.EtiquetaSinArea ||
                etiqueta.StartsWith(AreasOperativas.PrefijoEtiqueta, StringComparison.OrdinalIgnoreCase) ||
                etiqueta is "clinica" or "oficina" or "limpieza")
            {
                InventarioNube.Iniciar(App.Instance.Repositorio, VistaLecturaInventario.Area);
                NavFrame.Navigate(
                    typeof(AreaPage),
                    new AreaNavegacion(AreasOperativas.IdDesdeEtiqueta(etiqueta), ConsumirFichaPendiente()));
                return;
            }

            Navegar(etiqueta);
        }
    }

    private async Task<bool> ConfirmarEdicionPendienteAsync()
    {
        if (_tourEnCurso)
        {
            return true;
        }

        return NavFrame.Content is not IEdicionProducto edicion || await edicion.ConfirmarSalidaAsync();
    }

    private async void TipTour_ActionButtonClick(TeachingTip sender, object args)
    {
        if (!_tourEnCurso || _avanzandoTour)
        {
            return;
        }

        if (_indiceTour >= TourTrabajadorNuevo.Pasos.Count - 1)
        {
            TerminarTour();
            return;
        }

        _indiceTour++;
        await MostrarPasoTourAsync();
    }

    private void TipTour_CloseButtonClick(TeachingTip sender, object args)
    {
        if (_avanzandoTour)
        {
            return;
        }

        TerminarTour();
    }

    private void TerminarTour()
    {
        _tourEnCurso = false;
        _indiceTour = -1;
        _secuenciaTour++;
        _avanzandoTour = false;
        if (TipTour.IsOpen)
        {
            TipTour.IsOpen = false;
        }
    }

    private async Task MostrarPasoTourAsync()
    {
        if (!_tourEnCurso || _indiceTour < 0 || _indiceTour >= TourTrabajadorNuevo.Pasos.Count)
        {
            TerminarTour();
            return;
        }

        var secuencia = ++_secuenciaTour;
        var indice = _indiceTour;
        var paso = TourTrabajadorNuevo.Pasos[indice];
        _avanzandoTour = true;
        try
        {
            await CerrarTipTourAsync();
            if (secuencia != _secuenciaTour || !_tourEnCurso)
            {
                return;
            }

            TipTour.Target = null;

            NavView.IsPaneOpen = true;
            if (paso.ExpandirDashboards)
            {
                ItemDashboards.IsExpanded = true;
                NavView.Expand(ItemDashboards);
            }

            var destino = ResolverDestinoTour(paso);
            if (!string.IsNullOrEmpty(destino) && !EstaEnDestinoTour(destino))
            {
                await SeleccionarAsync(destino);
                await EsperarCargaPaginaAsync();
            }

            await Task.Delay(120);
            if (secuencia != _secuenciaTour || !_tourEnCurso)
            {
                return;
            }

            var objetivo = ResolverObjetivoTour(paso) ?? AnclaTour;
            if (objetivo.ActualWidth <= 0 || objetivo.ActualHeight <= 0)
            {
                objetivo.UpdateLayout();
            }

            objetivo.StartBringIntoView(new BringIntoViewOptions
            {
                AnimationDesired = false,
                VerticalAlignmentRatio = 0.2
            });
            await Task.Delay(80);
            if (secuencia != _secuenciaTour || !_tourEnCurso)
            {
                return;
            }

            if (!EsObjetivoTourUsable(objetivo))
            {
                objetivo = AnclaTour;
            }

            TipTour.Title = paso.Titulo;
            TipTour.Subtitle = paso.Texto;
            TipTour.PreferredPlacement = ColocacionTour(paso, objetivo);
            TipTour.ActionButtonContent = indice >= TourTrabajadorNuevo.Pasos.Count - 1
                ? "Terminar"
                : "Siguiente";
            TipTour.CloseButtonContent = "Salir";
            TipTour.Target = objetivo;
            await Task.Delay(16);
            if (secuencia != _secuenciaTour || !_tourEnCurso)
            {
                return;
            }

            TipTour.IsOpen = true;
        }
        catch
        {
            if (secuencia == _secuenciaTour && _tourEnCurso)
            {
                TipTour.Title = paso.Titulo;
                TipTour.Subtitle = paso.Texto;
                TipTour.Target = AnclaTour;
                TipTour.PreferredPlacement = TeachingTipPlacementMode.Bottom;
                TipTour.ActionButtonContent = "Siguiente";
                TipTour.CloseButtonContent = "Salir";
                TipTour.IsOpen = true;
            }
        }
        finally
        {
            if (secuencia == _secuenciaTour)
            {
                _avanzandoTour = false;
            }
        }
    }

    private async Task CerrarTipTourAsync()
    {
        if (!TipTour.IsOpen)
        {
            return;
        }

        var cerrado = new TaskCompletionSource();
        void AlCerrar(TeachingTip sender, TeachingTipClosedEventArgs args)
        {
            sender.Closed -= AlCerrar;
            cerrado.TrySetResult();
        }

        TipTour.Closed += AlCerrar;
        TipTour.IsOpen = false;
        var timeout = Task.Delay(700);
        if (await Task.WhenAny(cerrado.Task, timeout) == timeout)
        {
            TipTour.Closed -= AlCerrar;
        }
    }

    private static TeachingTipPlacementMode ColocacionTour(PasoTour paso, FrameworkElement objetivo)
    {
        if (ReferenceEquals(objetivo, null))
        {
            return TeachingTipPlacementMode.Bottom;
        }

        if (paso.Objetivo is ObjetivoTour.ItemMenu or ObjetivoTour.ItemConfiguracion)
        {
            return TeachingTipPlacementMode.Right;
        }

        if (paso.Nombre == "CajaBusquedaGlobal")
        {
            return TeachingTipPlacementMode.Bottom;
        }

        return TeachingTipPlacementMode.Bottom;
    }

    private static bool EsObjetivoTourUsable(FrameworkElement? elemento)
    {
        if (elemento is null || !elemento.IsLoaded || elemento.Visibility != Visibility.Visible)
        {
            return false;
        }

        if (elemento is NavigationViewItem)
        {
            return true;
        }

        return elemento.ActualWidth >= 8 && elemento.ActualHeight >= 8;
    }

    private string? ResolverDestinoTour(PasoTour paso)
    {
        if (paso.Destino == "primera-area" || paso.Nombre == "primera-area")
        {
            var area = AreasOperativas.Todas.FirstOrDefault();
            return area is null
                ? AreasOperativas.EtiquetaSinArea
                : AreasOperativas.EtiquetaMenu(area.Id);
        }

        return paso.Destino;
    }

    private bool EstaEnDestinoTour(string destino)
    {
        if (destino == "configuracion")
        {
            return NavFrame.Content is SettingsPage;
        }

        return (NavView.SelectedItem as NavigationViewItem)?.Tag as string == destino;
    }

    private FrameworkElement? ResolverObjetivoTour(PasoTour paso)
    {
        FrameworkElement? objetivo = paso.Objetivo switch
        {
            ObjetivoTour.ItemConfiguracion => NavView.SettingsItem as FrameworkElement,
            ObjetivoTour.ItemMenu => BuscarItemMenu(paso.Nombre),
            ObjetivoTour.ElementoVentana => paso.Nombre switch
            {
                "NavView" => ItemInicio,
                "CajaBusquedaGlobal" => CajaBusquedaGlobal,
                _ => null
            },
            ObjetivoTour.ElementoPagina => paso.Nombre is null
                ? null
                : (NavFrame.Content as FrameworkElement)?.FindName(paso.Nombre) as FrameworkElement,
            _ => null
        };

        return EsObjetivoTourUsable(objetivo) ? objetivo : null;
    }

    private FrameworkElement? BuscarItemMenu(string? nombre)
    {
        if (string.IsNullOrEmpty(nombre))
        {
            return null;
        }

        if (nombre == "primera-area")
        {
            nombre = ResolverDestinoTour(new PasoTour(string.Empty, string.Empty, "primera-area", ObjetivoTour.ItemMenu, "primera-area"));
        }

        foreach (var item in RecorrerMenu(NavView.MenuItems))
        {
            if (!Equals(item.Tag, nombre))
            {
                continue;
            }

            return item;
        }

        return null;
    }

    private async Task EsperarCargaPaginaAsync()
    {
        for (var i = 0; i < 30; i++)
        {
            if (NavFrame.Content is FrameworkElement { IsLoaded: true, ActualHeight: > 0 })
            {
                break;
            }

            await Task.Delay(40);
        }

        await Task.Delay(150);
    }

    public void CerrarParaActualizar()
    {
        _cerrarConfirmado = true;
        Close();
    }

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_cerrarConfirmado || NavFrame.Content is not IEdicionProducto edicion || !edicion.HayCambiosPendientes)
        {
            return;
        }

        args.Cancel = true;
        if (await edicion.ConfirmarSalidaAsync())
        {
            _cerrarConfirmado = true;
            Close();
        }
    }
}
