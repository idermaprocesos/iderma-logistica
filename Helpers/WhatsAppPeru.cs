using System.Text;

namespace IdermaFichas.Helpers;

public static class WhatsAppPeru
{
    public static Uri? UriWhatsApp(string? numero)
    {
        var internacional = NormalizarCelular(numero);
        return internacional is null ? null : new Uri($"https://wa.me/{internacional}");
    }

    public static Uri? UriDato(string? dato)
    {
        if (string.IsNullOrWhiteSpace(dato))
        {
            return null;
        }

        var texto = dato.Trim();
        if (Uri.TryCreate(texto, UriKind.Absolute, out var absoluta)
            && absoluta.Scheme is "http" or "https")
        {
            return absoluta;
        }

        if (texto.Contains('.') && !texto.Contains(' ')
            && Uri.TryCreate($"https://{texto.TrimStart('/')}", UriKind.Absolute, out var web)
            && web.Scheme is "http" or "https")
        {
            return web;
        }

        return null;
    }

    public static string? NormalizarCelular(string? numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
        {
            return null;
        }

        var digitos = new StringBuilder(numero.Length);
        foreach (var c in numero)
        {
            if (char.IsDigit(c))
            {
                digitos.Append(c);
            }
        }

        var valor = digitos.ToString();
        if (valor.StartsWith("51") && valor.Length is 11 or 12)
        {
            return valor;
        }

        if (valor.StartsWith("0") && valor.Length >= 9)
        {
            valor = valor[1..];
        }

        if (valor.Length == 9 && valor.StartsWith('9'))
        {
            return "51" + valor;
        }

        return null;
    }
}
