using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace IdermaFichas.Models;

public enum EstadoSolicitudClinica
{
    Pendiente,
    Aceptada,
    Denegada
}

public sealed class LineaSolicitudClinica
{
    public string Codigo { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }

    /// <summary>Cantidad que logística aceptó o denegó. Null mientras sigue pendiente.</summary>
    public int? CantidadRespuesta { get; set; }

    [JsonIgnore]
    public string Texto => $"{Codigo}  {Nombre}  x{Cantidad}".Trim();
}

public sealed class SolicitudClinica : INotifyPropertyChanged
{
    private EstadoSolicitudClinica _estado = EstadoSolicitudClinica.Pendiente;
    private string _motivoDenegacion = string.Empty;
    private DateTimeOffset? _fechaRespuesta;
    private bool _cantidadAcordada;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Folio { get; set; } = string.Empty;
    public string Enfermera { get; set; } = string.Empty;
    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Now;
    public bool Urgente { get; set; }
    public List<LineaSolicitudClinica> Items { get; set; } = [];
    public string Origen { get; set; } = "clinica";

    public EstadoSolicitudClinica Estado
    {
        get => _estado;
        set
        {
            if (_estado == value)
            {
                return;
            }

            _estado = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EstadoTexto));
            OnPropertyChanged(nameof(EstaPendiente));
            OnPropertyChanged(nameof(EstaAceptada));
            OnPropertyChanged(nameof(EstaDenegada));
            OnPropertyChanged(nameof(EstaRespondida));
            OnPropertyChanged(nameof(ResumenRespuesta));
            OnPropertyChanged(nameof(Detalle));
        }
    }

    public bool CantidadAcordada
    {
        get => _cantidadAcordada;
        set
        {
            if (_cantidadAcordada == value)
            {
                return;
            }

            _cantidadAcordada = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EstadoTexto));
            OnPropertyChanged(nameof(ResumenRespuesta));
            OnPropertyChanged(nameof(Detalle));
        }
    }

    public string MotivoDenegacion
    {
        get => _motivoDenegacion;
        set
        {
            if (_motivoDenegacion == value)
            {
                return;
            }

            _motivoDenegacion = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ResumenRespuesta));
        }
    }

    public DateTimeOffset? FechaRespuesta
    {
        get => _fechaRespuesta;
        set
        {
            if (_fechaRespuesta == value)
            {
                return;
            }

            _fechaRespuesta = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FechaRespuestaTexto));
            OnPropertyChanged(nameof(ResumenRespuesta));
        }
    }

    [JsonIgnore]
    public string FechaTexto => Fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    [JsonIgnore]
    public string FechaRespuestaTexto => FechaRespuesta?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "—";

    [JsonIgnore]
    public string EstadoTexto => Estado switch
    {
        EstadoSolicitudClinica.Aceptada when CantidadAcordada => "Cantidad acordada",
        EstadoSolicitudClinica.Aceptada => "Aceptada",
        EstadoSolicitudClinica.Denegada => "Denegada",
        _ => "Pendiente"
    };

    [JsonIgnore]
    public bool EstaPendiente => Estado == EstadoSolicitudClinica.Pendiente;

    [JsonIgnore]
    public bool EstaAceptada => Estado == EstadoSolicitudClinica.Aceptada;

    [JsonIgnore]
    public bool EstaDenegada => Estado == EstadoSolicitudClinica.Denegada;

    [JsonIgnore]
    public bool EstaRespondida => Estado != EstadoSolicitudClinica.Pendiente;

    [JsonIgnore]
    public int TotalCantidad => Items.Sum(i => i.Cantidad);

    [JsonIgnore]
    public string Resumen =>
        $"{Enfermera} · {Items.Count} {(Items.Count == 1 ? "producto" : "productos")} · {TotalCantidad} pzas";

    [JsonIgnore]
    public string Detalle => string.Join(Environment.NewLine, Items.Select(TextoLinea));

    [JsonIgnore]
    public string ResumenRespuesta
    {
        get
        {
            if (Estado == EstadoSolicitudClinica.Pendiente)
            {
                return "Esperando respuesta de logística.";
            }

            if (Estado == EstadoSolicitudClinica.Aceptada && CantidadAcordada)
            {
                return string.IsNullOrWhiteSpace(MotivoDenegacion)
                    ? $"Aceptada con cantidad acordada el {FechaRespuestaTexto}."
                    : $"Aceptada con cantidad acordada el {FechaRespuestaTexto}: {MotivoDenegacion}";
            }

            if (Estado == EstadoSolicitudClinica.Aceptada)
            {
                return $"Aceptada el {FechaRespuestaTexto}.";
            }

            return string.IsNullOrWhiteSpace(MotivoDenegacion)
                ? $"Denegada el {FechaRespuestaTexto}."
                : $"Denegada el {FechaRespuestaTexto}: {MotivoDenegacion}";
        }
    }

    private string TextoLinea(LineaSolicitudClinica linea)
    {
        var baseTexto = $"{linea.Codigo}  {linea.Nombre}".Trim();
        if (linea.CantidadRespuesta is not int respondida)
        {
            return linea.Texto;
        }

        if (Estado == EstadoSolicitudClinica.Denegada)
        {
            return $"{baseTexto}  pedidas x{linea.Cantidad} · denegadas x{respondida}".Trim();
        }

        if (CantidadAcordada)
        {
            return $"{baseTexto}  pedidas x{linea.Cantidad} · acordadas x{respondida}".Trim();
        }

        return linea.Texto;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
