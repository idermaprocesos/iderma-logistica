using System.Text.Json;
using System.Text.Json.Serialization;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

internal static class SolicitudesIncidenciasLocales
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static string Ruta() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IdermaCapilar",
            "solicitudes-incidencias-local.json");

    public static IReadOnlyList<SolicitudIncidencia> Cargar()
    {
        var ruta = Ruta();
        if (!File.Exists(ruta))
        {
            return [];
        }

        try
        {
            var doc = JsonSerializer.Deserialize<Archivo>(File.ReadAllText(ruta), Json);
            return doc?.Solicitudes ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Guardar(IEnumerable<SolicitudIncidencia> solicitudes)
    {
        var ruta = Ruta();
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        var json = JsonSerializer.Serialize(new Archivo { Solicitudes = solicitudes.ToList() }, Json);
        File.WriteAllText(ruta, json);
    }

    private sealed class Archivo
    {
        public List<SolicitudIncidencia> Solicitudes { get; set; } = [];
    }
}
