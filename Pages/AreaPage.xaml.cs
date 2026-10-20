using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas.Pages;

public sealed partial class AreaPage : Page, IEdicionProducto, IVistaInventario
{
    private static readonly JsonSerializerOptions JsonHuella = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(), new AreaIdJsonConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private Guid? _area;
    private Guid? _fichaInicial;
    private FichaTecnica? _enEdicion;
    private string _huellaOriginal = string.Empty;
    private string _huellaVigilada = string.Empty;
    private bool _actualizandoLista;
    private bool _usarAreaAlGuardar;
    private Guid? _areaAlGuardar;
    private bool _alertaSuciaVisible;
    private readonly DispatcherTimer _relojSucio = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly DispatcherTimer _relojBusqueda = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly DispatcherTimer _relojGuardadoFondo = new() { Interval = TimeSpan.FromSeconds(15) };
    private bool _guardandoEnFondo;
    private bool _seleccionandoCatalogo;
    private readonly HashSet<ComboBox> _esperaSeleccion = [];
    private double _existenciaAlAbrir;
    private string _huellaPartidasAlAbrir = string.Empty;
    private bool _actualizandoHistorialProveedor;
    private bool _actualizandoCanalDato;
    private readonly HashSet<UIElement> _ruedaProtegida = [];
    private readonly ObservableCollection<PartidaInventario> _partidas = [];
    private int _cargaFicha;
    private int _suspenderRecargaEditor;

    public AreaPage()
    {
        InitializeComponent();
        _relojSucio.Tick += (_, _) => ActualizarAlertaCambios();
        _relojGuardadoFondo.Tick += (_, _) => GuardarEnFondo();
        _relojBusqueda.Tick += (_, _) =>
        {
            _relojBusqueda.Stop();
            RefrescarLista(_enEdicion?.Id);
        };
        Formulario.SelectionChanged += (_, _) =>
        {
            ProtegerControlesDeRueda(Formulario);
            DispatcherQueue.TryEnqueue(SeleccionarValoresCatalogo);
        };
        Loaded += (_, _) =>
        {
            ProtegerControlesDeRueda(Formulario);
            _relojSucio.Start();
            DispatcherQueue.TryEnqueue(SeleccionarValoresCatalogo);
        };
        Unloaded += (_, _) =>
        {
            _relojSucio.Stop();
            _relojBusqueda.Stop();
            _relojGuardadoFondo.Stop();
        };
    }

    private readonly HashSet<Guid> _marcadas = [];
    private List<FichaListaItem> FichasVisibles { get; set; } = [];
    private int _anclaSeleccion = -1;
    private bool _modoSeleccionar;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is AreaNavegacion navegacion)
        {
            _area = navegacion.AreaId;
            _fichaInicial = navegacion.FichaId;
        }
        else
        {
            _area = e.Parameter is Guid id ? id : AreasOperativas.IdClinica;
            _fichaInicial = null;
        }

        TituloArea.Text = Catalogos.Titulo(_area);
        DescripcionArea.Text = Catalogos.Descripcion(_area);
        ComboOrden.ItemsSource = OrdenFichas.Opciones.Select(o => o.Texto).ToList();
        ComboOrden.SelectedIndex = OrdenFichas.IndicePredeterminado;
        RellenarDesplegables();
        ComboCanalDato1.ItemsSource = CanalDatoProveedor.Opciones;
        ComboCanalDato2.ItemsSource = CanalDatoProveedor.Opciones;

        var plantilla = _area is null ? (AreaOperativa?)null : AreasOperativas.Plantilla(_area);
        PanelClinica.Visibility = VisibleSi(plantilla == AreaOperativa.Clinica);
        PanelOficina.Visibility = VisibleSi(plantilla == AreaOperativa.Oficina);
        PanelLimpieza.Visibility = VisibleSi(plantilla == AreaOperativa.Limpieza);

        CargarHistorialProveedor();

        ListaFichas.ItemsSource = FichasVisibles;
        ListaPartidas.ItemsSource = _partidas;
        RefrescarLista(_fichaInicial, recargarEditor: true, cargarNube: _fichaInicial is not null);
    }

    public void AplicarCambioInventario()
    {
        var id = _enEdicion?.Id;
        var sigue = id is Guid fichaId && App.Instance.Repositorio.Obtener(fichaId) is not null;
        if (_enEdicion is not null && !sigue && !HayCambiosPendientes)
        {
            RefrescarLista();
            OcultarFormulario();
            AnimacionLista.Revelar(ListaFichas);
            return;
        }

        RefrescarLista(id, recargarEditor: _suspenderRecargaEditor == 0 && sigue && !HayCambiosPendientes);
        AnimacionLista.Revelar(ListaFichas);
    }

    public void RecargarDesplegables()
    {
        RellenarDesplegables();
        SeleccionarValoresCatalogo();
    }

    private void RellenarDesplegables()
    {
        ComboCategoria.ItemsSource = OpcionesCombo(Catalogos.CampoCategorias, ValorCatalogo(ComboCategoria) ?? string.Empty);
        ComboUnidad.ItemsSource = OpcionesCombo(Catalogos.CampoUnidades, ValorCatalogo(ComboUnidad) ?? string.Empty);
        ComboEstadoFisico.ItemsSource = OpcionesCombo(Catalogos.CampoEstados, ValorCatalogo(ComboEstadoFisico) ?? string.Empty);
        ComboVidaUtil.ItemsSource = OpcionesCombo(Catalogos.CampoVidasUtil, ValorCatalogo(ComboVidaUtil) ?? string.Empty);
        ComboPais.ItemsSource = OpcionesCombo(Catalogos.CampoPaises, ValorCatalogo(ComboPais) ?? string.Empty);
        ComboOrigen.ItemsSource = OpcionesCombo(Catalogos.CampoTiposOrigen, ValorCatalogo(ComboOrigen) ?? string.Empty);
        ComboPeligrosidad.ItemsSource = OpcionesCombo(Catalogos.CampoPeligrosidad, ValorCatalogo(ComboPeligrosidad) ?? string.Empty);
    }

    private static Visibility VisibleSi(bool condicion) =>
        condicion ? Visibility.Visible : Visibility.Collapsed;

    private static List<OpcionAreaVista> CrearOpcionesArea()
    {
        var opciones = new List<OpcionAreaVista>
        {
            new("Sin área", null)
        };
        opciones.AddRange(AreasOperativas.Todas.Select(a => new OpcionAreaVista(a.Nombre, a.Id)));
        return opciones;
    }

    private Guid? AreaElegida()
    {
        if (_usarAreaAlGuardar)
        {
            return _areaAlGuardar;
        }

        return _enEdicion?.Area ?? _area;
    }

    private void MenuMoverArea_Opening(object sender, object e)
    {
        if (sender is not MenuFlyout menu)
        {
            return;
        }

        menu.Items.Clear();
        foreach (var opcion in CrearOpcionesArea())
        {
            if (Equals(opcion.Id, _area))
            {
                continue;
            }

            var destino = opcion.Id;
            var item = new MenuFlyoutItem { Text = opcion.Nombre };
            item.Click += (_, _) => MoverProductos(ProductosElegidos(), destino);
            menu.Items.Add(item);
        }
    }

    private List<FichaTecnica> ProductosElegidos()
    {
        var marcadas = FichasVisibles.Where(f => f.EstaMarcada).Select(f => f.Ficha).ToList();
        if (marcadas.Count > 0)
        {
            return marcadas;
        }

        if (_enEdicion is not null)
        {
            return [_enEdicion];
        }

        if (ListaFichas.SelectedItem is FichaListaItem item)
        {
            return [item.Ficha];
        }

        return [];
    }

    private void MoverProductos(List<FichaTecnica> productos, Guid? destino)
    {
        try
        {
            if (productos.Count == 0)
            {
                Mostrar("Abra o marque un producto para cambiarlo de área.", InfoBarSeverity.Warning);
                return;
            }

            var aMover = productos.Where(f => !Equals(f.Area, destino)).ToList();
            if (aMover.Count == 0)
            {
                Mostrar("El producto ya está en esa área.", InfoBarSeverity.Warning);
                return;
            }

            var repo = App.Instance.Repositorio;
            if (aMover.Count == 1 && _enEdicion is not null && aMover[0].Id == _enEdicion.Id)
            {
                var areaAnterior = _enEdicion.Area;
                var codigoAnterior = _enEdicion.Codigo;
                var codigo = repo.PrepararMovimientoDeArea(_enEdicion, destino);
                if (CajaCodigo is not null)
                {
                    CajaCodigo.Text = codigo;
                }

                _areaAlGuardar = destino;
                _usarAreaAlGuardar = true;
                bool guardada;
                try
                {
                    guardada = GuardarFichaActual(silencioso: true);
                }
                finally
                {
                    _usarAreaAlGuardar = false;
                }

                if (!guardada)
                {
                    _enEdicion.Area = areaAnterior;
                    _enEdicion.Codigo = codigoAnterior;
                    if (CajaCodigo is not null)
                    {
                        CajaCodigo.Text = codigoAnterior;
                    }

                    return;
                }

                Mostrar($"El producto se movió a {Catalogos.Titulo(destino)} con código {codigo}.", InfoBarSeverity.Success);
                return;
            }

            var prefijo = Catalogos.PrefijoCodigo(destino);
            foreach (var ficha in aMover)
            {
                var viva = repo.Obtener(ficha.Id) ?? ficha;
                var codigo = repo.PrepararMovimientoDeArea(viva, destino);
                if (ReferenceEquals(viva, ficha))
                {
                    ficha.Codigo = codigo;
                }
                else
                {
                    ficha.Area = destino;
                    ficha.Codigo = codigo;
                    viva.Codigo = codigo;
                }

                _marcadas.Remove(ficha.Id);
            }

            repo.GuardarFichas(aMover.Select(f => repo.Obtener(f.Id) ?? f));
            var primero = repo.Obtener(aMover[0].Id) ?? aMover[0];
            if (_enEdicion is not null && aMover.Any(f => f.Id == _enEdicion.Id))
            {
                _enEdicion.Area = primero.Area;
                _enEdicion.Codigo = primero.Codigo;
                if (CajaCodigo is not null)
                {
                    CajaCodigo.Text = primero.Codigo;
                }
            }

            RefrescarLista(Equals(primero.Area, _area) ? primero.Id : null);
            if (_enEdicion is not null && aMover.Any(f => f.Id == _enEdicion.Id) && !Equals(primero.Area, _area))
            {
                OcultarFormulario();
                App.Instance.MainAppWindow?.IrAArea(destino, primero.Id);
            }

            Mostrar(
                aMover.Count == 1
                    ? $"El producto se movió a {Catalogos.Titulo(destino)} con código {primero.Codigo}."
                    : $"{aMover.Count} productos se movieron a {Catalogos.Titulo(destino)} con códigos nuevos {prefijo}_…",
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo cambiar el área: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void RefrescarLista(Guid? seleccionar = null, bool recargarEditor = false, bool cargarNube = false)
    {
        var filtro = CajaBusqueda.Text?.Trim() ?? string.Empty;
        var indiceOrden = ComboOrden.SelectedIndex;
        var criterio = indiceOrden >= 0 && indiceOrden < OrdenFichas.Opciones.Count
            ? OrdenFichas.Opciones[indiceOrden].Criterio
            : CriterioOrden.CodigoAz;
        var resultado = OrdenFichas.Aplicar(
                App.Instance.Repositorio.Buscar(filtro, _area, soloSinArea: _area is null),
                criterio)
            .ToList();

        var lote = resultado.Select(ficha => new FichaListaItem
        {
            Ficha = ficha,
            EstaMarcada = _marcadas.Contains(ficha.Id)
        }).ToList();

        var seleccion = seleccionar ?? _enEdicion?.Id;
        FichasVisibles = lote;
        ListaFichas.ItemsSource = FichasVisibles;
        _anclaSeleccion = Math.Min(_anclaSeleccion, FichasVisibles.Count - 1);

        if (seleccion is Guid id)
        {
            var coincide = lote.FirstOrDefault(f => f.Ficha.Id == id);
            if (coincide is not null)
            {
                _actualizandoLista = true;
                ListaFichas.SelectedItem = coincide;
                _actualizandoLista = false;
                if (_enEdicion is null || _enEdicion.Id != id || recargarEditor)
                {
                    MostrarFicha(coincide.Ficha, cargarNube: cargarNube);
                }

                return;
            }
        }

        if (FichasVisibles.Count == 0)
        {
            OcultarFormulario();
        }
    }

    private void CajaBusqueda_TextChanged(object sender, TextChangedEventArgs e)
    {
        _relojBusqueda.Stop();
        _relojBusqueda.Start();
    }

    private void ComboOrden_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RefrescarLista(_enEdicion?.Id);

    private async void ListaFichas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_actualizandoLista)
        {
            return;
        }

        if (ListaFichas.SelectedItem is not FichaListaItem item)
        {
            return;
        }

        if (SeleccionListaFichas.DebeMarcarSinAbrir(_modoSeleccionar))
        {
            SeleccionListaFichas.AplicarClic(FichasVisibles, _marcadas, item, ref _anclaSeleccion);
            RestaurarSeleccion(_enEdicion?.Id ?? Guid.Empty);
            return;
        }

        if (_enEdicion is not null && item.Ficha.Id != _enEdicion.Id && HayCambiosPendientes)
        {
            var destino = item;
            RestaurarSeleccion(_enEdicion.Id);
            if (!await ConfirmarSalidaAsync())
            {
                return;
            }
            _actualizandoLista = true;
            ListaFichas.SelectedItem = destino;
            _actualizandoLista = false;
            MostrarFicha(destino.Ficha, cargarNube: true);
            return;
        }

        MostrarFicha(item.Ficha, cargarNube: true);
    }

    private void ModoSeleccionar_Click(object sender, RoutedEventArgs e)
    {
        _modoSeleccionar = BotonModoSeleccionar.IsChecked == true;
        TextoModoSeleccionar.Text = _modoSeleccionar
            ? "Seleccionando (no abre ficha)"
            : "Seleccionar productos";
        AyudaSeleccion.Text = _modoSeleccionar
            ? "Clic marca · Ctrl añade · Mayús rango"
            : "Ctrl · Mayús";
    }

    private void SeleccionarTodas_Click(object sender, RoutedEventArgs e) =>
        SeleccionListaFichas.SeleccionarTodas(FichasVisibles, _marcadas);

    private void DeseleccionarTodas_Click(object sender, RoutedEventArgs e) =>
        SeleccionListaFichas.DeseleccionarTodas(FichasVisibles, _marcadas);

    private void SeleccionarCruzada_Click(object sender, RoutedEventArgs e) =>
        SeleccionListaFichas.SeleccionarCruzada(FichasVisibles, _marcadas);

    private void SeleccionarArmonia_Click(object sender, RoutedEventArgs e) =>
        SeleccionListaFichas.SeleccionarArmonia(FichasVisibles, _marcadas);

    private void ListaFichas_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.A && SeleccionListaFichas.CtrlIzquierdoPulsado)
        {
            SeleccionListaFichas.SeleccionarTodas(FichasVisibles, _marcadas);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            SeleccionListaFichas.DeseleccionarTodas(FichasVisibles, _marcadas);
            e.Handled = true;
        }
    }

    private void MarcaFicha_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e) =>
        e.Handled = true;

    private void MarcaFicha_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox caja)
        {
            return;
        }

        var item = caja.DataContext as FichaListaItem
                   ?? FichasVisibles.FirstOrDefault(f => Equals(caja.Tag, f.Ficha.Id));
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
    }

    private async void ImportarExcel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var resultado = await ExcelUi.ImportarAsync(_area);
            if (resultado is null)
            {
                return;
            }

            RefrescarLista(_enEdicion?.Id);
            Mostrar(resultado.Mensaje, InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo importar el Excel: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void SiguienteCodigoLibre_Click(object sender, RoutedEventArgs e)
    {
        if (_enEdicion is null)
        {
            return;
        }

        var area = AreaElegida() ?? _area;
        var codigo = App.Instance.Repositorio.SiguienteCodigo(area);
        _enEdicion.Codigo = codigo;
        CajaCodigo.Text = codigo;
        ActualizarAlertaCambios();
        Mostrar($"Código libre de esta lista: {codigo}.", InfoBarSeverity.Informational);
    }

    private void CajaIdentificacion_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox caja || string.IsNullOrEmpty(caja.Text))
        {
            return;
        }

        var mayusculas = caja == CajaCodigo
            ? TextoIdentificacion.Codigo(caja.Text, recortar: false)
            : TextoIdentificacion.Nombre(caja.Text, recortar: false);
        if (string.Equals(caja.Text, mayusculas, StringComparison.Ordinal))
        {
            return;
        }

        var cursor = caja.SelectionStart;
        caja.Text = mayusculas;
        caja.SelectionStart = Math.Min(cursor, mayusculas.Length);
        if (_enEdicion is null)
        {
            return;
        }

        if (caja == CajaCodigo)
        {
            _enEdicion.Codigo = mayusculas;
        }
        else
        {
            _enEdicion.Nombre = mayusculas;
        }
    }

    private void CargarHistorialProveedor()
    {
        _actualizandoHistorialProveedor = true;
        var historial = App.Instance.Repositorio.HistorialProveedores();
        ComboHistorialProveedor.ItemsSource = historial;
        ComboHistorialProveedor.SelectedItem = null;
        ComboHistorialProveedor.PlaceholderText = historial.Count == 0
            ? "Historial vacío: pulse Guardar en historial"
            : "Historial: elija un proveedor para rellenar el nombre y los datos";
        _actualizandoHistorialProveedor = false;
    }

    private void ComboHistorialProveedor_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_actualizandoHistorialProveedor || _enEdicion is null)
        {
            return;
        }

        if (ComboHistorialProveedor.SelectedItem is not ContactoProveedor contacto)
        {
            return;
        }

        contacto.AplicarA(_enEdicion);
        CajaProveedor.Text = _enEdicion.Proveedor;
        MostrarContactosProveedor();
        ActualizarAlertaCambios();
    }

    private void ComboCanalDato_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_actualizandoCanalDato || _enEdicion is null)
        {
            return;
        }

        var indice = IndiceCanal(sender);
        if (ComboCanal(indice).SelectedItem is not CanalDatoOpcion canal)
        {
            return;
        }

        AsignarCanalEnFicha(indice, canal.Id);
        CajaDato(indice).PlaceholderText = canal.Placeholder;
        ActualizarAlertaCambios();
    }

    private static int IndiceCanal(object sender) =>
        int.TryParse((sender as FrameworkElement)?.Tag as string, out var indice) && indice is 1 or 2
            ? indice
            : 1;

    private ComboBox ComboCanal(int indice) =>
        indice == 2 ? ComboCanalDato2 : ComboCanalDato1;

    private TextBox CajaDato(int indice) =>
        indice == 2 ? CajaDatoProveedor2 : CajaDatoProveedor1;

    private void AsignarCanalEnFicha(int indice, string canal)
    {
        if (_enEdicion is null)
        {
            return;
        }

        if (indice == 2)
        {
            _enEdicion.ContactoCanal2 = canal;
        }
        else
        {
            _enEdicion.ContactoCanal1 = canal;
        }
    }

    private void MostrarContactosProveedor()
    {
        if (_enEdicion is null)
        {
            return;
        }

        _enEdicion.PrepararContactosProveedor();
        CajaProveedor.Text = _enEdicion.Proveedor;
        _actualizandoCanalDato = true;
        PonerCanalYDato(1, _enEdicion.ContactoCanal1, _enEdicion.ContactoDato1);
        PonerCanalYDato(2, _enEdicion.ContactoCanal2, _enEdicion.ContactoDato2);
        _actualizandoCanalDato = false;
    }

    private void PonerCanalYDato(int indice, string? canalId, string? dato)
    {
        var canal = CanalDatoProveedor.De(canalId);
        ComboCanal(indice).SelectedItem = CanalDatoProveedor.Opciones.First(o => o.Id == canal.Id);
        CajaDato(indice).Text = dato ?? string.Empty;
        CajaDato(indice).PlaceholderText = canal.Placeholder;
        AsignarCanalEnFicha(indice, canal.Id);
    }

    private ContactoProveedor ContactoDesdeFormulario()
    {
        CopiarCanalDatoAlModelo();
        return ContactoProveedor.Crear(
            CajaProveedor.Text,
            _enEdicion?.ContactoCanal1,
            CajaDatoProveedor1.Text,
            _enEdicion?.ContactoCanal2,
            CajaDatoProveedor2.Text);
    }

    private void GuardarHistorialProveedor_Click(object sender, RoutedEventArgs e)
    {
        if (_enEdicion is null)
        {
            return;
        }

        SincronizarEnfoque();
        var contacto = ContactoDesdeFormulario();
        if (string.IsNullOrWhiteSpace(contacto.Nombre))
        {
            Mostrar("Escriba el nombre del proveedor para guardarlo en el historial.", InfoBarSeverity.Warning);
            return;
        }

        if (!AlmacenHistorialProveedores.Agregar(contacto))
        {
            Mostrar("Ese proveedor ya está en el historial.", InfoBarSeverity.Informational);
            return;
        }

        CargarHistorialProveedor();
        Mostrar("Proveedor guardado en el historial. En otros productos puede elegirlo de la lista.", InfoBarSeverity.Success);
    }

    private async void BorrarHistorialProveedor_Click(object sender, RoutedEventArgs e)
    {
        var contacto = ContactoElegidoParaHistorial();
        if (contacto is null || contacto.EstaVacio)
        {
            Mostrar("Elija un proveedor del historial o complete los datos que desea quitar.", InfoBarSeverity.Warning);
            return;
        }

        var nombre = string.IsNullOrWhiteSpace(contacto.Nombre) ? contacto.Resumen : contacto.Nombre;
        var dialogo = new ContentDialog
        {
            Title = "Borrar del historial",
            Content = $"¿Quitar «{nombre}» del historial de proveedores? Los productos no se modifican.",
            PrimaryButtonText = "Borrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        AlmacenHistorialProveedores.Eliminar(contacto);
        CargarHistorialProveedor();
        Mostrar("Proveedor quitado del historial.", InfoBarSeverity.Success);
    }

    private ContactoProveedor? ContactoElegidoParaHistorial()
    {
        if (ComboHistorialProveedor.SelectedItem is ContactoProveedor elegido)
        {
            return elegido;
        }

        SincronizarEnfoque();
        return ContactoDesdeFormulario();
    }

    private async void NuevaFicha_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmarSalidaAsync())
        {
            return;
        }

        var nueva = new FichaTecnica
        {
            Area = _area,
            Codigo = App.Instance.Repositorio.SiguienteCodigo(_area),
            FechaAdquisicion = DateTimeOffset.Now
        };

        _actualizandoLista = true;
        ListaFichas.SelectedItem = null;
        _actualizandoLista = false;
        MostrarFicha(nueva, esNueva: true);
        BarraEstado.IsOpen = false;
    }

    private async void MostrarFicha(FichaTecnica ficha, bool esNueva = false, bool cargarNube = false)
    {
        var token = cargarNube ? ++_cargaFicha : _cargaFicha;
        if (cargarNube && !esNueva)
        {
            _suspenderRecargaEditor++;
            try
            {
                var completa = await InventarioNube.CargarCompletaAsync(ficha.Id);
                if (token != _cargaFicha)
                {
                    return;
                }

                if (completa is not null)
                {
                    ficha = completa;
                }
            }
            catch (Exception ex)
            {
                Mostrar($"No se pudo abrir la ficha completa: {ex.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                _suspenderRecargaEditor--;
            }
        }

        if (_suspenderRecargaEditor > 0 && !cargarNube)
        {
            return;
        }

        if (!esNueva && App.Instance.Repositorio.Obtener(ficha.Id) is { } actual)
        {
            ficha = actual;
        }

        try
        {
            _enEdicion = ficha.Clonar();
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo abrir el producto: {ex.Message}", InfoBarSeverity.Error);
            return;
        }

        PrepararFechasDeCalendario(_enEdicion);
        if (esNueva)
        {
            _enEdicion.Id = ficha.Id;
            _enEdicion.Codigo = ficha.Codigo;
            _enEdicion.Area = ficha.Area;
            _enEdicion.FechaAdquisicion = FechaCalendario.ParaPicker(ficha.FechaAdquisicion);
        }

        Formulario.DataContext = _enEdicion;
        CargarHistorialProveedor();
        MostrarContactosProveedor();
        CargarPartidasEnEditor();
        RecordarInventarioAbierto(_enEdicion);
        FijarHuellaOriginal();
        _alertaSuciaVisible = false;
        _relojGuardadoFondo.Stop();
        EstadoVacio.Visibility = Visibility.Collapsed;
        ContenedorFormulario.Visibility = Visibility.Visible;
        ProtegerControlesDeRueda(Formulario);
        DispatcherQueue.TryEnqueue(SeleccionarValoresCatalogo);
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, FijarHuellaOriginal);
    }

    private void OcultarFormulario()
    {
        _enEdicion = null;
        _huellaOriginal = string.Empty;
        _alertaSuciaVisible = false;
        _relojGuardadoFondo.Stop();
        Formulario.DataContext = null;
        _partidas.Clear();
        EstadoVacio.Visibility = Visibility.Visible;
        ContenedorFormulario.Visibility = Visibility.Collapsed;
    }

    private void Guardar_Click(object sender, RoutedEventArgs e) => GuardarFichaActual(silencioso: false);

    public bool HayCambiosPendientes =>
        _enEdicion is not null && Huella(_enEdicion) != _huellaOriginal;

    public bool AutoguardarSiEsPosible()
    {
        try
        {
            return GuardarFichaActual(silencioso: true, enFondo: true);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ConfirmarSalidaAsync()
    {
        SincronizarEnfoque();
        if (!HayCambiosPendientes)
        {
            return true;
        }

        var nombre = string.IsNullOrWhiteSpace(_enEdicion?.Nombre)
            ? "esta ficha"
            : $"«{_enEdicion.Nombre}»";

        var dialogo = new ContentDialog
        {
            Title = "Cambios sin guardar",
            Content = $"Ha editado {nombre} en {Catalogos.Titulo(_area)}. Guarde la información para no perderla.",
            PrimaryButtonText = "Guardar",
            SecondaryButtonText = "Descartar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var resultado = await dialogo.ShowAsync();
        if (resultado == ContentDialogResult.Primary)
        {
            return GuardarFichaActual(silencioso: false);
        }

        if (resultado == ContentDialogResult.Secondary)
        {
            return true;
        }

        return false;
    }

    private bool GuardarFichaActual(bool silencioso, bool enFondo = false)
    {
        if (_enEdicion is null)
        {
            return false;
        }

        if (enFondo)
        {
            EmpujarBindingsSinQuitarFoco();
            return HayCambiosPendientes && GuardarCopiaEnFondo();
        }

        SincronizarEnfoque();
        CopiarPartidasAlModelo();
        AplicarExistenciaDelFormularioSiCorresponde();
        PartidasInventario.PrepararParaGuardar(_enEdicion);

        if (string.IsNullOrWhiteSpace(_enEdicion.Nombre) || string.IsNullOrWhiteSpace(_enEdicion.Codigo))
        {
            Mostrar(
                silencioso
                    ? "Autoguardado pendiente: el código y el nombre son obligatorios."
                    : "El código y el nombre son obligatorios.",
                InfoBarSeverity.Error);
            return false;
        }

        _enEdicion.Nombre = TextoIdentificacion.Nombre(_enEdicion.Nombre);
        _enEdicion.Codigo = TextoIdentificacion.Codigo(_enEdicion.Codigo);
        if (_enEdicion.Codigo.Length > 10)
        {
            _enEdicion.Codigo = _enEdicion.Codigo[..10];
        }

        CajaCodigo.Text = _enEdicion.Codigo;
        if (CajaNombre is not null)
        {
            CajaNombre.Text = _enEdicion.Nombre;
        }

        _enEdicion.Area = AreaElegida();

        if (App.Instance.Repositorio.CodigoEnUso(_enEdicion.Area, _enEdicion.Codigo, _enEdicion.Id))
        {
            Mostrar(
                silencioso
                    ? $"Autoguardado pendiente: el código {_enEdicion.Codigo} ya existe en {Catalogos.Titulo(_enEdicion.Area)}."
                    : $"El código {_enEdicion.Codigo} ya existe en {Catalogos.Titulo(_enEdicion.Area)}.",
                InfoBarSeverity.Error);
            return false;
        }

        App.Instance.Repositorio.GuardarFicha(_enEdicion);
        CargarHistorialProveedor();
        CargarPartidasEnEditor();
        RecordarInventarioAbierto(_enEdicion);
        _huellaOriginal = Huella(_enEdicion);
        _huellaVigilada = _huellaOriginal;
        _alertaSuciaVisible = false;
        var id = _enEdicion.Id;
        var areaDestino = _enEdicion.Area;
        var sigueEnEstaLista = Equals(areaDestino, _area) || (_area is null && areaDestino is null);
        RefrescarLista(sigueEnEstaLista ? id : null);
        if (!sigueEnEstaLista)
        {
            OcultarFormulario();
            var ventana = App.Instance.MainAppWindow;
            DispatcherQueue.TryEnqueue(() => ventana?.IrAArea(areaDestino, id));
            return true;
        }

        if (!silencioso)
        {
            Mostrar("Ficha técnica guardada.", InfoBarSeverity.Success);
        }

        return true;
    }

    private bool GuardarCopiaEnFondo()
    {
        if (_enEdicion is null)
        {
            return false;
        }

        var copia = _enEdicion.Clonar();
        AplicarExistenciaDelFormularioSiCorresponde(copia);
        PartidasInventario.PrepararParaGuardar(copia);
        copia.Nombre = TextoIdentificacion.Nombre(copia.Nombre);
        copia.Codigo = TextoIdentificacion.Codigo(copia.Codigo);
        if (copia.Codigo.Length > 10)
        {
            copia.Codigo = copia.Codigo[..10];
        }

        copia.Area = AreaElegida();
        if (string.IsNullOrWhiteSpace(copia.Nombre) || string.IsNullOrWhiteSpace(copia.Codigo))
        {
            return false;
        }

        if (App.Instance.Repositorio.CodigoEnUso(copia.Area, copia.Codigo, copia.Id))
        {
            return false;
        }

        App.Instance.Repositorio.GuardarFicha(copia);
        RecordarInventarioAbierto(copia);
        _huellaOriginal = Huella(_enEdicion);
        _huellaVigilada = _huellaOriginal;
        return true;
    }

    private void RestaurarSeleccion(Guid id)
    {
        var coincide = FichasVisibles.FirstOrDefault(f => f.Ficha.Id == id);
        _actualizandoLista = true;
        ListaFichas.SelectedItem = coincide;
        _actualizandoLista = false;
    }

    private void GuardarEnFondo()
    {
        _relojGuardadoFondo.Stop();
        if (_guardandoEnFondo || _enEdicion is null)
        {
            return;
        }

        _guardandoEnFondo = true;
        try
        {
            AutoguardarSiEsPosible();
            if (HayCambiosPendientes)
            {
                ProgramarGuardadoFondo();
            }
        }
        finally
        {
            _guardandoEnFondo = false;
        }
    }

    private void ProgramarGuardadoFondo()
    {
        if (_enEdicion is null)
        {
            return;
        }

        _relojGuardadoFondo.Stop();
        _relojGuardadoFondo.Start();
    }

    private void ActualizarAlertaCambios()
    {
        if (_enEdicion is null)
        {
            _relojGuardadoFondo.Stop();
            return;
        }

        SincronizarEditorSinForzarControlesOcultos();
        var actual = Huella(_enEdicion);
        var sucio = actual != _huellaOriginal;
        if (sucio)
        {
            if (actual != _huellaVigilada)
            {
                _huellaVigilada = actual;
                ProgramarGuardadoFondo();
            }
            else if (!_relojGuardadoFondo.IsEnabled)
            {
                ProgramarGuardadoFondo();
            }
        }
        else
        {
            _huellaVigilada = actual;
            _relojGuardadoFondo.Stop();
        }
        BotonGuardarAlerta.Visibility = sucio ? Visibility.Visible : Visibility.Collapsed;
        if (!sucio)
        {
            if (_alertaSuciaVisible)
            {
                BarraEstado.IsOpen = false;
                _alertaSuciaVisible = false;
            }

            return;
        }

        if (_alertaSuciaVisible && BarraEstado.IsOpen && BarraEstado.Severity == InfoBarSeverity.Warning)
        {
            return;
        }

        if (BarraEstado.IsOpen && BarraEstado.Severity == InfoBarSeverity.Error)
        {
            BotonGuardarAlerta.Visibility = Visibility.Visible;
            return;
        }

        _alertaSuciaVisible = true;
        Mostrar("Hay cambios sin guardar en este producto. Pulse Guardar ahora para conservarlos.", InfoBarSeverity.Warning);
    }

    private static string Huella(FichaTecnica ficha)
    {
        var copia = ficha.Clonar();
        copia.FechaCreacion = default;
        copia.FechaActualizacion = default;
        copia.FechaAdquisicion = NormalizarFechaHuella(copia.FechaAdquisicion);
        copia.FechaCaducidad = NormalizarFechaHuella(copia.FechaCaducidad);
        if (double.IsNaN(copia.Existencia))
        {
            copia.Existencia = 0;
        }

        if (double.IsNaN(copia.StockMinimo))
        {
            copia.StockMinimo = 0;
        }

        if (double.IsNaN(copia.GarantiaMeses))
        {
            copia.GarantiaMeses = 0;
        }

        foreach (var partida in copia.Partidas)
        {
            partida.FechaCaducidad = NormalizarFechaHuella(partida.FechaCaducidad);
            if (double.IsNaN(partida.Cantidad))
            {
                partida.Cantidad = 0;
            }
        }

        return JsonSerializer.Serialize(copia, JsonHuella);
    }

    private static DateTimeOffset? NormalizarFechaHuella(DateTimeOffset? fecha) =>
        fecha is { } valor ? new DateTimeOffset(valor.Date, TimeSpan.Zero) : null;

    private void FijarHuellaOriginal()
    {
        if (_enEdicion is null)
        {
            return;
        }

        _huellaOriginal = Huella(_enEdicion);
        _huellaVigilada = _huellaOriginal;
    }

    private void CopiarCanalDatoAlModelo()
    {
        if (_enEdicion is null || !ControlListoParaLeer(CajaProveedor) || !EstaEnPestañaVisible(CajaProveedor))
        {
            return;
        }

        CopiarTextoSiListo(CajaProveedor, valor => _enEdicion.Proveedor = valor);
        CopiarTextoSiListo(CajaDatoProveedor1, valor => _enEdicion.ContactoDato1 = valor);
        CopiarTextoSiListo(CajaDatoProveedor2, valor => _enEdicion.ContactoDato2 = valor);
        _enEdicion.ContactoDato3 = string.Empty;
        _enEdicion.ContactoCanal3 = CanalDatoProveedor.Web;
        if (ComboCanalDato1.SelectedItem is CanalDatoOpcion c1)
        {
            _enEdicion.ContactoCanal1 = c1.Id;
        }

        if (ComboCanalDato2.SelectedItem is CanalDatoOpcion c2)
        {
            _enEdicion.ContactoCanal2 = c2.Id;
        }

        _enEdicion.ContactosProveedorDefinidos = true;
        _enEdicion.SincronizarCamposProveedorLegados();
    }

    private void CopiarTextoSiListo(TextBox? caja, Action<string> asignar)
    {
        if (!ControlListoParaLeer(caja))
        {
            return;
        }

        asignar(caja!.Text?.Trim() ?? string.Empty);
    }

    private static bool ControlListoParaLeer(FrameworkElement? control) =>
        control is { IsLoaded: true, Visibility: Visibility.Visible } && control.ActualHeight > 0;

    private bool EstaEnPestañaVisible(DependencyObject origen)
    {
        var actual = origen;
        while (actual is not null && actual is not Pivot)
        {
            if (actual is PivotItem item)
            {
                return ReferenceEquals(item, Formulario.SelectedItem);
            }

            actual = VisualTreeHelper.GetParent(actual);
        }

        return true;
    }

    private void SincronizarEnfoque()
    {
        CopiarPartidasAlModelo();
        CopiarCanalDatoAlModelo();
        if (XamlRoot is null)
        {
            return;
        }

        ContenedorFormulario.Focus(FocusState.Programmatic);
    }

    private void EmpujarBindingsSinQuitarFoco()
    {
        SincronizarEditorSinForzarControlesOcultos();
        if (Formulario is not null)
        {
            EmpujarBindings(Formulario);
        }
    }

    private void SincronizarEditorSinForzarControlesOcultos()
    {
        CopiarPartidasAlModelo();
        CopiarCanalDatoAlModelo();
        if (XamlRoot is not null
            && FocusManager.GetFocusedElement(XamlRoot) is TextBox caja
            && ElementoEnFoco(caja))
        {
            AplicarTextoEnfocado(caja);
        }
    }

    private void EmpujarBindings(DependencyObject raiz)
    {
        if (raiz is FrameworkElement elemento && (!ControlListoParaLeer(elemento) || !EstaEnPestañaVisible(elemento)))
        {
            return;
        }

        switch (raiz)
        {
            case TextBox caja:
                if (ElementoEnFoco(caja))
                {
                    AplicarTextoEnfocado(caja);
                }
                else if (!string.IsNullOrEmpty(caja.Text) || !TieneTextoEnModelo(caja))
                {
                    caja.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                }

                return;
            case NumberBox numero:
                if (!ElementoEnFoco(numero) && !double.IsNaN(numero.Value))
                {
                    numero.GetBindingExpression(NumberBox.ValueProperty)?.UpdateSource();
                }

                return;
            case ComboBox combo:
                if (!ElementoEnFoco(combo))
                {
                    if (!string.IsNullOrEmpty(combo.Text) || !TieneTextoEnModelo(combo))
                    {
                        combo.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                    }

                    if (combo.SelectedItem is not null)
                    {
                        combo.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
                    }
                }

                return;
            case CalendarDatePicker fecha:
                if (!ElementoEnFoco(fecha) && (fecha.Date is not null || !TieneFechaEnModelo(fecha)))
                {
                    fecha.GetBindingExpression(CalendarDatePicker.DateProperty)?.UpdateSource();
                }

                return;
            case CheckBox check:
                check.GetBindingExpression(CheckBox.IsCheckedProperty)?.UpdateSource();
                return;
            case ToggleSwitch interruptor:
                interruptor.GetBindingExpression(ToggleSwitch.IsOnProperty)?.UpdateSource();
                return;
        }

        var hijos = VisualTreeHelper.GetChildrenCount(raiz);
        for (var i = 0; i < hijos; i++)
        {
            EmpujarBindings(VisualTreeHelper.GetChild(raiz, i));
        }
    }

    private static bool TieneTextoEnModelo(FrameworkElement control)
    {
        var ruta = control switch
        {
            TextBox caja => caja.GetBindingExpression(TextBox.TextProperty)?.ParentBinding?.Path?.Path,
            ComboBox combo => combo.GetBindingExpression(ComboBox.SelectedItemProperty)?.ParentBinding?.Path?.Path
                ?? combo.GetBindingExpression(ComboBox.TextProperty)?.ParentBinding?.Path?.Path,
            _ => null
        };
        if (string.IsNullOrEmpty(ruta) || control.DataContext is null)
        {
            return false;
        }

        var propiedad = control.DataContext.GetType().GetProperty(ruta);
        return propiedad?.GetValue(control.DataContext) is string texto && texto.Length > 0;
    }

    private static bool TieneFechaEnModelo(CalendarDatePicker fecha)
    {
        var ruta = fecha.GetBindingExpression(CalendarDatePicker.DateProperty)?.ParentBinding?.Path?.Path;
        if (string.IsNullOrEmpty(ruta) || fecha.DataContext is null)
        {
            return false;
        }

        return fecha.DataContext.GetType().GetProperty(ruta)?.GetValue(fecha.DataContext) is DateTimeOffset;
    }

    private IEnumerable<ComboBox> CombosDePestanaVisible()
    {
        if (Formulario.SelectedItem is not PivotItem pestana)
        {
            yield break;
        }

        var titulo = pestana.Header?.ToString();
        if (titulo == "Ficha")
        {
            yield return ComboCategoria;
            yield return ComboUnidad;
            yield return ComboEstadoFisico;
            yield return ComboVidaUtil;
            yield return ComboPeligrosidad;
        }
        else if (titulo == "Procedencia")
        {
            yield return ComboPais;
            yield return ComboOrigen;
        }
    }

    private void SeleccionarValoresCatalogo()
    {
        if (_enEdicion is null || _seleccionandoCatalogo)
        {
            return;
        }

        _seleccionandoCatalogo = true;
        try
        {
            foreach (var combo in CombosDePestanaVisible())
            {
                if (!combo.IsLoaded)
                {
                    if (_esperaSeleccion.Add(combo))
                    {
                        RoutedEventHandler? alCargar = null;
                        alCargar = (_, _) =>
                        {
                            combo.Loaded -= alCargar;
                            _esperaSeleccion.Remove(combo);
                            SeleccionarValoresCatalogo();
                        };
                        combo.Loaded += alCargar;
                    }

                    continue;
                }

                var campo = CampoCatalogo(combo);
                if (campo is null)
                {
                    continue;
                }

                var valor = ValorCatalogo(combo) ?? string.Empty;
                var opciones = OpcionesCombo(campo, valor);
                combo.ItemsSource = opciones;
                if (valor.Length == 0)
                {
                    combo.SelectedItem = null;
                    continue;
                }

                var coincidencia = opciones.FirstOrDefault(o =>
                    o.Equals(valor, StringComparison.CurrentCultureIgnoreCase));
                if (coincidencia is not null)
                {
                    combo.SelectedItem = coincidencia;
                }
            }
        }
        finally
        {
            _seleccionandoCatalogo = false;
        }
    }

    private string? CampoCatalogo(ComboBox caja)
    {
        if (ReferenceEquals(caja, ComboCategoria)) return Catalogos.CampoCategorias;
        if (ReferenceEquals(caja, ComboUnidad)) return Catalogos.CampoUnidades;
        if (ReferenceEquals(caja, ComboEstadoFisico)) return Catalogos.CampoEstados;
        if (ReferenceEquals(caja, ComboVidaUtil)) return Catalogos.CampoVidasUtil;
        if (ReferenceEquals(caja, ComboPais)) return Catalogos.CampoPaises;
        if (ReferenceEquals(caja, ComboOrigen)) return Catalogos.CampoTiposOrigen;
        if (ReferenceEquals(caja, ComboPeligrosidad)) return Catalogos.CampoPeligrosidad;
        return null;
    }

    private string? ValorCatalogo(ComboBox caja)
    {
        if (_enEdicion is null)
        {
            return null;
        }

        if (ReferenceEquals(caja, ComboCategoria)) return _enEdicion.Categoria;
        if (ReferenceEquals(caja, ComboUnidad)) return _enEdicion.UnidadMedida;
        if (ReferenceEquals(caja, ComboEstadoFisico)) return _enEdicion.EstadoFisico;
        if (ReferenceEquals(caja, ComboVidaUtil)) return _enEdicion.VidaUtil;
        if (ReferenceEquals(caja, ComboPais)) return _enEdicion.PaisOrigen;
        if (ReferenceEquals(caja, ComboOrigen)) return _enEdicion.TipoOrigen;
        if (ReferenceEquals(caja, ComboPeligrosidad)) return _enEdicion.Peligrosidad;
        return null;
    }

    private List<string> OpcionesCombo(string campo, string textoActual)
    {
        var opciones = Catalogos.Obtener(_area, campo)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (campo == Catalogos.CampoPaises && !opciones.Exists(EsPeru))
        {
            opciones.Insert(0, "Perú");
        }

        if (textoActual.Length > 0 &&
            !opciones.Exists(v => v.Equals(textoActual, StringComparison.CurrentCultureIgnoreCase)))
        {
            opciones.Insert(0, textoActual);
        }

        return opciones;
    }

    private static bool EsPeru(string valor)
    {
        var texto = valor.Trim();
        return texto.Equals("Perú", StringComparison.CurrentCultureIgnoreCase)
            || texto.Equals("Peru", StringComparison.CurrentCultureIgnoreCase);
    }

    private void ProtegerControlesDeRueda(DependencyObject? raiz)
    {
        if (raiz is null)
        {
            return;
        }

        if (raiz is ComboBox or NumberBox or CalendarDatePicker && raiz is UIElement control && _ruedaProtegida.Add(control))
        {
            control.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(RedirigirRuedaAlDesplazamiento), true);
        }

        var hijos = VisualTreeHelper.GetChildrenCount(raiz);
        for (var i = 0; i < hijos; i++)
        {
            ProtegerControlesDeRueda(VisualTreeHelper.GetChild(raiz, i));
        }
    }

    private static void RedirigirRuedaAlDesplazamiento(object sender, PointerRoutedEventArgs e)
    {
        if (sender is ComboBox { IsDropDownOpen: true } || sender is not UIElement origen)
        {
            return;
        }

        var scroll = BuscarPadre<ScrollViewer>(origen);
        if (scroll is null)
        {
            return;
        }

        e.Handled = true;
        var delta = e.GetCurrentPoint(origen).Properties.MouseWheelDelta;
        scroll.ChangeView(null, scroll.VerticalOffset - delta, null, true);
    }

    private static T? BuscarPadre<T>(DependencyObject? origen)
        where T : DependencyObject
    {
        var actual = origen;
        while (actual is not null)
        {
            if (actual is T coincidencia)
            {
                return coincidencia;
            }

            actual = VisualTreeHelper.GetParent(actual);
        }

        return null;
    }

    private bool ElementoEnFoco(DependencyObject elemento)
    {
        if (XamlRoot is null)
        {
            return false;
        }

        var actual = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        while (actual is not null)
        {
            if (ReferenceEquals(actual, elemento))
            {
                return true;
            }

            actual = VisualTreeHelper.GetParent(actual);
        }

        return false;
    }

    private static void AplicarTextoEnfocado(TextBox caja)
    {
        var ruta = caja.GetBindingExpression(TextBox.TextProperty)?.ParentBinding?.Path?.Path;
        if (string.IsNullOrEmpty(ruta) || caja.DataContext is null)
        {
            return;
        }

        var propiedad = caja.DataContext.GetType().GetProperty(ruta);
        if (propiedad is null || !propiedad.CanWrite || propiedad.PropertyType != typeof(string))
        {
            return;
        }

        var actual = (string?)propiedad.GetValue(caja.DataContext) ?? string.Empty;
        var texto = caja.Text ?? string.Empty;
        if (!string.Equals(actual, texto, StringComparison.Ordinal))
        {
            propiedad.SetValue(caja.DataContext, texto);
        }
    }

    private static void PrepararFechasDeCalendario(FichaTecnica ficha)
    {
        ficha.FechaAdquisicion = FechaCalendario.ParaPicker(ficha.FechaAdquisicion);
        ficha.FechaCaducidad = FechaCalendario.ParaPicker(ficha.FechaCaducidad);
        foreach (var partida in ficha.Partidas ?? [])
        {
            partida.FechaCaducidad = FechaCalendario.ParaPicker(partida.FechaCaducidad);
        }
    }

    private void CargarPartidasEnEditor()
    {
        _partidas.Clear();
        if (_enEdicion is null)
        {
            return;
        }

        PartidasInventario.Normalizar(_enEdicion);
        foreach (var partida in _enEdicion.Partidas.Select(p => p.Clonar()))
        {
            _partidas.Add(partida);
        }

        _enEdicion.Partidas = _partidas.Select(p => p.Clonar()).ToList();
        ProtegerControlesDeRueda(ListaPartidas);
    }

    private void CopiarPartidasAlModelo()
    {
        if (_enEdicion is null)
        {
            return;
        }

        _enEdicion.Partidas = _partidas.Select(p => p.Clonar()).ToList();
    }

    private void AplicarExistenciaDelFormularioSiCorresponde(FichaTecnica? destino = null)
    {
        var ficha = destino ?? _enEdicion;
        if (ficha is null)
        {
            return;
        }

        var lotesCambiados = !string.Equals(HuellaPartidas(ficha), _huellaPartidasAlAbrir, StringComparison.Ordinal);
        var existenciaCambiada = Math.Abs(ficha.Existencia - _existenciaAlAbrir) > PartidasInventario.Epsilon;
        if (lotesCambiados || !existenciaCambiada || ficha.Partidas.Count > 1)
        {
            return;
        }

        if (ficha.Partidas.Count == 0)
        {
            if (ficha.Existencia > PartidasInventario.Epsilon
                || !string.IsNullOrWhiteSpace(ficha.Lote)
                || ficha.FechaCaducidad.HasValue)
            {
                ficha.Partidas.Add(new PartidaInventario
                {
                    Lote = ficha.Lote?.Trim() ?? string.Empty,
                    FechaCaducidad = ficha.FechaCaducidad,
                    Cantidad = Math.Max(0, ficha.Existencia)
                });
            }

            return;
        }

        ficha.Partidas[0].Cantidad = Math.Max(0, ficha.Existencia);
    }

    private void RecordarInventarioAbierto(FichaTecnica ficha)
    {
        _existenciaAlAbrir = ficha.Existencia;
        _huellaPartidasAlAbrir = HuellaPartidas(ficha);
    }

    private static string HuellaPartidas(FichaTecnica ficha) =>
        JsonSerializer.Serialize(
            (ficha.Partidas ?? []).Select(p => new
            {
                p.Id,
                Lote = p.Lote ?? string.Empty,
                Fecha = p.FechaCaducidad?.UtcDateTime.Date,
                Cantidad = Math.Round(p.Cantidad, 4)
            }),
            JsonHuella);

    private void AgregarPartida_Click(object sender, RoutedEventArgs e)
    {
        if (_enEdicion is null)
        {
            return;
        }

        _partidas.Add(new PartidaInventario());
        CopiarPartidasAlModelo();
        ProtegerControlesDeRueda(ListaPartidas);
        ActualizarAlertaCambios();
    }

    private void QuitarPartida_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement elemento || elemento.DataContext is not PartidaInventario item)
        {
            return;
        }

        _partidas.Remove(item);
        CopiarPartidasAlModelo();
        ActualizarAlertaCambios();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaFichas.SelectedItem is FichaListaItem item)
        {
            MostrarFicha(item.Ficha);
            Mostrar("Se descartaron los cambios no guardados.", InfoBarSeverity.Informational);
            return;
        }

        OcultarFormulario();
    }

    private async void Eliminar_Click(object sender, RoutedEventArgs e)
    {
        if (_enEdicion is null)
        {
            return;
        }

        var existe = App.Instance.Repositorio.Todas.Any(f => f.Id == _enEdicion.Id);
        if (!existe)
        {
            OcultarFormulario();
            return;
        }

        var dialogo = new ContentDialog
        {
            Title = "Eliminar ficha técnica",
            Content = $"Se eliminará «{_enEdicion.Nombre}» ({_enEdicion.Codigo}) del área de {Catalogos.Titulo(_area)}.",
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await AprobacionBorradoInventario.InterceptarAsync(XamlRoot, [_enEdicion]))
        {
            return;
        }

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        ImagenFichaService.EliminarDeFicha(_enEdicion.Id);
        App.Instance.Repositorio.Eliminar(_enEdicion.Id);
        _marcadas.Remove(_enEdicion.Id);
        OcultarFormulario();
        RefrescarLista();
        Mostrar("La ficha se eliminó del catálogo.", InfoBarSeverity.Success);
    }

    private async void ExportarFicha_Click(object sender, RoutedEventArgs e)
    {
        if (_enEdicion is null)
        {
            return;
        }

        await ExportarIndividualAsync(_enEdicion);
    }

    private async void ExportarFichaLista_Click(object sender, RoutedEventArgs e)
    {
        var marcadas = FichasVisibles.Where(f => f.EstaMarcada).Select(f => f.Ficha).ToList();
        if (marcadas.Count == 1)
        {
            await ExportarIndividualAsync(marcadas[0]);
            return;
        }

        if (_enEdicion is not null)
        {
            await ExportarIndividualAsync(_enEdicion);
            return;
        }

        Mostrar("Abra una ficha, márquela o pulse el botón de exportar en su fila.", InfoBarSeverity.Warning);
    }

    private async void ExportarFila_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement elemento)
        {
            return;
        }

        var ficha = (elemento.DataContext as FichaListaItem)?.Ficha
                    ?? FichasVisibles.FirstOrDefault(f => Equals(elemento.Tag, f.Ficha.Id))?.Ficha;
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
            if (resultado is null)
            {
                return;
            }

            Mostrar($"Se exportó la ficha técnica ({resultado.Extension.Trim('.')}).", InfoBarSeverity.Success);
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo exportar: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void ExportarConsolidado_Click(object sender, RoutedEventArgs e)
    {
        var seleccionadas = FichasVisibles
            .Where(f => f.EstaMarcada)
            .Select(f => f.Ficha)
            .ToList();

        if (seleccionadas.Count == 0)
        {
            Mostrar("Marque al menos una ficha de la lista para exportar el consolidado.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            seleccionadas = (await InventarioNube.CargarCompletasAsync(seleccionadas)).ToList();
            var resultado = await ExcelUi.ExportarConsolidadoAsync(seleccionadas);
            if (resultado is null)
            {
                return;
            }

            Mostrar($"Se exportó el consolidado de {seleccionadas.Count} ficha(s) ({resultado.Extension.Trim('.')}).", InfoBarSeverity.Success);
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo exportar: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void Mostrar(string mensaje, InfoBarSeverity severidad)
    {
        BarraEstado.Severity = severidad;
        BarraEstado.Message = mensaje;
        BarraEstado.IsOpen = true;
        BotonGuardarAlerta.Visibility = severidad == InfoBarSeverity.Warning || HayCambiosPendientes
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}

public sealed record OpcionAreaVista(string Nombre, Guid? Id);
