using System.Globalization;

namespace IdermaFichas.Models;

public static class GlifosWinUi
{
    public static string CodigoXaml(string? glifo)
    {
        var codigo = Codigo(glifo);
        return "&#x" + codigo + ";";
    }

    public static string Caracter(string? glifo)
    {
        var codigo = Codigo(glifo);
        return char.ConvertFromUtf32(int.Parse(codigo, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    public static bool Mismo(string? izquierda, string? derecha) =>
        string.Equals(Codigo(izquierda), Codigo(derecha), StringComparison.OrdinalIgnoreCase);

    public static string Codigo(string? glifo)
    {
        if (string.IsNullOrWhiteSpace(glifo))
        {
            return "E8F1";
        }

        var texto = glifo.Trim();
        if (texto.StartsWith("&#x", StringComparison.OrdinalIgnoreCase) && texto.EndsWith(';'))
        {
            texto = texto[3..^1];
        }
        else if (texto.StartsWith("\\u", StringComparison.OrdinalIgnoreCase) && texto.Length >= 6)
        {
            texto = texto[2..];
        }

        if (texto.Length is >= 4 and <= 6 &&
            texto.All(Uri.IsHexDigit) &&
            int.TryParse(texto, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var punto) &&
            punto is >= 0xE000 and <= 0xF8FF)
        {
            return punto.ToString("X4");
        }

        if (texto.Length == 1)
        {
            return ((int)texto[0]).ToString("X4");
        }

        return "E8F1";
    }
}
