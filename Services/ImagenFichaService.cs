using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class ImagenFichaService
{
    private static readonly HashSet<string> Extensiones = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".gif"
    };

    public static string Carpeta { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "imagenes-fichas");

    public static string? RutaEfectiva(FichaTecnica? ficha)
    {
        if (ficha is null)
        {
            return null;
        }

        return BuscarArchivo(ficha.Id, pendientes: false);
    }

    public static string? RutaParaVista(FichaTecnica? ficha)
    {
        if (ficha is null)
        {
            return null;
        }

        return BuscarArchivo(ficha.Id, pendientes: true) ?? BuscarArchivo(ficha.Id, pendientes: false);
    }

    public static async Task<string> GuardarDesdeAsync(Guid fichaId, Stream origen, string extension)
    {
        Directory.CreateDirectory(Carpeta);
        var ext = NormalizarExtension(extension);
        DescartarPendiente(fichaId);
        var destino = Path.Combine(Carpeta, $"{fichaId:N}.pending{ext}");
        await using var archivo = File.Create(destino);
        await origen.CopyToAsync(archivo);
        return destino;
    }

    public static void Confirmar(Guid fichaId, bool conservarImagen)
    {
        if (!conservarImagen)
        {
            EliminarDeFicha(fichaId);
            return;
        }

        var pendiente = BuscarArchivo(fichaId, pendientes: true);
        if (pendiente is null)
        {
            return;
        }

        var destino = Path.Combine(Carpeta, $"{fichaId:N}{Path.GetExtension(pendiente)}");
        foreach (var archivo in Enumerar(fichaId, pendientes: false))
        {
            TryDelete(archivo);
        }

        if (File.Exists(destino))
        {
            TryDelete(destino);
        }

        File.Move(pendiente, destino);
    }

    public static void DescartarPendiente(Guid fichaId)
    {
        foreach (var archivo in Enumerar(fichaId, pendientes: true))
        {
            TryDelete(archivo);
        }
    }

    public static void EliminarDeFicha(Guid fichaId)
    {
        if (!Directory.Exists(Carpeta))
        {
            return;
        }

        foreach (var archivo in Enumerar(fichaId, pendientes: false)
                     .Concat(Enumerar(fichaId, pendientes: true))
                     .ToList())
        {
            TryDelete(archivo);
        }
    }

    private static string? BuscarArchivo(Guid fichaId, bool pendientes)
    {
        return Enumerar(fichaId, pendientes)
            .FirstOrDefault(ruta => Extensiones.Contains(Path.GetExtension(ruta)));
    }

    private static IEnumerable<string> Enumerar(Guid fichaId, bool pendientes)
    {
        if (!Directory.Exists(Carpeta))
        {
            return [];
        }

        return Directory.EnumerateFiles(Carpeta, $"{fichaId:N}*")
            .Where(ruta => EsPendiente(ruta) == pendientes);
    }

    private static bool EsPendiente(string ruta)
    {
        var nombre = Path.GetFileName(ruta);
        return nombre.Contains(".pending.", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string archivo)
    {
        try
        {
            File.Delete(archivo);
        }
        catch (IOException)
        {
        }
    }

    private static string NormalizarExtension(string extension)
    {
        var ext = extension.StartsWith('.')
            ? extension.ToLowerInvariant()
            : "." + extension.ToLowerInvariant();
        return Extensiones.Contains(ext) ? ext : ".png";
    }
}
