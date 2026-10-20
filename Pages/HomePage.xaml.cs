using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Windows.UI;

namespace IdermaFichas.Pages;

public sealed partial class HomePage : Page, IVistaInventario
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-MX");
    private readonly DispatcherTimer _reloj = new() { Interval = TimeSpan.FromSeconds(30) };

    public HomePage()
    {
        InitializeComponent();
        _reloj.Tick += (_, _) => ActualizarSaludo();
        Loaded += (_, _) => _reloj.Start();
        Unloaded += (_, _) => _reloj.Stop();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => AplicarCambioInventario();

    public void AplicarCambioInventario()
    {
        ActualizarSaludo();
        var repo = App.Instance.Repositorio;
        var todas = repo.Todas;
        var total = todas.Count;
        var vencidos = repo.PartidasVencidas().Count();
        var proximos = repo.PartidasProximas(30).Count();
        var stockBajo = todas.Count(f => f.EstaStockCritico);
        var actualizadosHoy = todas.Count(f => f.FechaActualizacion.Date == DateTime.Today);
        var ultima = todas.Count == 0
            ? (DateTimeOffset?)null
            : todas.Max(f => f.FechaActualizacion);

        var tarjetas = AreasOperativas.Todas.Select(area =>
        {
            var n = repo.Contar(area.Id);
            return new AreaResumenVista
            {
                Id = area.Id,
                Nombre = area.Nombre,
                Descripcion = area.Descripcion,
                ConteoNumero = n,
                Conteo = n.ToString("N0", Cultura),
                Porcentaje = Porcentaje(n, total),
                ColorBarra = ColorDeArea(area)
            };
        }).ToList();
        var sinArea = repo.Contar(null);
        if (sinArea > 0)
        {
            tarjetas.Add(new AreaResumenVista
            {
                Id = null,
                Nombre = "Sin área",
                Descripcion = "Productos que no pertenecen a ningún área operativa.",
                ConteoNumero = sinArea,
                Conteo = sinArea.ToString("N0", Cultura),
                Porcentaje = Porcentaje(sinArea, total),
                ColorBarra = Color.FromArgb(255, 96, 96, 96)
            });
        }

        PintarTarjetasArea(tarjetas);

        KpiTotal.Text = total.ToString("N0", Cultura);
        KpiVencidos.Text = vencidos.ToString("N0", Cultura);
        KpiProximos.Text = proximos.ToString("N0", Cultura);
        KpiStockBajo.Text = stockBajo.ToString("N0", Cultura);

        DetalleVencidos.Text = vencidos == 0 && proximos == 0
            ? "No hay materiales vencidos ni próximos a caducar en 30 días."
            : $"{vencidos} lote(s) vencido(s) y {proximos} por vencer. Priorice reposición y baja de lote.";

        DetalleActividad.Text = actualizadosHoy == 0
            ? "Hoy no se han actualizado fichas. El catálogo permanece estable."
            : $"{actualizadosHoy} ficha(s) se actualizaron hoy.";

        TextoUltimaActualizacion.Text = ultima is DateTimeOffset fecha
            ? $"Última edición: {fecha.ToLocalTime():dd/MM/yyyy HH:mm}"
            : "Sin movimientos";

        var ultimoMovimiento = UltimosMovimientosLocales();
        var recientes = todas
            .OrderByDescending(f => f.FechaActualizacion)
            .Take(8)
            .Select(f => new FichaRecienteVista
            {
                Ficha = f,
                ActualizadoTexto = f.FechaActualizacion.ToLocalTime().ToString("dd/MM HH:mm"),
                Explicacion = ExplicarLocal(f, ultimoMovimiento.GetValueOrDefault(f.Id))
            })
            .ToList();
        ListaRecientes.ItemsSource = recientes;

        TextoResumenDia.Text = ArmarResumen(total, vencidos, proximos, stockBajo);
        TextoNube.Text = string.IsNullOrWhiteSpace(ModulosNube.Estado)
            ? InventarioNube.Estado
            : InventarioNube.Estado + "\n" + ModulosNube.Estado;
    }

    private void ActualizarSaludo()
    {
        var ahora = DateTime.Now;
        var hora = ahora.Hour;
        var saludo = hora switch
        {
            >= 5 and < 12 => "Buenos días",
            >= 12 and < 19 => "Buenas tardes",
            _ => "Buenas noches"
        };

        SaludoPrincipal.Text = $"{saludo}, equipo Iderma";
        TextoHora.Text = ahora.ToString("HH:mm");
        var textoFecha = ahora.ToString("dddd d 'de' MMMM 'de' yyyy", Cultura);
        TextoFecha.Text = string.IsNullOrEmpty(textoFecha)
            ? ahora.ToString("dd/MM/yyyy")
            : char.ToUpper(textoFecha[0], Cultura) + textoFecha[1..];
    }

    private static string ArmarResumen(int total, int vencidos, int proximos, int stockBajo)
    {
        if (total == 0)
        {
            return "Aún no hay productos en el catálogo. Importe un Excel o cree la primera ficha.";
        }

        if (vencidos > 0)
        {
            return $"Hay {vencidos} lote(s) vencido(s). Revise caducidad antes de usar material en consulta.";
        }

        if (proximos > 0)
        {
            return $"{proximos} lote(s) caducan en 30 días. Planifique reposición con el proveedor.";
        }

        if (stockBajo > 0)
        {
            return $"{stockBajo} producto(s) están en stock mínimo. Conviene reordenar existencias.";
        }

        return $"Inventario en orden: {total} producto(s) activos, sin alertas de caducidad ni stock crítico.";
    }

    private void PintarTarjetasArea(IReadOnlyList<AreaResumenVista> tarjetas)
    {
        PanelTarjetasArea.Children.Clear();
        PanelTarjetasArea.ColumnDefinitions.Clear();
        PanelTarjetasArea.RowDefinitions.Clear();
        if (tarjetas.Count == 0)
        {
            return;
        }

        var columnas = tarjetas.Count <= 3 ? tarjetas.Count : (tarjetas.Count == 4 ? 4 : 3);
        for (var i = 0; i < columnas; i++)
        {
            PanelTarjetasArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var filas = (int)Math.Ceiling(tarjetas.Count / (double)columnas);
        for (var i = 0; i < filas; i++)
        {
            PanelTarjetasArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var i = 0; i < tarjetas.Count; i++)
        {
            var vista = tarjetas[i];
            var boton = CrearTarjetaArea(vista);
            Grid.SetColumn(boton, i % columnas);
            Grid.SetRow(boton, i / columnas);
            PanelTarjetasArea.Children.Add(boton);
        }
    }

    private Button CrearTarjetaArea(AreaResumenVista vista)
    {
        var boton = new Button
        {
            Style = (Style)Application.Current.Resources["TarjetaAreaStyle"],
            Tag = vista,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        boton.Click += TarjetaArea_Click;

        var color = new SolidColorBrush(vista.ColorBarra);
        var raiz = new Grid { Padding = new Thickness(20), RowSpacing = 8 };
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        raiz.Children.Add(new Border
        {
            Width = 40,
            Height = 6,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = color,
            CornerRadius = new CornerRadius(3)
        });

        var titulo = new TextBlock
        {
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Text = vista.Nombre,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(titulo, 1);
        raiz.Children.Add(titulo);

        var descripcion = new TextBlock
        {
            Opacity = 0.72,
            Text = vista.Descripcion,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(descripcion, 2);
        raiz.Children.Add(descripcion);

        var conteo = new TextBlock
        {
            Margin = new Thickness(0, 4, 0, 0),
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = color,
            Text = vista.Conteo
        };
        Grid.SetRow(conteo, 3);
        raiz.Children.Add(conteo);

        var porcentaje = new TextBlock
        {
            Opacity = 0.6,
            FontSize = 12,
            Text = vista.Porcentaje
        };
        Grid.SetRow(porcentaje, 4);
        raiz.Children.Add(porcentaje);

        boton.Content = raiz;
        return boton;
    }

    private static Color ColorDeArea(AreaDefinicion area)
    {
        if (area.Id == AreasOperativas.IdClinica)
        {
            return Color.FromArgb(255, 11, 110, 107);
        }

        if (area.Id == AreasOperativas.IdOficina || area.Id == AreasOperativas.IdProductosVenta)
        {
            return Color.FromArgb(255, 27, 79, 114);
        }

        if (area.Id == AreasOperativas.IdLaboratorio)
        {
            return Color.FromArgb(255, 122, 80, 160);
        }

        if (area.Id == AreasOperativas.IdLimpieza)
        {
            return Color.FromArgb(255, 46, 125, 79);
        }

        var paleta = new[]
        {
            Color.FromArgb(255, 11, 110, 107),
            Color.FromArgb(255, 27, 79, 114),
            Color.FromArgb(255, 46, 125, 79),
            Color.FromArgb(255, 122, 80, 160),
            Color.FromArgb(255, 192, 86, 0)
        };
        return paleta[Math.Abs(area.Id.GetHashCode()) % paleta.Length];
    }

    private static string Porcentaje(int parte, int total)
    {
        if (total == 0)
        {
            return "0% del catálogo";
        }

        return $"{100.0 * parte / total:0.#}% del catálogo";
    }

    private void TarjetaArea_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AreaResumenVista vista })
        {
            App.Instance.MainAppWindow?.IrAArea(vista.Id);
        }
    }

    private void AbrirCaducidad_Click(object sender, RoutedEventArgs e) =>
        App.Instance.MainAppWindow?.IrACaducidad();

    private void AbrirVencidos_Click(object sender, RoutedEventArgs e) =>
        App.Instance.MainAppWindow?.IrACaducidad(new CaducidadNavegacion());

    private void AbrirProximos_Click(object sender, RoutedEventArgs e) =>
        App.Instance.MainAppWindow?.IrACaducidad(new CaducidadNavegacion(Proximos: true));

    private void AbrirCatalogo_Click(object sender, RoutedEventArgs e) =>
        App.Instance.MainAppWindow?.IrACatalogo();

    private void AbrirStockBajo_Click(object sender, RoutedEventArgs e) =>
        App.Instance.MainAppWindow?.IrACatalogo(new CatalogoNavegacion(Stock: FiltroStockCatalogo.Critico));

    private async void ListaRecientes_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not FichaRecienteVista fila)
        {
            return;
        }

        var dialogo = new ContentDialog
        {
            Title = fila.Nombre,
            Content = new TextBlock
            {
                Text = fila.Explicacion,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 460
            },
            CloseButtonText = "Cerrar",
            XamlRoot = XamlRoot
        };
        await dialogo.ShowAsync();
    }

    private static Dictionary<Guid, MovimientoInventario> UltimosMovimientosLocales()
    {
        var ultimos = new Dictionary<Guid, MovimientoInventario>();
        foreach (var movimiento in App.Instance.Registro.CopiarTodos())
        {
            if (!ultimos.TryGetValue(movimiento.FichaId, out var anterior) || movimiento.Fecha > anterior.Fecha)
            {
                ultimos[movimiento.FichaId] = movimiento;
            }
        }

        return ultimos;
    }

    private static string ExplicarLocal(FichaTecnica ficha, MovimientoInventario? ultimo)
    {
        var unidad = string.IsNullOrWhiteSpace(ficha.UnidadMedida) ? string.Empty : " " + ficha.UnidadMedida.Trim();
        var existencia = ficha.Existencia.ToString("0.##", Cultura);
        var stock = ficha.StockMinimo > 0
            ? ficha.EstaStockCritico
                ? $"En este equipo hay {existencia}{unidad}, en o bajo el mínimo de {ficha.StockMinimo.ToString("0.##", Cultura)}."
                : $"En este equipo hay {existencia}{unidad}, por encima del mínimo de {ficha.StockMinimo.ToString("0.##", Cultura)}."
            : $"En este equipo hay {existencia}{unidad}. No tiene mínimo definido.";

        var caducidad = ficha.EstaVencido
            ? "Hay un lote vencido en la copia local."
            : ficha.EstaProximoAVencer
                ? "Hay un lote que vence en los próximos 30 días, según la copia local."
                : "La copia local no marca caducidad vencida ni próxima.";

        var movimiento = ultimo is null
            ? "No hay entradas ni salidas de este producto en el registro de este equipo."
            : $"Último movimiento guardado aquí: {ultimo.DocumentoResumen}, {ultimo.Cantidad.ToString("0.##", Cultura)}{unidad}, el {ultimo.Fecha.ToLocalTime():dd/MM/yyyy HH:mm}. Después quedaron {ultimo.ExistenciaResultante.ToString("0.##", Cultura)}{unidad}.";

        var edicion = $"La ficha se editó en este equipo el {ficha.FechaActualizacion.ToLocalTime():dd/MM/yyyy HH:mm}.";
        return $"{stock} {caducidad} {movimiento} {edicion}";
    }
}

public sealed class AreaResumenVista
{
    public Guid? Id { get; init; }
    public required string Nombre { get; init; }
    public required string Descripcion { get; init; }
    public int ConteoNumero { get; init; }
    public required string Conteo { get; init; }
    public required string Porcentaje { get; init; }
    public Color ColorBarra { get; init; }
}

public sealed class FichaRecienteVista
{
    public required FichaTecnica Ficha { get; init; }
    public required string ActualizadoTexto { get; init; }
    public required string Explicacion { get; init; }
    public string Codigo => Ficha.Codigo;
    public string Nombre => Ficha.Nombre;
    public string AreaTitulo => Ficha.AreaTitulo;
    public string Existencia => Ficha.Existencia.ToString("0.##");
    public string FechaActualizacion => ActualizadoTexto;
}
