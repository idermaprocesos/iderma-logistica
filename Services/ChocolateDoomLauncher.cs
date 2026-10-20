using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;

namespace IdermaFichas.Services;

/// <summary>
/// Descarga el binario de Windows publicado en chocolate-doom/chocolate-doom
/// y lo abre. El motor no incluye los datos comerciales de Doom; Freedoom
/// aporta un IWAD libre para que el juego arranque.
/// </summary>
public static class ChocolateDoomLauncher
{
    private const string UrlMotor =
        "https://github.com/chocolate-doom/chocolate-doom/releases/download/chocolate-doom-3.1.1/chocolate-doom-3.1.1-win64.zip";

    private const string UrlDatos =
        "https://github.com/freedoom/freedoom/releases/download/v0.13.0/freedoom-0.13.0.zip";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    static ChocolateDoomLauncher()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("IdermaCapilarApp");
    }

    public static bool EstaListo => File.Exists(RutaExe) && File.Exists(RutaWad);

    private static string Carpeta =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IdermaCapilar",
            "chocolate-doom");

    private static string RutaExe => Path.Combine(Carpeta, "chocolate-doom.exe");

    private static string RutaWad => Path.Combine(Carpeta, "freedoom2.wad");

    public static async Task PrepararAsync(IProgress<string>? progreso, CancellationToken cancelacion)
    {
        Directory.CreateDirectory(Carpeta);
        if (!File.Exists(RutaExe))
        {
            var zip = Path.Combine(Carpeta, "chocolate-doom.zip");
            await DescargarAsync(UrlMotor, zip, progreso, "Descargando Chocolate Doom", cancelacion);
            progreso?.Report("Instalando Chocolate Doom…");
            ExtraerMotor(zip, Carpeta);
            File.Delete(zip);
        }

        if (!File.Exists(RutaWad))
        {
            var zip = Path.Combine(Carpeta, "freedoom.zip");
            await DescargarAsync(UrlDatos, zip, progreso, "Descargando Freedoom", cancelacion);
            progreso?.Report("Instalando los datos del juego…");
            ExtraerWad(zip, RutaWad);
            File.Delete(zip);
        }

        if (!EstaListo)
        {
            throw new InvalidOperationException("No se pudo preparar Chocolate Doom.");
        }
    }

    public static void Abrir()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = RutaExe,
            WorkingDirectory = Carpeta,
            Arguments = $"-iwad \"{RutaWad}\"",
            UseShellExecute = false
        });
    }

    private static async Task DescargarAsync(
        string url,
        string destino,
        IProgress<string>? progreso,
        string mensaje,
        CancellationToken cancelacion)
    {
        var temporal = destino + ".parcial";
        using var respuesta = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancelacion);
        respuesta.EnsureSuccessStatusCode();
        var total = respuesta.Content.Headers.ContentLength ?? 0;
        await using (var origen = await respuesta.Content.ReadAsStreamAsync(cancelacion))
        await using (var archivo = File.Create(temporal))
        {
            var buffer = new byte[81_920];
            long leido = 0;
            int n;
            while ((n = await origen.ReadAsync(buffer, cancelacion)) > 0)
            {
                await archivo.WriteAsync(buffer.AsMemory(0, n), cancelacion);
                leido += n;
                progreso?.Report(total > 0
                    ? $"{mensaje}… {leido * 100 / total} %"
                    : $"{mensaje}…");
            }
        }

        File.Move(temporal, destino, overwrite: true);
    }

    private static void ExtraerMotor(string zip, string carpeta)
    {
        using var archivo = ZipFile.OpenRead(zip);
        var exe = archivo.Entries.FirstOrDefault(e =>
            e.Name.Equals("chocolate-doom.exe", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("El paquete no contiene chocolate-doom.exe.");

        var prefijo = exe.FullName[..^exe.Name.Length];
        var raiz = Path.GetFullPath(carpeta) + Path.DirectorySeparatorChar;
        foreach (var entrada in archivo.Entries)
        {
            if (string.IsNullOrEmpty(entrada.Name)
                || !entrada.FullName.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relativo = entrada.FullName[prefijo.Length..].Replace('/', Path.DirectorySeparatorChar);
            var destino = Path.GetFullPath(Path.Combine(carpeta, relativo));
            if (!destino.StartsWith(raiz, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
            entrada.ExtractToFile(destino, overwrite: true);
        }
    }

    private static void ExtraerWad(string zip, string destino)
    {
        using var archivo = ZipFile.OpenRead(zip);
        var wad = archivo.Entries.FirstOrDefault(e =>
            e.Name.Equals("freedoom2.wad", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("El paquete no contiene freedoom2.wad.");

        wad.ExtractToFile(destino, overwrite: true);
    }
}
