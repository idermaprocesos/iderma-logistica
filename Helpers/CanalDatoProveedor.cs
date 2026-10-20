namespace IdermaFichas.Helpers;

public sealed record CanalDatoOpcion(string Id, string Titulo, string Icono, string Placeholder);

public static class CanalDatoProveedor
{
    public const string WhatsApp = "whatsapp";
    public const string Web = "web";
    public const string Facebook = "facebook";
    public const string Instagram = "instagram";

    public static IReadOnlyList<CanalDatoOpcion> Opciones { get; } =
    [
        new(WhatsApp, "WhatsApp", "ms-appx:///Assets/whatsapp.png", "Celular o usuario"),
        new(Web, "Web", "ms-appx:///Assets/web.png", "Web o dato"),
        new(Facebook, "Facebook", "ms-appx:///Assets/facebook.png", "Usuario o enlace"),
        new(Instagram, "Instagram", "ms-appx:///Assets/instagram.png", "Usuario o enlace")
    ];

    public static CanalDatoOpcion De(string? id) =>
        Opciones.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? Opciones.First(o => o.Id == Web);

    public static string Normalizar(string? id) => De(id).Id;

    public static Uri? Uri(string? canal, string? dato, string? celularRespaldo = null)
    {
        var id = Normalizar(canal);
        var texto = (dato ?? string.Empty).Trim();
        return id switch
        {
            WhatsApp => WhatsAppPeru.UriWhatsApp(texto)
                ?? WhatsAppPeru.UriWhatsApp(celularRespaldo)
                ?? WhatsAppPeru.UriDato(texto),
            Facebook => UriRed("facebook.com", "www.facebook.com", texto),
            Instagram => UriRed("instagram.com", "www.instagram.com", texto, barraFinal: true),
            _ => WhatsAppPeru.UriDato(texto)
        };
    }

    private static Uri? UriRed(string dominio, string anfitrion, string texto, bool barraFinal = false)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var absoluta = WhatsAppPeru.UriDato(texto);
        if (absoluta is not null)
        {
            return absoluta;
        }

        var usuario = texto.Trim().TrimStart('@').Trim('/');
        if (usuario.Contains('.') && usuario.Contains(dominio, StringComparison.OrdinalIgnoreCase))
        {
            return WhatsAppPeru.UriDato(usuario);
        }

        if (usuario.Length == 0 || usuario.Contains(' ') || usuario.Contains('/'))
        {
            return null;
        }

        var ruta = barraFinal ? $"{usuario}/" : usuario;
        return new Uri($"https://{anfitrion}/{ruta}");
    }
}
