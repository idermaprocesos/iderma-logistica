namespace IdermaFichas.Models;

public sealed class PartidaInventario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Lote { get; set; } = string.Empty;
    public DateTimeOffset? FechaCaducidad { get; set; }
    public double Cantidad { get; set; }

    public bool EstaVencido =>
        FechaCaducidad.HasValue && FechaCaducidad.Value.Date < DateTimeOffset.Now.Date;

    public bool EstaProximoAVencer =>
        FechaCaducidad.HasValue
        && !EstaVencido
        && FechaCaducidad.Value.Date <= DateTimeOffset.Now.Date.AddDays(30);

    public int? DiasParaVencer =>
        FechaCaducidad.HasValue
            ? (int)(FechaCaducidad.Value.Date - DateTimeOffset.Now.Date).TotalDays
            : null;

    public string FechaCaducidadTexto =>
        FechaCaducidad?.ToString("dd/MM/yyyy") ?? "Sin caducidad";

    public string LoteTexto =>
        string.IsNullOrWhiteSpace(Lote) ? "Sin lote" : Lote.Trim();

    public string EstadoCaducidadTexto
    {
        get
        {
            if (!FechaCaducidad.HasValue)
            {
                return "Sin fecha de caducidad";
            }

            if (EstaVencido)
            {
                return $"Vencido hace {Math.Abs(DiasParaVencer ?? 0)} día(s)";
            }

            var restantes = DiasParaVencer ?? 0;
            return restantes == 0 ? "Vence hoy" : $"Vence en {restantes} día(s)";
        }
    }

    public PartidaInventario Clonar() => new()
    {
        Id = Id,
        Lote = Lote,
        FechaCaducidad = FechaCaducidad,
        Cantidad = Cantidad
    };
}
