using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using IdermaFichas.Services;

namespace IdermaFichas.Models;

public static class CalculoOrdenCompra
{
    public const double TasaIgv = 0.18;
    public const string CodigoFormato = "FO0LOG015";
    public const int MesesConsumo = 6;

    public static readonly string[] TiposPrecio = ["Contrato", "Promoción", "Estimado"];
    public static readonly string[] EstadosDocumento = ["Borrador", "En revisión", "Aprobada"];

    /// <summary>
    /// El último precio registrado se toma con IGV incluido.
    /// El precio sin IGV es ese monto menos el 18 %.
    /// </summary>
    public static (double SinIgv, double Igv) DesglosarUnitario(double precioConIgv)
    {
        if (precioConIgv <= 0 || double.IsNaN(precioConIgv) || double.IsInfinity(precioConIgv))
        {
            return (0, 0);
        }

        var igv = Redondear(precioConIgv * TasaIgv);
        return (Redondear(precioConIgv - igv), igv);
    }

    public static double IgvDeTotal(double totalConIgv) =>
        totalConIgv <= 0 || double.IsNaN(totalConIgv) ? 0 : Redondear(totalConIgv * TasaIgv);

    public static double Redondear(double monto) =>
        Math.Round(monto, 2, MidpointRounding.AwayFromZero);

    public static double ConsumoPromedioMensual(IEnumerable<MovimientoInventario> movimientos)
    {
        var desde = DateTimeOffset.Now.AddMonths(-MesesConsumo);
        var salidas = movimientos
            .Where(m => m.EsSalida && m.Fecha >= desde)
            .Sum(m => m.Cantidad);
        return Redondear(salidas / MesesConsumo);
    }

    public static double ConsumoEnPeriodo(
        IEnumerable<MovimientoInventario> movimientos,
        int? anio,
        IReadOnlySet<int> meses,
        DateTimeOffset? desde,
        int? atajoMeses)
    {
        if (atajoMeses is int ventana && desde is DateTimeOffset inicio)
        {
            var recientes = movimientos
                .Where(m => m.EsSalida && m.Fecha >= inicio)
                .Sum(m => m.Cantidad);
            return Redondear(recientes / Math.Max(1, ventana));
        }

        if (anio is null && meses.Count == 0)
        {
            return ConsumoPromedioMensual(movimientos);
        }

        var salidas = FiltroPeriodoIngresos.Aplicar(movimientos.Where(m => m.EsSalida), anio, meses);
        var divisor = meses.Count > 0 ? meses.Count : 12;
        return Redondear(salidas.Sum(m => m.Cantidad) / divisor);
    }
}

public sealed class LineaOrdenCompra : INotifyPropertyChanged
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private double _cantidadPedir;
    private string _tipoPrecio = "Estimado";

    public Guid FichaId { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Marca { get; init; } = string.Empty;
    public string Proveedor { get; init; } = string.Empty;
    public string Ruc { get; set; } = string.Empty;
    public string Unidad { get; init; } = string.Empty;
    public double ConsumoPromedio { get; init; }
    public double StockActual { get; init; }
    public double PrecioConIgv { get; init; }
    public bool TienePrecio { get; init; }
    public string Moneda { get; init; } = MonedaPrecio.Soles;
    public IReadOnlyList<string> Tipos => CalculoOrdenCompra.TiposPrecio;

    public double CantidadPedir
    {
        get => _cantidadPedir;
        set
        {
            var cantidad = double.IsNaN(value) || double.IsInfinity(value) || value < 0 ? 0 : value;
            if (Math.Abs(_cantidadPedir - cantidad) < 0.0000001)
            {
                return;
            }

            _cantidadPedir = cantidad;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextoSinIgv));
            OnPropertyChanged(nameof(TextoIgv));
            OnPropertyChanged(nameof(TextoTotal));
            OnPropertyChanged(nameof(TextoEstado));
        }
    }

    public string TipoPrecio
    {
        get => _tipoPrecio;
        set
        {
            var tipo = string.IsNullOrWhiteSpace(value) ? "Estimado" : value.Trim();
            if (_tipoPrecio == tipo)
            {
                return;
            }

            _tipoPrecio = tipo;
            OnPropertyChanged();
        }
    }

    public double PrecioSinIgv => CalculoOrdenCompra.DesglosarUnitario(PrecioConIgv).SinIgv;
    public double IgvUnitario => CalculoOrdenCompra.DesglosarUnitario(PrecioConIgv).Igv;

    /// <summary>
    /// Importe de la fila. Con cantidad 0 muestra el precio unitario para que el total no quede en cero.
    /// </summary>
    public double ImporteConIgv =>
        !TienePrecio ? 0 :
        CantidadPedir > 0.0001
            ? CalculoOrdenCompra.Redondear(PrecioConIgv * CantidadPedir)
            : PrecioConIgv;

    public double ImporteIgv =>
        !TienePrecio ? 0 :
        CantidadPedir > 0.0001
            ? CalculoOrdenCompra.IgvDeTotal(ImporteConIgv)
            : IgvUnitario;

    public double ImporteSinIgv =>
        !TienePrecio ? 0 : CalculoOrdenCompra.Redondear(ImporteConIgv - ImporteIgv);

    public string TextoConsumo => ConsumoPromedio.ToString("0.##", Cultura);
    public string TextoStock => StockActual.ToString("0.##", Cultura);
    public string TextoUnidad => string.IsNullOrWhiteSpace(Unidad) ? "und" : Unidad.Trim();
    public string TextoMarca => TextoOGuion(Marca);
    public string TextoProveedor => TextoOGuion(Proveedor);
    public string TextoRuc => TextoOGuion(Ruc);
    public string TextoUltimo => TienePrecio ? MonedaPrecio.Formato(PrecioConIgv, Moneda) : "Sin precio";
    public string TextoSinIgv => TienePrecio ? MonedaPrecio.Formato(ImporteSinIgv, Moneda) : "—";
    public string TextoIgv => TienePrecio ? MonedaPrecio.Formato(ImporteIgv, Moneda) : "—";
    public string TextoTotal => TienePrecio ? MonedaPrecio.Formato(ImporteConIgv, Moneda) : "—";
    public string TextoEstado =>
        !TienePrecio ? "Sin precio" :
        CantidadPedir > 0.0001 ? "Por pedir" : "Cubierto";

    public event PropertyChangedEventHandler? PropertyChanged;

    public LineaOrdenExportacion Exportar() => new(
        string.IsNullOrWhiteSpace(Codigo) ? "—" : Codigo.Trim(),
        string.IsNullOrWhiteSpace(Nombre) ? "—" : Nombre.Trim(),
        TextoMarca,
        TextoProveedor,
        TextoRuc,
        ConsumoPromedio,
        StockActual,
        CantidadPedir,
        TextoUnidad,
        ImporteSinIgv,
        ImporteIgv,
        ImporteConIgv,
        TipoPrecio,
        TextoEstado,
        TienePrecio,
        MonedaPrecio.Normalizar(Moneda))
    {
        FichaId = FichaId
    };

    private void OnPropertyChanged([CallerMemberName] string? nombre = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));

    private static string TextoOGuion(string valor) =>
        string.IsNullOrWhiteSpace(valor) ? "—" : valor.Trim();
}

public sealed record LineaOrdenExportacion(
    string Codigo,
    string Nombre,
    string Marca,
    string Proveedor,
    string Ruc,
    double ConsumoPromedio,
    double StockActual,
    double CantidadPedir,
    string Unidad,
    double PrecioUnitarioSinIgv,
    double Igv,
    double TotalConIgv,
    string TipoPrecio,
    string Estado,
    bool TienePrecio,
    string Moneda)
{
    public Guid FichaId { get; init; }
}

public sealed class OrdenCompraDocumento
{
    public string CodigoFormato { get; init; } = CalculoOrdenCompra.CodigoFormato;
    public string MesAnio { get; init; } = string.Empty;
    public string FechaElaboracion { get; init; } = string.Empty;
    public string ElaboradoPor { get; init; } = string.Empty;
    public string RevisadoPor { get; init; } = string.Empty;
    public string PeriodoCobertura { get; init; } = string.Empty;
    public string Estado { get; init; } = "Borrador";
    public List<LineaOrdenExportacion> Lineas { get; init; } = [];

    public string TituloImpresion
    {
        get
        {
            var fecha = string.IsNullOrWhiteSpace(FechaElaboracion)
                ? DateTime.Today.ToString("dd/MM/yyyy")
                : FechaElaboracion.Trim();
            var codigo = string.IsNullOrWhiteSpace(CodigoFormato)
                ? CalculoOrdenCompra.CodigoFormato
                : CodigoFormato.Trim();
            return $"ORDEN DE COMPRA {fecha} {codigo}";
        }
    }

    public string TituloPrecios =>
        Lineas.Any(l => l.TienePrecio && MonedaPrecio.EsDolar(l.Moneda))
            ? "PRECIOS"
            : "PRECIOS (S/)";
}

public sealed class OrdenCompraRegistrada
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset FechaRegistro { get; set; } = DateTimeOffset.Now;
    public OrdenCompraDocumento Documento { get; set; } = new();
}
