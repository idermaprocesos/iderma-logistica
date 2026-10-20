using System.Globalization;

namespace IdermaFichas.Models;

public sealed class AvisoAutorizacion
{
    public string Id { get; set; } = string.Empty;
    public string Folio { get; set; } = string.Empty;
    public string Solicitante { get; set; } = string.Empty;
    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Now;
    public string EstadoTexto { get; set; } = "Pendiente";
    public string Resumen { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public string Respuesta { get; set; } = string.Empty;
    public bool EstaPendiente { get; set; }
    public bool EstaRespondida { get; set; }

    public string FechaTexto => Fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
}
