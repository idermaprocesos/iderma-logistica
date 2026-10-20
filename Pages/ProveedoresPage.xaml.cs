using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas.Pages;

public sealed class ProveedorFila
{
    public required string IdTexto { get; init; }
    public required string Nombre { get; init; }
    public required string Dato1 { get; init; }
    public required string Dato2 { get; init; }
    public required string Placeholder1 { get; init; }
    public required string Placeholder2 { get; init; }
    public required string Notas { get; init; }
    public required string Calificacion { get; init; }
    public required string FechaTexto { get; init; }
    public required string UsoTexto { get; init; }
    public required string EmpresaTexto { get; init; }
    public required string GestionTexto { get; init; }
    public IReadOnlyList<CanalDatoOpcion> Canales => CanalDatoProveedor.Opciones;
    public IReadOnlyList<string> Calificaciones => CalificacionProveedor.Todas;
}

public sealed partial class ProveedoresPage : Page, IVistaInventario
{
    private readonly HashSet<int> _meses = [];
    private int? _atajoMeses;
    private bool _aplicandoFiltros;
    private int _listaSuspendida;
    private int _total;

    public ProveedoresPage()
    {
        InitializeComponent();
        ConstruirMeses();
        _aplicandoFiltros = true;
        ComboCalificacion.ItemsSource = new List<string> { "Todas las calificaciones" }
            .Concat(CalificacionProveedor.Todas)
            .ToList();
        ComboCalificacion.SelectedIndex = 0;
        _aplicandoFiltros = false;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var agregados = SincronizarFichas();
        RefrescarLista(recargarAnios: true);
        if (agregados > 0)
        {
            Mostrar(
                InfoBarSeverity.Informational,
                agregados == 1
                    ? "Se agregó 1 proveedor desde las fichas técnicas, con sus redes, web y notas."
                    : $"Se agregaron {agregados} proveedores desde las fichas técnicas, con sus redes, web y notas.");
        }
    }

    private void CajaBusqueda_TextChanged(object sender, TextChangedEventArgs e) => RefrescarLista(false);

    private void Filtro_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_aplicandoFiltros)
        {
            return;
        }

        RefrescarLista(false);
    }

    private void Periodo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_aplicandoFiltros)
        {
            return;
        }

        QuitarAtajoMeses();
        RefrescarLista(false);
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
        if (_aplicandoFiltros || sender is not ToggleButton chip || chip.Tag is not int mes)
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
        RefrescarLista(false);
    }

    private void TodosMeses_Click(object sender, RoutedEventArgs e)
    {
        _meses.Clear();
        _aplicandoFiltros = true;
        foreach (var hijo in PanelMeses.Children.OfType<ToggleButton>())
        {
            hijo.IsChecked = false;
        }

        _aplicandoFiltros = false;
        QuitarAtajoMeses();
        RefrescarLista(false);
    }

    private void AtajoMeses_Click(object sender, RoutedEventArgs e)
    {
        if (_aplicandoFiltros || sender is not ToggleButton chip ||
            !int.TryParse(chip.Tag?.ToString(), out var meses))
        {
            return;
        }

        if (chip.IsChecked == true)
        {
            _atajoMeses = meses;
            _aplicandoFiltros = true;
            ComboAnio.SelectedIndex = 0;
            _meses.Clear();
            foreach (var hijo in PanelMeses.Children.OfType<ToggleButton>())
            {
                hijo.IsChecked = false;
            }

            _aplicandoFiltros = false;
        }
        else if (_atajoMeses == meses)
        {
            _atajoMeses = null;
        }

        ActualizarAtajos();
        RefrescarLista(false);
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
        _aplicandoFiltros = true;
        ChipUltimos3.IsChecked = _atajoMeses == 3;
        ChipUltimos6.IsChecked = _atajoMeses == 6;
        ChipUltimos12.IsChecked = _atajoMeses == 12;
        _aplicandoFiltros = false;
    }

    private void CargarAnios()
    {
        var actual = ComboAnio.SelectedItem as string;
        var opciones = new List<string> { "Todos los años" };
        foreach (var anio in App.Instance.Proveedores.Anios())
        {
            opciones.Add(anio.ToString());
        }

        var hoy = DateTime.Now.Year.ToString();
        if (!opciones.Contains(hoy))
        {
            opciones.Insert(1, hoy);
        }

        _aplicandoFiltros = true;
        ComboAnio.ItemsSource = opciones;
        if (actual is not null && opciones.Contains(actual))
        {
            ComboAnio.SelectedItem = actual;
        }
        else
        {
            ComboAnio.SelectedIndex = 0;
        }

        _aplicandoFiltros = false;
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

    private string? CalificacionActual()
    {
        if (ComboCalificacion.SelectedIndex <= 0 || ComboCalificacion.SelectedItem is not string texto)
        {
            return null;
        }

        return texto;
    }

    public void AplicarCambioInventario()
    {
        SincronizarFichas();
        RefrescarLista(false);
        AnimacionLista.Revelar(Lista);
    }

    private void RefrescarLista(bool recargarAnios)
    {
        if (recargarAnios)
        {
            CargarAnios();
        }

        var busqueda = (CajaBusqueda.Text ?? string.Empty).Trim();
        var calificacion = CalificacionActual();
        var anio = _atajoMeses is null ? AnioActual() : null;
        IReadOnlySet<int> meses = _atajoMeses is null ? _meses : new HashSet<int>();
        var desde = DesdeActual();
        var todos = App.Instance.Proveedores.Listar();
        _total = todos.Count;

        var usos = UsosPorNombre();
        var filas = todos
            .Where(p => Pasa(p, busqueda, calificacion, anio, meses, desde))
            .Select(p => CrearFila(p, usos.TryGetValue(p.Nombre, out var n) ? n : 0))
            .ToList();

        SuspenderLista();
        Lista.ItemsSource = filas;
        ReanudarLista();

        var hayFiltro = busqueda.Length > 0 || calificacion is not null || anio is not null || meses.Count > 0 || desde is not null;
        TextoVacio.Visibility = filas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TextoVacio.Text = _total == 0
            ? "Aún no hay proveedores. Agregue uno o actualice desde las fichas técnicas."
            : "Ningún proveedor coincide con la búsqueda o el periodo.";
        TextoConteo.Text = hayFiltro
            ? $"{TextoCantidad(filas.Count)} de {TextoCantidad(_total)} · {FiltroPeriodoIngresos.Describir(anio, meses, desde, _atajoMeses)}"
            : $"{TextoCantidad(_total)} · {FiltroPeriodoIngresos.Describir(anio, meses, desde, _atajoMeses)}";
    }

    private static bool Pasa(
        ProveedorRegistro proveedor,
        string busqueda,
        string? calificacion,
        int? anio,
        IReadOnlySet<int> meses,
        DateTimeOffset? desde)
    {
        if (calificacion is not null &&
            !string.Equals(proveedor.Calificacion, calificacion, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!FiltroPeriodoIngresos.Pasa(proveedor.FechaAgregado, anio, meses, desde))
        {
            return false;
        }

        if (busqueda.Length == 0)
        {
            return true;
        }

        var texto = $"{proveedor.Nombre} {proveedor.RazonSocial} {proveedor.Ruc} {proveedor.Contacto} {proveedor.Dato1} {proveedor.Dato2} {proveedor.Notas} {proveedor.Calificacion} {CanalDatoProveedor.De(proveedor.Canal1).Titulo} {CanalDatoProveedor.De(proveedor.Canal2).Titulo}";
        return texto.Contains(busqueda, StringComparison.CurrentCultureIgnoreCase);
    }

    private static Dictionary<string, int> UsosPorNombre() =>
        App.Instance.Repositorio.Todas
            .Select(f => (f.Proveedor ?? string.Empty).Trim())
            .Where(nombre => nombre.Length > 0)
            .GroupBy(nombre => nombre, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

    private static ProveedorFila CrearFila(ProveedorRegistro proveedor, int usos) => new()
    {
        IdTexto = proveedor.Id.ToString(),
        Nombre = proveedor.Nombre,
        Dato1 = proveedor.Dato1,
        Dato2 = proveedor.Dato2,
        Placeholder1 = CanalDatoProveedor.De(proveedor.Canal1).Placeholder,
        Placeholder2 = CanalDatoProveedor.De(proveedor.Canal2).Placeholder,
        Notas = proveedor.Notas,
        Calificacion = proveedor.Calificacion,
        FechaTexto = $"Agregado el {proveedor.FechaAgregado.ToLocalTime():dd/MM/yyyy}",
        EmpresaTexto = proveedor.TextoEmpresa,
        GestionTexto = proveedor.TextoGestionCompra,
        UsoTexto = usos == 0
            ? "Aún no está en una ficha técnica"
            : usos == 1 ? "En 1 ficha técnica" : $"En {usos} fichas técnicas"
    };

    private static string TextoCantidad(int cantidad) =>
        cantidad == 1 ? "1 proveedor" : $"{cantidad} proveedores";

    private async void Agregar_Click(object sender, RoutedEventArgs e) =>
        await EditarAsync(null);

    private async void Editar_Click(object sender, RoutedEventArgs e)
    {
        if (!TryId(sender, out var id) || App.Instance.Proveedores.Obtener(id) is not { } proveedor)
        {
            return;
        }

        await EditarAsync(proveedor);
    }

    private async Task EditarAsync(ProveedorRegistro? origen)
    {
        var esNuevo = origen is null;
        var id = origen?.Id ?? Guid.NewGuid();
        var nombre = new TextBox
        {
            Header = "Nombre",
            Text = origen?.Nombre ?? string.Empty,
            PlaceholderText = "Nombre con el que aparece en las fichas"
        };
        var ruc = new TextBox
        {
            Header = "RUC",
            Text = origen?.Ruc ?? string.Empty,
            PlaceholderText = "11 dígitos",
            MaxLength = 11
        };
        var razonSocial = new TextBox
        {
            Header = "Nombre de empresa",
            Text = origen?.RazonSocial ?? string.Empty,
            PlaceholderText = "Razón social"
        };
        var tiempoEntrega = new NumberBox
        {
            Header = "Tiempo de entrega (días)",
            PlaceholderText = "Días que indica el proveedor",
            Minimum = 0,
            Maximum = 365,
            SmallChange = 1,
            LargeChange = 5,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            Value = origen?.TiempoEntregaDias ?? 0
        };
        var acuerdo = new CheckBox
        {
            Content = "Acuerdo por contrato: hay que comprar una cantidad fija",
            IsChecked = origen?.AcuerdoContrato == true
        };
        var cantidadContrato = new NumberBox
        {
            Header = "Cantidad que se debe comprar",
            Minimum = 0,
            SmallChange = 1,
            LargeChange = 10,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            Value = (double)(origen?.CantidadContrato ?? 0),
            IsEnabled = origen?.AcuerdoContrato == true
        };
        var unidadContrato = new TextBox
        {
            Header = "Unidad de esa cantidad",
            Text = origen?.UnidadContrato ?? string.Empty,
            PlaceholderText = "unidades, cajas, litros…",
            IsEnabled = origen?.AcuerdoContrato == true
        };
        acuerdo.Checked += (_, _) =>
        {
            cantidadContrato.IsEnabled = true;
            unidadContrato.IsEnabled = true;
        };
        acuerdo.Unchecked += (_, _) =>
        {
            cantidadContrato.IsEnabled = false;
            unidadContrato.IsEnabled = false;
        };
        var canal1 = CrearComboCanal(origen?.Canal1);
        var dato1 = new TextBox
        {
            Header = "Dato",
            Text = origen?.Dato1 ?? string.Empty,
            PlaceholderText = CanalDatoProveedor.De(origen?.Canal1).Placeholder
        };
        var canal2 = CrearComboCanal(origen?.Canal2 ?? CanalDatoProveedor.Web);
        var dato2 = new TextBox
        {
            Header = "Dato",
            Text = origen?.Dato2 ?? string.Empty,
            PlaceholderText = CanalDatoProveedor.De(origen?.Canal2).Placeholder
        };
        canal1.SelectionChanged += (_, _) =>
        {
            if (canal1.SelectedItem is CanalDatoOpcion opcion)
            {
                dato1.PlaceholderText = opcion.Placeholder;
            }
        };
        canal2.SelectionChanged += (_, _) =>
        {
            if (canal2.SelectedItem is CanalDatoOpcion opcion)
            {
                dato2.PlaceholderText = opcion.Placeholder;
            }
        };
        var filaCanal1 = new Grid { ColumnSpacing = 8 };
        filaCanal1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        filaCanal1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(dato1, 1);
        filaCanal1.Children.Add(canal1);
        filaCanal1.Children.Add(dato1);
        var filaCanal2 = new Grid { ColumnSpacing = 8 };
        filaCanal2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        filaCanal2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(dato2, 1);
        filaCanal2.Children.Add(canal2);
        filaCanal2.Children.Add(dato2);
        var notas = new TextBox
        {
            Header = "Notas",
            Text = origen?.Notas ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 90
        };
        var calificacion = new ComboBox
        {
            Header = "Calificación",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        foreach (var item in CalificacionProveedor.Todas)
        {
            calificacion.Items.Add(item);
        }

        calificacion.SelectedItem = CalificacionProveedor.Normalizar(origen?.Calificacion);
        var error = new TextBlock
        {
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["VencidoBrush"],
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };
        var panel = new StackPanel { Spacing = 12, MinWidth = 480 };
        panel.Children.Add(nombre);
        panel.Children.Add(ruc);
        panel.Children.Add(razonSocial);
        panel.Children.Add(tiempoEntrega);
        panel.Children.Add(acuerdo);
        panel.Children.Add(cantidadContrato);
        panel.Children.Add(unidadContrato);
        panel.Children.Add(filaCanal1);
        panel.Children.Add(filaCanal2);
        panel.Children.Add(calificacion);
        panel.Children.Add(notas);
        panel.Children.Add(error);

        var dialogo = new ContentDialog
        {
            Title = esNuevo ? "Agregar proveedor" : "Editar proveedor",
            Content = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 560
            },
            PrimaryButtonText = "Guardar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        dialogo.PrimaryButtonClick += (_, args) =>
        {
            var limpio = nombre.Text.Trim();
            if (limpio.Length == 0)
            {
                args.Cancel = true;
                error.Text = "Escriba el nombre del proveedor.";
                error.Visibility = Visibility.Visible;
                return;
            }

            if (App.Instance.Proveedores.ExisteNombre(limpio, id))
            {
                args.Cancel = true;
                error.Text = "Ya existe un proveedor con ese nombre.";
                error.Visibility = Visibility.Visible;
                return;
            }

            var rucLimpio = ProveedorRegistro.NormalizarRuc(ruc.Text);
            if (rucLimpio.Length > 0 && rucLimpio.Length != 11)
            {
                args.Cancel = true;
                error.Text = "El RUC debe tener 11 dígitos.";
                error.Visibility = Visibility.Visible;
                return;
            }

            var conAcuerdo = acuerdo.IsChecked == true;
            if (conAcuerdo && (double.IsNaN(cantidadContrato.Value) || cantidadContrato.Value <= 0))
            {
                args.Cancel = true;
                error.Text = "Indique la cantidad que el contrato obliga a comprar.";
                error.Visibility = Visibility.Visible;
                return;
            }

            error.Visibility = Visibility.Collapsed;
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var registro = new ProveedorRegistro
        {
            Id = id,
            Nombre = nombre.Text,
            Canal1 = (canal1.SelectedItem as CanalDatoOpcion)?.Id ?? CanalDatoProveedor.WhatsApp,
            Dato1 = dato1.Text,
            Canal2 = (canal2.SelectedItem as CanalDatoOpcion)?.Id ?? CanalDatoProveedor.Web,
            Dato2 = dato2.Text,
            Notas = notas.Text,
            Calificacion = calificacion.SelectedItem as string ?? CalificacionProveedor.SinCalificar,
            Ruc = ruc.Text,
            RazonSocial = razonSocial.Text,
            TiempoEntregaDias = double.IsNaN(tiempoEntrega.Value) ? 0 : (int)Math.Round(tiempoEntrega.Value),
            AcuerdoContrato = acuerdo.IsChecked == true,
            CantidadContrato = acuerdo.IsChecked == true && !double.IsNaN(cantidadContrato.Value)
                ? (decimal)cantidadContrato.Value
                : 0,
            UnidadContrato = acuerdo.IsChecked == true ? unidadContrato.Text : string.Empty,
            FechaAgregado = origen?.FechaAgregado ?? DateTimeOffset.Now,
            FechaContacto = DateTimeOffset.Now
        };
        if (!App.Instance.Proveedores.Guardar(registro))
        {
            Mostrar(InfoBarSeverity.Error, "No se pudo guardar el proveedor.");
            return;
        }

        var guardado = App.Instance.Proveedores.Obtener(id) ?? registro;
        ProveedorFichaSync.Publicar(origen?.Nombre ?? guardado.Nombre, guardado);
        RefrescarLista(true);
        Mostrar(
            InfoBarSeverity.Success,
            esNuevo
                ? "Proveedor agregado. También queda en el historial de la ficha técnica."
                : "Proveedor actualizado en esta lista y en las fichas que lo usan.");
    }

    private ComboBox CrearComboCanal(string? canalId)
    {
        var combo = new ComboBox
        {
            Header = "Red o web",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Bottom,
            ItemTemplate = (DataTemplate)Resources["CanalDatoPlantilla"]
        };
        foreach (var opcion in CanalDatoProveedor.Opciones)
        {
            combo.Items.Add(opcion);
        }

        var elegido = CanalDatoProveedor.De(canalId);
        combo.SelectedItem = combo.Items.OfType<CanalDatoOpcion>().First(o => o.Id == elegido.Id);
        return combo;
    }

    private async void Eliminar_Click(object sender, RoutedEventArgs e)
    {
        if (!TryId(sender, out var id) || App.Instance.Proveedores.Obtener(id) is not { } proveedor)
        {
            return;
        }

        var fichas = ProveedorFichaSync.Contar(proveedor.Nombre);
        var detalle = fichas == 0
            ? "Se quita de esta lista y del historial de la ficha técnica."
            : fichas == 1
                ? "Se quita de 1 ficha técnica y del historial. El producto conserva el resto de sus datos."
                : $"Se quita de {fichas} fichas técnicas y del historial. Los productos conservan el resto de sus datos.";
        var dialogo = new ContentDialog
        {
            Title = "Eliminar proveedor",
            Content = $"¿Eliminar «{proveedor.Nombre}»? {detalle} También se borran sus notas y su calificación.",
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        ProveedorFichaSync.Quitar(proveedor.Nombre);
        App.Instance.Proveedores.Eliminar(id);
        RefrescarLista(true);
        Mostrar(InfoBarSeverity.Success, "Proveedor eliminado de la lista y de las fichas técnicas.");
    }

    private void Notas_LostFocus(object sender, RoutedEventArgs e)
    {
        if (ListaSuspendida || sender is not TextBox caja || !TryId(caja, out var id))
        {
            return;
        }

        App.Instance.Proveedores.ActualizarNotas(id, caja.Text);
    }

    private void Canal1_Loaded(object sender, RoutedEventArgs e) => CargarCanal(sender, 1);

    private void Canal2_Loaded(object sender, RoutedEventArgs e) => CargarCanal(sender, 2);

    private void CargarCanal(object sender, int indice)
    {
        if (sender is not ComboBox combo || !TryId(combo, out var id))
        {
            return;
        }

        var proveedor = App.Instance.Proveedores.Obtener(id);
        if (proveedor is null)
        {
            return;
        }

        var canal = CanalDatoProveedor.De(indice == 2 ? proveedor.Canal2 : proveedor.Canal1);
        var indiceOpcion = 0;
        for (var i = 0; i < CanalDatoProveedor.Opciones.Count; i++)
        {
            if (CanalDatoProveedor.Opciones[i].Id == canal.Id)
            {
                indiceOpcion = i;
                break;
            }
        }

        SuspenderLista();
        combo.SelectedIndex = indiceOpcion;
        ReanudarLista();
        if (combo.Parent is Grid grid)
        {
            foreach (var hijo in grid.Children.OfType<TextBox>())
            {
                hijo.PlaceholderText = canal.Placeholder;
            }
        }
    }

    private void Canal1_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        GuardarCanal(sender, 1);

    private void Canal2_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        GuardarCanal(sender, 2);

    private void GuardarCanal(object sender, int indice)
    {
        if (ListaSuspendida || sender is not ComboBox combo || combo.SelectedItem is not CanalDatoOpcion opcion ||
            !TryId(combo, out var id))
        {
            return;
        }

        var proveedor = App.Instance.Proveedores.Obtener(id);
        if (proveedor is null)
        {
            return;
        }

        var actual = indice == 2 ? proveedor.Canal2 : proveedor.Canal1;
        if (string.Equals(actual, opcion.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (combo.Parent is Grid grid)
        {
            foreach (var hijo in grid.Children.OfType<TextBox>())
            {
                hijo.PlaceholderText = opcion.Placeholder;
            }
        }

        var nombre = proveedor.Nombre;
        if (!App.Instance.Proveedores.ActualizarContacto(id, indice, opcion.Id, null, cambiarCanal: true, cambiarDato: false))
        {
            return;
        }

        if (App.Instance.Proveedores.Obtener(id) is { } actualizado)
        {
            ProveedorFichaSync.Publicar(nombre, actualizado);
        }
    }

    private void Dato1_LostFocus(object sender, RoutedEventArgs e) => GuardarDato(sender, 1);

    private void Dato2_LostFocus(object sender, RoutedEventArgs e) => GuardarDato(sender, 2);

    private void GuardarDato(object sender, int indice)
    {
        if (ListaSuspendida || sender is not TextBox caja || !TryId(caja, out var id))
        {
            return;
        }

        var proveedor = App.Instance.Proveedores.Obtener(id);
        if (proveedor is null)
        {
            return;
        }

        var actual = indice == 2 ? proveedor.Dato2 : proveedor.Dato1;
        if (string.Equals(actual, caja.Text.Trim(), StringComparison.Ordinal))
        {
            return;
        }

        var nombre = proveedor.Nombre;
        if (!App.Instance.Proveedores.ActualizarContacto(id, indice, null, caja.Text, cambiarCanal: false, cambiarDato: true))
        {
            return;
        }

        if (App.Instance.Proveedores.Obtener(id) is { } actualizado)
        {
            ProveedorFichaSync.Publicar(nombre, actualizado);
        }
    }

    private async void AbrirCanal_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Parent: Grid grid })
        {
            return;
        }

        var combo = grid.Children.OfType<ComboBox>().FirstOrDefault();
        var caja = grid.Children.OfType<TextBox>().FirstOrDefault();
        var canal = combo?.SelectedItem is CanalDatoOpcion opcion
            ? opcion.Id
            : CanalDatoProveedor.Web;
        var dato = caja?.Text ?? string.Empty;
        var uri = CanalDatoProveedor.Uri(canal, dato);
        if (uri is null)
        {
            var titulo = CanalDatoProveedor.De(canal).Titulo;
            Mostrar(
                InfoBarSeverity.Warning,
                titulo == "WhatsApp"
                    ? "Indique un celular peruano de 9 dígitos (empieza en 9)."
                    : $"Escriba un {titulo.ToLowerInvariant()} válido.");
            return;
        }

        if (!await Windows.System.Launcher.LaunchUriAsync(uri))
        {
            Mostrar(InfoBarSeverity.Error, $"No se pudo abrir {CanalDatoProveedor.De(canal).Titulo}.");
        }
    }

    private void ComboCalificacion_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox combo || !TryId(combo, out var id))
        {
            return;
        }

        var proveedor = App.Instance.Proveedores.Obtener(id);
        if (proveedor is null)
        {
            return;
        }

        SuspenderLista();
        combo.SelectedItem = CalificacionProveedor.Normalizar(proveedor.Calificacion);
        ReanudarLista();
    }

    private void Calificacion_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListaSuspendida || sender is not ComboBox combo || combo.SelectedItem is not string elegida ||
            !TryId(combo, out var id))
        {
            return;
        }

        var proveedor = App.Instance.Proveedores.Obtener(id);
        if (proveedor is null ||
            string.Equals(proveedor.Calificacion, elegida, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        App.Instance.Proveedores.ActualizarCalificacion(id, elegida);
        var filtro = CalificacionActual();
        if (filtro is not null && !string.Equals(filtro, elegida, StringComparison.OrdinalIgnoreCase))
        {
            RefrescarLista(false);
        }
    }

    private void TraerProductos_Click(object sender, RoutedEventArgs e)
    {
        var agregados = SincronizarFichas();
        RefrescarLista(true);
        Mostrar(
            agregados > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Informational,
            agregados > 0
                ? (agregados == 1
                    ? "Se agregó 1 proveedor desde las fichas técnicas."
                    : $"Se agregaron {agregados} proveedores desde las fichas técnicas.")
                : "Los proveedores ya coinciden con las fichas técnicas.");
    }

    private static int SincronizarFichas() =>
        App.Instance.Proveedores.SincronizarDesdeFichas(
            App.Instance.Repositorio.Todas,
            AlmacenHistorialProveedores.Guardados);

    private bool ListaSuspendida => _listaSuspendida > 0;

    private void SuspenderLista() => _listaSuspendida++;

    private void ReanudarLista() => _listaSuspendida = Math.Max(0, _listaSuspendida - 1);

    private static bool TryId(object sender, out Guid id)
    {
        id = Guid.Empty;
        return sender is FrameworkElement { Tag: string texto } && Guid.TryParse(texto, out id);
    }

    private void Mostrar(InfoBarSeverity gravedad, string mensaje)
    {
        Barra.Severity = gravedad;
        Barra.Message = mensaje;
        Barra.IsOpen = true;
    }
}
