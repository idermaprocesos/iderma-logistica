namespace IdermaFichas.Models;

public sealed class PreferenciasApp
{
    public static readonly int[] IntervalosMinutos = [5, 10, 20, 30];

    public static readonly int[] IntervalosCopiasHoras = [1, 6, 12, 24];

    public static readonly int[] MaximosCopias = [5, 10, 20, 40];

    public bool AutoguardadoActivo { get; set; } = true;

    public int IntervaloAutoguardadoMinutos { get; set; } = 10;

    public bool CopiasActivas { get; set; } = true;

    public int IntervaloCopiasHoras { get; set; } = 6;

    public int MaximoCopias { get; set; } = 20;

    public bool CopiasIncluyenImagenes { get; set; } = true;

    public DateTimeOffset? UltimaCopiaUtc { get; set; }

    public void Normalizar()
    {
        if (!IntervalosMinutos.Contains(IntervaloAutoguardadoMinutos))
        {
            IntervaloAutoguardadoMinutos = 10;
        }

        if (!IntervalosCopiasHoras.Contains(IntervaloCopiasHoras))
        {
            IntervaloCopiasHoras = 6;
        }

        if (!MaximosCopias.Contains(MaximoCopias))
        {
            MaximoCopias = 20;
        }
    }
}
