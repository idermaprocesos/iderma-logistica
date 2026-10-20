using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace IdermaFichas.Models;

public static class CalculoOrdenServicio
{
    public const double TasaIgv = 0.18;
    public const string CodigoFormato = "FO0LOG016";
    public static readonly string[] EstadosDocumento = ["Borrador", "En revisión", "Aprobada"];

    public static double Redondear(double monto) =>
        Math.Round(monto, 2, MidpointRounding.AwayFromZero);

    public static (double SinIgv, double Igv) Desglosar(double montoConIgv)
    {
        if (montoConIgv <= 0 || double.IsNaN(montoConIgv) || double.IsInfinity(montoConIgv))
        {
            return (0, 0);
        }

        var igv = Redondear(montoConIgv * TasaIgv);
        return (Redondear(montoConIgv - igv), igv);
    }
}

public sealed class LineaOrdenServicio : INotifyPropertyChanged
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private double _meses = 1;
    private double _montoMensual;
    private string _servicio = "Ampliación de contrato";

    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ProveedorId { get; init; }
    public string Proveedor { get; init; } = string.Empty;
    public string Ruc { get; set; } = string.Empty;
    public string Contrato { get; init; } = string.Empty;
    public DateTime? VigenciaActual { get; set; }

    public string Servicio
    {
        get => _servicio;
        set
        {
            _servicio = string.IsNullOrWhiteSpace(value) ? "Ampliación de contrato" : value.Trim();
            Avisar();
        }
    }

    public double Meses
    {
        get => _meses;
        set => Fijar(ref _meses, Math.Round(Math.Clamp(Normal(value, 1), 1, 36), 0, MidpointRounding.AwayFromZero));
    }

    public double MontoMensual
    {
        get => _montoMensual;
        set => Fijar(ref _montoMensual, Normal(value, 0));
    }

    public int MesesEnteros => (int)Meses;

    public DateTime NuevaVigencia
    {
        get
        {
            var inicio = VigenciaActual?.Date ?? DateTime.Today;
            if (inicio < DateTime.Today)
            {
                inicio = DateTime.Today;
            }

            return inicio.AddMonths(MesesEnteros);
        }
    }

    public double ImporteConIgv => CalculoOrdenServicio.Redondear(MontoMensual * MesesEnteros);
    public double ImporteIgv => CalculoOrdenServicio.Desglosar(ImporteConIgv).Igv;
    public double ImporteSinIgv => CalculoOrdenServicio.Desglosar(ImporteConIgv).SinIgv;
    public string TextoRuc => string.IsNullOrWhiteSpace(Ruc) ? "—" : Ruc.Trim();
    public string TextoVigencia => VigenciaActual is null ? "Sin término" : VigenciaActual.Value.ToString("dd/MM/yyyy");
    public string TextoNueva => NuevaVigencia.ToString("dd/MM/yyyy");
    public string TextoSinIgv => ImporteSinIgv.ToString("C", Cultura);
    public string TextoIgv => ImporteIgv.ToString("C", Cultura);
    public string TextoTotal => ImporteConIgv.ToString("C", Cultura);

    public event PropertyChangedEventHandler? PropertyChanged;

    public LineaOrdenServicioExportacion Exportar() => new(
        ProveedorId,
        Proveedor,
        Ruc,
        Contrato,
        Servicio,
        VigenciaActual,
        MesesEnteros,
        NuevaVigencia,
        MontoMensual,
        ImporteSinIgv,
        ImporteIgv,
        ImporteConIgv);

    private void Fijar(ref double campo, double valor, [CallerMemberName] string? nombre = null)
    {
        if (Math.Abs(campo - valor) < 0.0000001)
        {
            return;
        }

        campo = valor;
        Avisar(nombre);
        Avisar(nameof(TextoNueva));
        Avisar(nameof(TextoSinIgv));
        Avisar(nameof(TextoIgv));
        Avisar(nameof(TextoTotal));
    }

    private static double Normal(double valor, double defecto) =>
        double.IsNaN(valor) || double.IsInfinity(valor) || valor < 0 ? defecto : valor;

    private void Avisar([CallerMemberName] string? nombre = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
}

public sealed record LineaOrdenServicioExportacion(
    Guid ProveedorId,
    string Proveedor,
    string Ruc,
    string Contrato,
    string Servicio,
    DateTime? VigenciaActual,
    int Meses,
    DateTime NuevaVigencia,
    double MontoMensual,
    double SinIgv,
    double Igv,
    double TotalConIgv);

public sealed class OrdenServicioDocumento
{
    public string CodigoFormato { get; set; } = CalculoOrdenServicio.CodigoFormato;
    public string MesAnio { get; set; } = string.Empty;
    public string FechaElaboracion { get; set; } = string.Empty;
    public string ElaboradoPor { get; set; } = string.Empty;
    public string RevisadoPor { get; set; } = string.Empty;
    public string PeriodoCobertura { get; set; } = string.Empty;
    public string Estado { get; set; } = "Borrador";

    public string TituloImpresion
    {
        get
        {
            var fecha = string.IsNullOrWhiteSpace(FechaElaboracion)
                ? DateTime.Today.ToString("dd/MM/yyyy")
                : FechaElaboracion.Trim();
            var codigo = string.IsNullOrWhiteSpace(CodigoFormato)
                ? CalculoOrdenServicio.CodigoFormato
                : CodigoFormato.Trim();
            return $"ORDEN DE SERVICIO {fecha} {codigo}";
        }
    }
    public List<LineaOrdenServicioExportacion> Lineas { get; set; } = [];
}

public sealed class OrdenServicioRegistrada
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset FechaRegistro { get; set; } = DateTimeOffset.Now;
    public bool AmpliacionAplicada { get; set; }
    public OrdenServicioDocumento Documento { get; set; } = new();
}

public sealed class OrdenServicioEdicion
{
    public Guid Id { get; init; }
    public DateTimeOffset FechaRegistro { get; init; }
    public bool AmpliacionAplicada { get; init; }
    public OrdenServicioDocumento? Documento { get; init; }
    public required IReadOnlyList<LineaOrdenServicio> Lineas { get; init; }
}
