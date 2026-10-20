using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class ReciboMovimientoService
{
    private static readonly HashSet<string> ExtensionesImagen = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".gif"
    };

    public static string Carpeta { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "recibos-movimientos");

    public static bool EsImagen(string? ruta) =>
        !string.IsNullOrWhiteSpace(ruta) && ExtensionesImagen.Contains(Path.GetExtension(ruta));

    public static string Guardar(Guid movimientoId, string origen, string extension)
    {
        Directory.CreateDirectory(Carpeta);
        var ext = Normalizar(extension);
        Eliminar(movimientoId);
        var destino = Path.Combine(Carpeta, $"{movimientoId:N}{ext}");
        File.Copy(origen, destino, overwrite: true);
        return destino;
    }

    public static string? RutaEfectiva(MovimientoInventario movimiento)
    {
        if (!string.IsNullOrWhiteSpace(movimiento.RutaRecibo) && File.Exists(movimiento.RutaRecibo))
        {
            return movimiento.RutaRecibo;
        }

        if (!Directory.Exists(Carpeta))
        {
            return null;
        }

        return Directory.EnumerateFiles(Carpeta, $"{movimiento.Id:N}.*")
            .FirstOrDefault();
    }

    public static void Eliminar(Guid movimientoId)
    {
        if (!Directory.Exists(Carpeta))
        {
            return;
        }

        foreach (var archivo in Directory.EnumerateFiles(Carpeta, $"{movimientoId:N}.*").ToList())
        {
            try
            {
                File.Delete(archivo);
            }
            catch
            {
            }
        }
    }

    public static void EliminarVarios(IEnumerable<Guid> ids)
    {
        foreach (var id in ids)
        {
            Eliminar(id);
        }
    }

    public static string NombreExportacion(FichaTecnica ficha, MovimientoInventario movimiento)
    {
        var fecha = movimiento.Fecha.ToLocalTime().ToString("yyyyMMdd-HHmm");
        var tipo = movimiento.EsSalida ? "salida" : "ingreso";
        var doc = string.IsNullOrWhiteSpace(movimiento.NumeroDocumento)
            ? movimiento.Id.ToString("N")[..8]
            : movimiento.NumeroDocumento;
        var ext = Path.GetExtension(RutaEfectiva(movimiento) ?? movimiento.NombreRecibo);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = Path.GetExtension(movimiento.RutaRecibo);
        }

        var nombre = $"{fecha}_{Sanitizar(ficha.Codigo)}_{tipo}_{Sanitizar(movimiento.TipoDocumento)}_{Sanitizar(doc)}{ext}";
        return nombre;
    }

    private static string Normalizar(string extension)
    {
        var ext = extension.StartsWith('.') ? extension : "." + extension;
        return ext.ToLowerInvariant();
    }

    private static string Sanitizar(string valor)
    {
        var limpio = new string((valor ?? string.Empty)
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(limpio) ? "recibo" : limpio;
    }
}
