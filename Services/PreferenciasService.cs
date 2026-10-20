using System.Text.Json;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public sealed class PreferenciasService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "preferencias.json");

    public PreferenciasApp Actual { get; private set; } = new();

    public void Cargar()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        if (!File.Exists(RutaArchivo))
        {
            Guardar();
            return;
        }

        try
        {
            var json = File.ReadAllText(RutaArchivo);
            Actual = JsonSerializer.Deserialize<PreferenciasApp>(json, JsonOptions) ?? new PreferenciasApp();
        }
        catch
        {
            Actual = new PreferenciasApp();
        }

        Actual.Normalizar();
    }

    public void Guardar()
    {
        Actual.Normalizar();
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        ArchivoAtomico.EscribirTexto(RutaArchivo, JsonSerializer.Serialize(Actual, JsonOptions));
    }
}
