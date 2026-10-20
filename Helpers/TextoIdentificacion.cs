using System.Globalization;

namespace IdermaFichas.Helpers;

public static class TextoIdentificacion
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    public static string Codigo(string? valor, bool recortar = true)
    {
        var texto = valor ?? string.Empty;
        if (recortar)
        {
            texto = texto.Trim();
        }

        return texto.ToUpperInvariant();
    }

    public static string Nombre(string? valor, bool recortar = true)
    {
        var texto = valor ?? string.Empty;
        if (recortar)
        {
            texto = texto.Trim();
        }

        return texto.ToUpper(Cultura);
    }
}
