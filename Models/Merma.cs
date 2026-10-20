using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace IdermaFichas.Models;

public static class MotivosMerma
{
    public static readonly string[] Todos =
    [
        "Vencimiento próximo",
        "Vencido",
        "Producto dañado",
        "Contaminado",
        "Empaque roto",
        "Pérdida o faltante",
        "Mal almacenado",
        "Otro"
    ];

    public static string Sugerir(PartidaInventario? partida)
    {
        if (partida is null)
        {
            return "Producto dañado";
        }

        if (partida.EstaVencido)
        {
            return "Vencido";
        }

        if (partida.EstaProximoAVencer)
        {
            return "Vencimiento próximo";
        }

        return "Producto dañado";
    }
}

public sealed class LineaMermaEnvio : INotifyPropertyChanged
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private double _cantidad;
    private string _motivo = "Producto dañado";
    private string _detalle = string.Empty;

    public Guid FichaId { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Unidad { get; init; } = string.Empty;
    public string Lote { get; init; } = string.Empty;
    public string Caducidad { get; init; } = string.Empty;
    public double Stock { get; init; }
    public IReadOnlyList<string> Motivos => MotivosMerma.Todos;

    public double Cantidad
    {
        get => _cantidad;
        set
        {
            var cantidad = double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Clamp(value, 0, Math.Max(0, Stock));
            if (Math.Abs(_cantidad - cantidad) < 0.0000001)
            {
                return;
            }

            _cantidad = cantidad;
            Avisar();
        }
    }

    public string Motivo
    {
        get => _motivo;
        set
        {
            var motivo = string.IsNullOrWhiteSpace(value) ? "Producto dañado" : value.Trim();
            if (_motivo == motivo)
            {
                return;
            }

            _motivo = motivo;
            Avisar();
        }
    }

    public string Detalle
    {
        get => _detalle;
        set
        {
            _detalle = value ?? string.Empty;
            Avisar();
        }
    }

    public string ResumenLote => $"{Lote} · {Caducidad}";
    public string TextoStock => $"Stock {Stock.ToString("0.##", Cultura)} {Unidad}".Trim();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Avisar([CallerMemberName] string? nombre = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
}

public sealed class MermaRegistrada
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Now;
    public Guid FichaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Lote { get; set; } = string.Empty;
    public DateTimeOffset? FechaCaducidad { get; set; }
    public double Cantidad { get; set; }
    public string Unidad { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
}
