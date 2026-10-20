using System.Text.Json;
using System.Text.Json.Serialization;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

internal static class SolicitudesLocales
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
            "solicitudes-clinica-local.json");

    public static IReadOnlyList<SolicitudClinica> Cargar()
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

    public static void Guardar(IEnumerable<SolicitudClinica> solicitudes)
    {
        var ruta = Ruta();
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        var doc = new Archivo
        {
            Guardado = DateTimeOffset.Now,
            Solicitudes = solicitudes
                .Select(Clonar)
                .OrderByDescending(s => s.EstaPendiente)
                .ThenByDescending(s => s.Fecha)
                .ToList()
        };
        var temporal = ruta + $".{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporal, JsonSerializer.Serialize(doc, Json));
        File.Copy(temporal, ruta, overwrite: true);
        File.Delete(temporal);
    }

    private static SolicitudClinica Clonar(SolicitudClinica s) => new()
    {
        Id = s.Id,
        Folio = s.Folio,
        Enfermera = s.Enfermera,
        Fecha = s.Fecha,
        Urgente = s.Urgente,
        Origen = s.Origen,
        Estado = s.Estado,
        CantidadAcordada = s.CantidadAcordada,
        MotivoDenegacion = s.MotivoDenegacion,
        FechaRespuesta = s.FechaRespuesta,
        Items = s.Items.Select(i => new LineaSolicitudClinica
        {
            Codigo = i.Codigo,
            Area = i.Area,
            Nombre = i.Nombre,
            Cantidad = i.Cantidad,
            CantidadRespuesta = i.CantidadRespuesta
        }).ToList()
    };

    private sealed class Archivo
    {
        public DateTimeOffset Guardado { get; set; }
        public List<SolicitudClinica> Solicitudes { get; set; } = [];
    }
}
