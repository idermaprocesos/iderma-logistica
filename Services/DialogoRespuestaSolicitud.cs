using IdermaFichas.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IdermaFichas.Services;

public enum DecisionRespuestaSolicitud
{
    Aceptar,
    CantidadAcordada,
    Denegar
}

public sealed class ResultadoRespuestaSolicitud
{
    public required DecisionRespuestaSolicitud Decision { get; init; }
    public required IReadOnlyList<int> Cantidades { get; init; }
    public required string Motivo { get; init; }
}

public static class RespuestasNegativasPedido
{
    public static readonly string[] Opciones =
    [
        "Son las últimas que quedan",
        "Hay que ahorrar",
        "No hay stock suficiente",
        "Hay que reservar para urgencias",
        "Producto agotado",
        "Lote próximo a vencer",
        "Pedido duplicado",
        "Fuera de lo autorizado",
        "Otra"
    ];
}

public static class DialogoRespuestaSolicitud
{
    public static async Task<ResultadoRespuestaSolicitud?> MostrarAsync(XamlRoot raiz, SolicitudClinica solicitud)
    {
        ArgumentNullException.ThrowIfNull(raiz);
        ArgumentNullException.ThrowIfNull(solicitud);

        var cajas = new List<NumberBox>();
        var lineas = new StackPanel { Spacing = 8 };
        foreach (var linea in solicitud.Items)
        {
            var pedida = Math.Max(0, linea.Cantidad);
            var caja = new NumberBox
            {
                Header = string.IsNullOrWhiteSpace(linea.Nombre) ? linea.Codigo : linea.Nombre,
                Minimum = 0,
                Maximum = pedida,
                Value = pedida,
                SmallChange = 1,
                LargeChange = 1,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
            };
            cajas.Add(caja);
            lineas.Children.Add(caja);
        }

        var combo = new ComboBox
        {
            Header = "Respuesta al pedido",
            PlaceholderText = "Elige un motivo",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = RespuestasNegativasPedido.Opciones
        };
        var otra = new TextBox
        {
            Header = "Escribe la respuesta",
            PlaceholderText = "Motivo",
            Visibility = Visibility.Collapsed,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 72,
            MaxLength = 500
        };
        combo.SelectionChanged += (_, _) =>
        {
            otra.Visibility = combo.SelectedItem as string == "Otra"
                ? Visibility.Visible
                : Visibility.Collapsed;
        };

        var error = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Application.Current.Resources.TryGetValue("TextFillColorCriticalBrush", out var pincel) &&
                         pincel is Microsoft.UI.Xaml.Media.Brush brush
                ? brush
                : null,
            Visibility = Visibility.Collapsed
        };

        var panel = new StackPanel { Spacing = 12, Width = 460 };
        panel.Children.Add(new TextBlock
        {
            Text = $"{solicitud.Folio} · {solicitud.Enfermera}",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
            Text = "Ajusta la cantidad de cada producto. Aceptar entrega lo pedido. Cantidad acordada entrega lo que escribas y pide un motivo. Denegar no saca stock."
        });
        panel.Children.Add(new ScrollViewer
        {
            MaxHeight = 280,
            Content = lineas
        });
        panel.Children.Add(combo);
        panel.Children.Add(otra);
        panel.Children.Add(error);

        var aceptar = new Button { Content = "Aceptar" };
        if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out var estilo) && estilo is Style acento)
        {
            aceptar.Style = acento;
        }
        var acordada = new Button { Content = "Aceptar cantidad acordada" };
        var denegar = new Button { Content = "Denegar" };
        var acciones = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        acciones.Children.Add(denegar);
        acciones.Children.Add(acordada);
        acciones.Children.Add(aceptar);
        panel.Children.Add(acciones);

        var dialogo = new ContentDialog
        {
            Title = "Responder solicitud",
            Content = panel,
            CloseButtonText = "Cancelar",
            XamlRoot = raiz
        };

        ResultadoRespuestaSolicitud? resultado = null;
        aceptar.Click += (_, _) =>
        {
            if (Intentar(DecisionRespuestaSolicitud.Aceptar, out var elegido))
            {
                resultado = elegido;
                dialogo.Hide();
            }
        };
        acordada.Click += (_, _) =>
        {
            if (Intentar(DecisionRespuestaSolicitud.CantidadAcordada, out var elegido))
            {
                resultado = elegido;
                dialogo.Hide();
            }
        };
        denegar.Click += (_, _) =>
        {
            if (Intentar(DecisionRespuestaSolicitud.Denegar, out var elegido))
            {
                resultado = elegido;
                dialogo.Hide();
            }
        };

        await dialogo.ShowAsync();
        return resultado;

        bool Intentar(DecisionRespuestaSolicitud decision, out ResultadoRespuestaSolicitud? elegido)
        {
            elegido = null;
            var cantidades = cajas.Select(LeerCantidad).ToList();
            var pedidas = solicitud.Items.Select(i => Math.Max(0, i.Cantidad)).ToList();
            var motivo = MotivoSeleccionado();

            string? fallo = decision switch
            {
                DecisionRespuestaSolicitud.Aceptar when cantidades.Count == 0 || cantidades.Any(c => c <= 0)
                    => "Indica una cantidad mayor que cero para aceptar.",
                DecisionRespuestaSolicitud.Aceptar when !cantidades.SequenceEqual(pedidas)
                    => "Para entregar otra cantidad usa Aceptar cantidad acordada.",
                DecisionRespuestaSolicitud.CantidadAcordada when cantidades.Sum() <= 0
                    => "Indica al menos una pieza en la cantidad acordada.",
                DecisionRespuestaSolicitud.CantidadAcordada when cantidades.SequenceEqual(pedidas)
                    => "La cantidad es la misma que pidieron. Usa Aceptar, o baja alguna cantidad.",
                DecisionRespuestaSolicitud.CantidadAcordada when string.IsNullOrWhiteSpace(motivo)
                    => "Elige por qué no se entrega todo lo pedido.",
                DecisionRespuestaSolicitud.Denegar when cantidades.Sum() <= 0
                    => "Indica cuántas piezas se deniegan.",
                DecisionRespuestaSolicitud.Denegar when string.IsNullOrWhiteSpace(motivo)
                    => "Elige una respuesta para la denegación.",
                _ => null
            };

            if (fallo is not null)
            {
                error.Text = fallo;
                error.Visibility = Visibility.Visible;
                return false;
            }

            error.Visibility = Visibility.Collapsed;
            elegido = new ResultadoRespuestaSolicitud
            {
                Decision = decision,
                Cantidades = cantidades,
                Motivo = motivo
            };
            return true;
        }

        string MotivoSeleccionado()
        {
            var opcion = combo.SelectedItem as string ?? string.Empty;
            if (opcion == "Otra")
            {
                return otra.Text.Trim();
            }

            return opcion.Trim();
        }
    }

    private static int LeerCantidad(NumberBox caja)
    {
        if (double.IsNaN(caja.Value) || double.IsInfinity(caja.Value))
        {
            return 0;
        }

        var tope = double.IsNaN(caja.Maximum) ? caja.Value : caja.Maximum;
        return (int)Math.Clamp(Math.Round(caja.Value), 0, Math.Max(0, tope));
    }
}
