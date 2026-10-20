namespace IdermaFichas.Models;

public sealed class CaracteristicaProducto
{
    public const int MaximoPorProducto = 20;

    public string Nombre { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;

    public CaracteristicaProducto Clonar() => new()
    {
        Nombre = Nombre,
        Valor = Valor
    };
}
