using System.Diagnostics;
using Iderma.Core.Catalog;
using Iderma.Core.Services;

namespace IdermaFichas.Services;

internal static class AbrirAppMantenimiento
{
    public static string Abrir(string codigoEquipo)
    {
        var permiso = MotivoSinPermiso();
        if (permiso is not null)
        {
            return permiso;
        }

        var argumento = "equipo " + codigoEquipo.Trim();
        var exe = BuscarExe();
        if (exe is not null)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = argumento,
                WorkingDirectory = Path.GetDirectoryName(exe)!,
                UseShellExecute = true
            });
            return string.Empty;
        }

        var proyecto = BuscarProyecto();
        if (proyecto is null)
        {
            return "No se encontró la app de Mantenimiento junto a logística.";
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{proyecto}\" -- {argumento}",
            WorkingDirectory = Path.GetDirectoryName(proyecto)!,
            UseShellExecute = true
        });
        return string.Empty;
    }

    private static string? MotivoSinPermiso()
    {
        using var sesiones = new SessionStore();
        var sesion = sesiones.Snapshot();
        if (string.IsNullOrWhiteSpace(sesion.UserId))
        {
            return "Inicia sesión en el launcher para abrir Mantenimiento.";
        }

        using var usuarios = new UserStore();
        var usuario = usuarios.FindById(sesion.UserId);
        if (usuario is null)
        {
            return "No se encontró la cuenta de esta sesión en Cuentas.";
        }

        if (!usuario.IsActive || !usuario.AppIds.Contains(AppCatalog.Mantenimiento, StringComparer.OrdinalIgnoreCase))
        {
            return "Esta cuenta no tiene permiso para la app de Mantenimiento. Se asigna en Cuentas.";
        }

        return null;
    }

    private static string? BuscarExe()
    {
        foreach (var raiz in Raices())
        {
            var bin = Path.Combine(raiz, "bin");
            if (!Directory.Exists(bin))
            {
                continue;
            }

            var candidatos = Directory.EnumerateFiles(bin, "MantenimientoCapilar.exe", SearchOption.AllDirectories)
                .OrderByDescending(path => path.Contains($"{Path.DirectorySeparatorChar}Debug{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(File.GetLastWriteTimeUtc)
                .ToList();
            if (candidatos.Count > 0)
            {
                return candidatos[0];
            }
        }

        return null;
    }

    private static string? BuscarProyecto()
    {
        foreach (var raiz in Raices())
        {
            var proyecto = Path.Combine(raiz, "MantenimientoCapilar.csproj");
            if (File.Exists(proyecto))
            {
                return proyecto;
            }
        }

        return null;
    }

    private static IEnumerable<string> Raices()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var area = Path.Combine(dir.FullName, "areas", "mantenimiento");
            if (Directory.Exists(area))
            {
                yield return area;
                yield break;
            }

            dir = dir.Parent;
        }
    }
}
