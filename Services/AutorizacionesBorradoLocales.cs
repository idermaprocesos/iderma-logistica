using System.Text.Json;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

internal static class AutorizacionesBorradoLocales
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string Ruta() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IdermaCapilar",
            "autorizaciones-borrado-local.json");

    public static IReadOnlyList<AvisoAutorizacion> Cargar()
    {
        var ruta = Ruta();
        if (!File.Exists(ruta))
        {
            return [];
        }

        try
        {
            var doc = JsonSerializer.Deserialize<Archivo>(File.ReadAllText(ruta), Json);
            return doc?.Avisos ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Guardar(IEnumerable<AvisoAutorizacion> avisos)
    {
        var ruta = Ruta();
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        var doc = new Archivo
        {
            Guardado = DateTimeOffset.Now,
            Avisos = avisos
                .Select(Clonar)
                .OrderByDescending(a => a.EstaPendiente)
                .ThenByDescending(a => a.Fecha)
                .ToList()
        };
        var temporal = ruta + $".{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporal, JsonSerializer.Serialize(doc, Json));
        File.Copy(temporal, ruta, overwrite: true);
        File.Delete(temporal);
    }

    private static AvisoAutorizacion Clonar(AvisoAutorizacion aviso) => new()
    {
        Id = aviso.Id,
        Folio = aviso.Folio,
        Solicitante = aviso.Solicitante,
        Fecha = aviso.Fecha,
        EstadoTexto = aviso.EstadoTexto,
        Resumen = aviso.Resumen,
        Detalle = aviso.Detalle,
        Respuesta = aviso.Respuesta,
        EstaPendiente = aviso.EstaPendiente,
        EstaRespondida = aviso.EstaRespondida
    };

    private sealed class Archivo
    {
        public DateTimeOffset Guardado { get; set; }
        public List<AvisoAutorizacion> Avisos { get; set; } = [];
    }
}
