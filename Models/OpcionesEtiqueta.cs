namespace IdermaFichas.Models;

public enum TipoSimboloEtiqueta
{
    Qr,
    Code128,
    Code39,
    Ean13,
    DataMatrix,
    Pdf417,
    Aztec
}

public enum TamanoHojaEtiqueta
{
    A4,
    Carta,
    Oficio,
    A5
}

public enum DisposicionEtiqueta
{
    Vertical,
    Horizontal
}

public sealed class FilaEtiqueta
{
    public FilaEtiqueta(string codigo, string area, string producto, int cantidad)
    {
        Codigo = codigo;
        Area = area;
        Producto = producto;
        Cantidad = cantidad;
    }

    public string Codigo { get; }
    public string Area { get; }
    public string Producto { get; }
    public int Cantidad { get; }

    public string ResumenCantidad =>
        Cantidad <= 0 ? "0 · no se imprime" : $"× {Cantidad}";
}

public enum OrdenTablaQr
{
    Alfabetico,
    Codigo
}

public sealed record ProductoQrLista(string Codigo, string Nombre, bool EnCatalogo);

public sealed class OpcionesEtiqueta
{
    public TipoSimboloEtiqueta Tipo { get; set; } = TipoSimboloEtiqueta.Qr;
    public int Columnas { get; set; } = 3;
    public int Filas { get; set; } = 8;
    public TamanoHojaEtiqueta Hoja { get; set; } = TamanoHojaEtiqueta.A4;
    public float TamanoMm { get; set; } = 20;
    public DisposicionEtiqueta Disposicion { get; set; } = DisposicionEtiqueta.Vertical;
    public bool GuiaCorte { get; set; }
}
