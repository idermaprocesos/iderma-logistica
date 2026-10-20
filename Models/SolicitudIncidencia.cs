using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace IdermaFichas.Models;

public enum TipoIncidencia
{
    EquipoElectronico,
    Equipo,
    Insumo,
    Recurso
}

public enum EstadoIncidencia
{
    Pendiente,
    Aceptada,
    Denegada
}

public sealed class SolicitudIncidencia : INotifyPropertyChanged
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private EstadoIncidencia _estado = EstadoIncidencia.Pendiente;
    private string _motivo = string.Empty;
    private DateTimeOffset? _fechaRespuesta;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Folio { get; set; } = string.Empty;
    public string Solicitante { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public TipoIncidencia Tipo { get; set; } = TipoIncidencia.Insumo;
    public string Nombre { get; set; } = string.Empty;
    public string Enlace { get; set; } = string.Empty;
    public string DetalleTexto { get; set; } = string.Empty;
    public string Unidad { get; set; } = "und";
    public double Cantidad { get; set; } = 1;
    public double PrecioReposicion { get; set; }
    public bool Urgente { get; set; }

    private string _ordenId = string.Empty;
    private string _estadoOrden = string.Empty;

    public string OrdenId
    {
        get => _ordenId;
        set
        {
            _ordenId = value ?? string.Empty;
            Avisar();
            Avisar(nameof(EstaPendiente));
            Avisar(nameof(PuedeAgregarOrden));
        }
    }

    public string EstadoOrden
    {
        get => _estadoOrden;
        set
        {
            _estadoOrden = value ?? string.Empty;
            Avisar();
            Avisar(nameof(EstaPendiente));
            Avisar(nameof(PuedeAgregarOrden));
            Avisar(nameof(ResumenRespuesta));
        }
    }
    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Now;

    public EstadoIncidencia Estado
    {
        get => _estado;
        set
        {
            if (_estado == value)
            {
                return;
            }

            _estado = value;
            Avisar(nameof(Estado));
            Avisar(nameof(EstadoTexto));
            Avisar(nameof(EstaPendiente));
            Avisar(nameof(EstaDenegada));
            Avisar(nameof(PuedeAgregarOrden));
            Avisar(nameof(ResumenRespuesta));
        }
    }

    public string Motivo
    {
        get => _motivo;
        set
        {
            _motivo = value ?? string.Empty;
            Avisar();
            Avisar(nameof(ResumenRespuesta));
        }
    }

    public DateTimeOffset? FechaRespuesta
    {
        get => _fechaRespuesta;
        set
        {
            _fechaRespuesta = value;
            Avisar();
            Avisar(nameof(ResumenRespuesta));
        }
    }

    [JsonIgnore]
    public string FechaTexto => Fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    [JsonIgnore]
    public string TipoTexto => Tipo switch
    {
        TipoIncidencia.EquipoElectronico => "Equipo electrónico",
        TipoIncidencia.Equipo => "Equipo",
        TipoIncidencia.Recurso => "Recurso",
        _ => "Insumo"
    };

    [JsonIgnore]
    public string EstadoTexto => Estado switch
    {
        EstadoIncidencia.Aceptada => "Aceptada",
        EstadoIncidencia.Denegada => "Denegada",
        _ => "Pendiente"
    };

    [JsonIgnore]
    public bool EstaPendiente =>
        Estado == EstadoIncidencia.Pendiente && string.IsNullOrWhiteSpace(OrdenId);

    [JsonIgnore]
    public bool EstaDenegada => Estado == EstadoIncidencia.Denegada;

    [JsonIgnore]
    public bool PuedeAgregarOrden => EstaPendiente;

    [JsonIgnore]
    public string Resumen => $"{Solicitante} · {Area} · {TipoTexto}";

    [JsonIgnore]
    public string Detalle
    {
        get
        {
            var cantidad = Cantidad.ToString("0.##", Cultura);
            var precio = PrecioReposicion > 0
                ? " · reposición " + PrecioReposicion.ToString("C", Cultura)
                : string.Empty;
            var nota = string.IsNullOrWhiteSpace(DetalleTexto) ? string.Empty : " · " + DetalleTexto.Trim();
            var enlace = string.IsNullOrWhiteSpace(Enlace) ? string.Empty : " · " + Enlace.Trim();
            return $"{Nombre} · {cantidad} {Unidad}{precio}{enlace}{nota}";
        }
    }

    [JsonIgnore]
    public string ResumenRespuesta => Estado switch
    {
        EstadoIncidencia.Denegada => string.IsNullOrWhiteSpace(Motivo) ? "Denegada" : "Denegada · " + Motivo.Trim(),
        EstadoIncidencia.Aceptada => "Aceptada" + (FechaRespuesta is null ? string.Empty : " · " + FechaRespuesta.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")),
        _ => string.Empty
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Avisar([CallerMemberName] string? nombre = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
}

public sealed class OrdenCompraEdicion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset? FechaRegistro { get; init; }
    public OrdenCompraDocumento? Documento { get; init; }
    public required IReadOnlyList<LineaOrdenCompra> Lineas { get; init; }
}
