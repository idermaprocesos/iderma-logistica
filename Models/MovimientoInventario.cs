namespace IdermaFichas.Models;

public sealed class MovimientoInventario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FichaId { get; set; }
    public Guid? PartidaId { get; set; }
    public string Lote { get; set; } = string.Empty;
    public DateTimeOffset? FechaCaducidad { get; set; }
    public Guid? Area { get; set; }
    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Now;
    public double Cantidad { get; set; }
    public double ExistenciaResultante { get; set; }
    public string Tipo { get; set; } = "Ingreso";
    public string Origen { get; set; } = "Ingreso";
    public string TipoDocumento { get; set; } = string.Empty;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string Nota { get; set; } = string.Empty;
    public string RutaRecibo { get; set; } = string.Empty;
    public string NombreRecibo { get; set; } = string.Empty;

    public bool EsSalida =>
        string.Equals(Tipo, "Salida", StringComparison.OrdinalIgnoreCase);

    public double EfectoExistencia => EsSalida ? -Cantidad : Cantidad;

    public string DocumentoResumen
    {
        get
        {
            var tipo = EsSalida ? "Salida" : (string.IsNullOrWhiteSpace(Tipo) ? Origen : Tipo);
            var lote = string.IsNullOrWhiteSpace(Lote) ? string.Empty : Lote.Trim();
            if (string.IsNullOrWhiteSpace(TipoDocumento) && string.IsNullOrWhiteSpace(NumeroDocumento))
            {
                return string.IsNullOrEmpty(lote) ? tipo : $"{tipo} · lote {lote}";
            }

            var documento = $"{TipoDocumento} {NumeroDocumento}".Trim();
            var baseTexto = string.IsNullOrWhiteSpace(documento) ? tipo : $"{tipo} · {documento}";
            return string.IsNullOrEmpty(lote) ? baseTexto : $"{baseTexto} · lote {lote}";
        }
    }
}
