namespace IdermaFichas.Models;

public sealed class ManifiestoCopia
{
    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Now;

    public string Origen { get; set; } = "manual";

    public int Fichas { get; set; }

    public int Areas { get; set; }

    public int Movimientos { get; set; }

    public int Recibos { get; set; }

    public bool IncluyeImagenes { get; set; }

    public bool IncluyeRecibos { get; set; }
}

public sealed class CopiaSeguridadInfo
{
    public required string Carpeta { get; init; }

    public required DateTimeOffset Fecha { get; init; }

    public required string Origen { get; init; }

    public int Fichas { get; init; }

    public int Areas { get; init; }

    public int Movimientos { get; init; }

    public int Recibos { get; init; }

    public bool IncluyeImagenes { get; init; }

    public bool IncluyeRecibos { get; init; }

    public string Titulo => Fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    public string Detalle
    {
        get
        {
            var partes = new List<string>
            {
                TextoOrigen,
                $"{Fichas} ficha(s)",
                $"{Areas} área(s)"
            };
            if (Movimientos > 0)
            {
                partes.Add($"{Movimientos} movimiento(s)");
            }

            if (IncluyeImagenes)
            {
                partes.Add("con imágenes");
            }

            if (IncluyeRecibos)
            {
                partes.Add(Recibos > 0 ? $"{Recibos} recibo(s)" : "con recibos");
            }

            return string.Join(" · ", partes);
        }
    }

    private string TextoOrigen => Origen switch
    {
        "automatica" => "Automática",
        "antes-de-borrar" => "Antes de borrar",
        "antes-de-restaurar" => "Antes de restaurar",
        _ => "Manual"
    };
}
