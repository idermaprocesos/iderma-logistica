using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public sealed record ReleaseDisponible(
    Version Version,
    string Etiqueta,
    string UrlNotas,
    string NombreArchivo,
    string UrlDescarga,
    long TamanoBytes);

public static class ActualizacionService
{
    public const string Repositorio = "idermaprocesos/idermalogistica";
    private const string ApiLatest = "https://api.github.com/repos/idermaprocesos/idermalogistica/releases/latest";
    private const string ApiLista = "https://api.github.com/repos/idermaprocesos/idermalogistica/releases?per_page=15";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HttpClient Http = CrearCliente();

    public static Version VersionInstalada
    {
        get
        {
            var version = typeof(App).Assembly.GetName().Version;
            return version is null || version == new Version(0, 0, 0, 0)
                ? new Version(9, 0, 0, 0)
                : version;
        }
    }

    public static string TextoVersionInstalada
    {
        get
        {
            var v = VersionInstalada;
            return $"V{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    public static string AplicarCopiaTrasActualizacion()
    {
        var ruta = RutaPendiente();
        if (!File.Exists(ruta))
        {
            return string.Empty;
        }

        try
        {
            var pendiente = JsonSerializer.Deserialize<ActualizacionPendiente>(File.ReadAllText(ruta), JsonOptions);
            if (pendiente is null || string.IsNullOrWhiteSpace(pendiente.CarpetaCopia))
            {
                File.Delete(ruta);
                return string.Empty;
            }

            if (Directory.Exists(pendiente.CarpetaCopia) || File.Exists(pendiente.CarpetaCopia))
            {
                App.Instance.Copias.Restaurar(pendiente.CarpetaCopia);
            }

            File.Delete(ruta);
            return pendiente.CarpetaCopia;
        }
        catch
        {
            try
            {
                File.Delete(ruta);
            }
            catch
            {
            }

            return string.Empty;
        }
    }

    public static async Task<ReleaseDisponible> ConsultarAsync(CancellationToken cancelar = default)
    {
        var json = await DescargarTextoAsync(ApiLatest, cancelar);
        ReleaseGithub? release = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            release = JsonSerializer.Deserialize<ReleaseGithub>(json, JsonOptions);
        }

        if (release is null || string.IsNullOrWhiteSpace(release.TagName))
        {
            json = await DescargarTextoAsync(ApiLista, cancelar);
            var lista = string.IsNullOrWhiteSpace(json)
                ? []
                : JsonSerializer.Deserialize<List<ReleaseGithub>>(json, JsonOptions) ?? [];
            release = lista.FirstOrDefault(r => !r.Draft && !r.Prerelease)
                      ?? lista.FirstOrDefault();
        }

        if (release is null)
        {
            throw new InvalidOperationException(
                "No hay versiones publicadas todavía en GitHub Releases.");
        }

        return Mapear(release);
    }

    public static async Task<ReleaseDisponible> ConsultarPenultimaAsync(CancellationToken cancelar = default)
    {
        var json = await DescargarTextoAsync(ApiLista, cancelar);
        var lista = string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<ReleaseGithub>>(json, JsonOptions) ?? [];

        var publicadas = new List<ReleaseDisponible>();
        foreach (var item in lista.Where(r => !r.Draft && !r.Prerelease && !string.IsNullOrWhiteSpace(r.TagName)))
        {
            try
            {
                publicadas.Add(Mapear(item));
            }
            catch
            {
            }
        }

        publicadas = publicadas
            .GroupBy(r => Normalizar(r.Version))
            .Select(g => g.First())
            .OrderByDescending(r => r.Version)
            .ToList();

        if (publicadas.Count < 2)
        {
            throw new InvalidOperationException(
                "En GitHub Releases hace falta al menos dos versiones con instalador para restaurar la anterior.");
        }

        return publicadas[1];
    }

    public static bool HayActualizacion(ReleaseDisponible release) =>
        Normalizar(release.Version) > Normalizar(VersionInstalada);

    public static bool MismaVersion(ReleaseDisponible release) =>
        Normalizar(release.Version) == Normalizar(VersionInstalada);

    public static async Task<string> DescargarInstaladorAsync(
        ReleaseDisponible release,
        IProgress<(double Porcentaje, string Mensaje)>? progreso,
        CancellationToken cancelar = default)
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "IdermaCapilar-update");
        Directory.CreateDirectory(carpeta);
        var destino = Path.Combine(carpeta, SanitizarNombre(release.NombreArchivo));

        progreso?.Report((5, "Descargando el instalador desde GitHub…"));
        using var respuesta = await Http.GetAsync(release.UrlDescarga, HttpCompletionOption.ResponseHeadersRead, cancelar);
        respuesta.EnsureSuccessStatusCode();

        var total = respuesta.Content.Headers.ContentLength ?? release.TamanoBytes;
        await using var origen = await respuesta.Content.ReadAsStreamAsync(cancelar);
        await using var archivo = File.Create(destino);
        var buffer = new byte[81_920];
        long leidos = 0;
        int n;
        while ((n = await origen.ReadAsync(buffer, cancelar)) > 0)
        {
            await archivo.WriteAsync(buffer.AsMemory(0, n), cancelar);
            leidos += n;
            if (total > 0)
            {
                var pct = Math.Clamp(10 + 80.0 * leidos / total, 10, 90);
                progreso?.Report((pct, $"Descargando… {leidos / 1_048_576d:0.0} / {total / 1_048_576d:0.0} MB"));
            }
        }

        progreso?.Report((92, "Instalador listo."));
        return destino;
    }

    public static CopiaSeguridadInfo PrepararCopiaYMarcar(string version, string origen = "antes-de-actualizar")
    {
        var copia = App.Instance.Copias.Crear(origen, forzarImagenes: true);
        var pendiente = new ActualizacionPendiente
        {
            CarpetaCopia = copia.Carpeta,
            Version = version,
            FechaUtc = DateTimeOffset.UtcNow
        };
        Directory.CreateDirectory(Path.GetDirectoryName(RutaPendiente())!);
        File.WriteAllText(RutaPendiente(), JsonSerializer.Serialize(pendiente, JsonOptions));
        return copia;
    }

    public static CopiaSeguridadInfo PrepararCopiaYMarcar(ReleaseDisponible release, string origen = "antes-de-actualizar") =>
        PrepararCopiaYMarcar(release.Version.ToString(), origen);

    public static void GuardarInstaladorPendiente(string ruta, ReleaseDisponible release, bool restaurar)
    {
        var pendiente = new InstaladorPendiente
        {
            Ruta = ruta,
            Etiqueta = release.Etiqueta,
            Version = release.Version.ToString(),
            Restaurar = restaurar
        };
        Directory.CreateDirectory(Path.GetDirectoryName(RutaInstaladorPendiente())!);
        File.WriteAllText(RutaInstaladorPendiente(), JsonSerializer.Serialize(pendiente, JsonOptions));
    }

    public static InstaladorPendiente? LeerInstaladorPendiente()
    {
        var meta = RutaInstaladorPendiente();
        if (!File.Exists(meta))
        {
            return null;
        }

        try
        {
            var pendiente = JsonSerializer.Deserialize<InstaladorPendiente>(File.ReadAllText(meta), JsonOptions);
            if (pendiente is null || string.IsNullOrWhiteSpace(pendiente.Ruta) || !File.Exists(pendiente.Ruta))
            {
                File.Delete(meta);
                return null;
            }

            return pendiente;
        }
        catch
        {
            try
            {
                File.Delete(meta);
            }
            catch
            {
            }

            return null;
        }
    }

    public static void LanzarInstaladorYCerrar(string rutaInstalador)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = rutaInstalador,
            UseShellExecute = true
        });
        App.Instance.MainAppWindow?.CerrarParaActualizar();
    }

    public static void LanzarRestauracionYCerrar(string rutaInstalador)
    {
        var script = Path.Combine(Path.GetTempPath(), "IdermaCapilar-restaurar.ps1");
        File.WriteAllText(script, ScriptRestauracion());
        try
        {
            var iniciado = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Instalador \"{rutaInstalador}\"",
                UseShellExecute = true,
                Verb = "runas"
            });
            if (iniciado is null)
            {
                throw new InvalidOperationException("No se pudo iniciar la restauración.");
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException("Se canceló el permiso de administrador. La versión actual no se modificó.");
        }

        App.Instance.MainAppWindow?.CerrarParaActualizar();
    }

    private static async Task<string?> DescargarTextoAsync(string url, CancellationToken cancelar)
    {
        using var respuesta = await Http.GetAsync(url, cancelar);
        if (respuesta.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if ((int)respuesta.StatusCode == 403 || (int)respuesta.StatusCode == 429)
        {
            throw new InvalidOperationException(
                "GitHub limitó las consultas. Espere un minuto e inténtelo de nuevo.");
        }

        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadAsStringAsync(cancelar);
    }

    private static ReleaseDisponible Mapear(ReleaseGithub release)
    {
        var version = ParsearVersion(release.TagName);
        var asset = ElegirInstalador(release.Assets);
        if (asset is null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
        {
            throw new InvalidOperationException(
                $"La versión {release.TagName} no incluye un instalador (.exe) en GitHub Releases.");
        }

        return new ReleaseDisponible(
            version,
            release.TagName,
            release.HtmlUrl ?? $"https://github.com/{Repositorio}/releases",
            asset.Name ?? "IdermaCapilarApp.exe",
            asset.BrowserDownloadUrl,
            asset.Size);
    }

    private static string ScriptRestauracion() =>
        """
        param([Parameter(Mandatory=$true)][string]$Instalador)
        $ErrorActionPreference = 'Continue'
        Start-Sleep -Seconds 3
        Get-Process IdermaCapilarApp -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 1
        $claves = @(
            'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
            'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
        )
        $apps = Get-ItemProperty $claves -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -like 'Iderma Capilar App*' }
        foreach ($app in $apps) {
            $cmd = $app.QuietUninstallString
            if ([string]::IsNullOrWhiteSpace($cmd)) { $cmd = $app.UninstallString }
            if ([string]::IsNullOrWhiteSpace($cmd)) { continue }
            if ($cmd -match '^"([^"]+)"(.*)$' -and $Matches[1] -like '*.exe') {
                Start-Process -FilePath $Matches[1] -ArgumentList '/uninstall','/quiet','/norestart' -Wait -WindowStyle Hidden
            }
            elseif ($cmd -match 'MsiExec\.exe|msiexec') {
                if ($app.PSChildName -match '^\{[0-9A-Fa-f-]+\}$') {
                    Start-Process msiexec.exe -ArgumentList '/x', $app.PSChildName, '/qn', '/norestart' -Wait -WindowStyle Hidden
                }
                else {
                    cmd.exe /c $cmd
                }
            }
            else {
                cmd.exe /c $cmd
            }
        }
        Start-Process -FilePath $Instalador -Wait
        """;

    private static AssetGithub? ElegirInstalador(IReadOnlyList<AssetGithub>? assets)
    {
        if (assets is null || assets.Count == 0)
        {
            return null;
        }

        return assets.FirstOrDefault(a => Nombre(a).Contains("IdermaCapilarApp-V", StringComparison.OrdinalIgnoreCase)
                                          && Nombre(a).EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
               ?? assets.FirstOrDefault(a => Nombre(a).Contains("instalador", StringComparison.OrdinalIgnoreCase)
                                             && Nombre(a).EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
               ?? assets.FirstOrDefault(a => Nombre(a).EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                                             && !Nombre(a).Contains("redist", StringComparison.OrdinalIgnoreCase)
                                             && !Nombre(a).Contains("webview", StringComparison.OrdinalIgnoreCase));
    }

    private static string Nombre(AssetGithub asset) => asset.Name ?? string.Empty;

    private static Version ParsearVersion(string etiqueta)
    {
        var limpio = etiqueta.Trim();
        if (limpio.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            limpio = limpio[1..].Trim().TrimStart('.').Trim();
        }

        var coincidencia = System.Text.RegularExpressions.Regex.Match(limpio, @"\d+(?:\.\d+){1,3}");
        if (!coincidencia.Success || !Version.TryParse(coincidencia.Value, out var version))
        {
            throw new InvalidOperationException($"No se reconoció la versión «{etiqueta}».");
        }

        return Normalizar(version);
    }

    private static Version Normalizar(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

    private static string SanitizarNombre(string nombre)
    {
        var invalido = Path.GetInvalidFileNameChars();
        var limpio = new string(nombre.Where(c => !invalido.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(limpio) ? "IdermaCapilarApp-update.exe" : limpio;
    }

    private static string RutaInstaladorPendiente() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IdermaCapilar",
            "instalador-pendiente.json");

    private static string RutaPendiente() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IdermaCapilar",
            "actualizacion-pendiente.json");

    private static HttpClient CrearCliente()
    {
        var cliente = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(15)
        };
        cliente.DefaultRequestHeaders.UserAgent.ParseAdd("IdermaCapilarApp/9.0.0");
        cliente.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        cliente.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return cliente;
    }

    public sealed class InstaladorPendiente
    {
        public string Ruta { get; set; } = string.Empty;
        public string Etiqueta { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public bool Restaurar { get; set; }
    }

    private sealed class ActualizacionPendiente
    {
        public string CarpetaCopia { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTimeOffset FechaUtc { get; set; }
    }

    private sealed class ReleaseGithub
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("assets")]
        public List<AssetGithub> Assets { get; set; } = [];
    }

    private sealed class AssetGithub
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}
