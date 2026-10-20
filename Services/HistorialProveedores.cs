using System.Text.Json;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class AlmacenHistorialProveedores
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly object Candado = new();
    private static List<ContactoProveedor> _guardados = [];
    private static HashSet<string> _ocultos = new(StringComparer.OrdinalIgnoreCase);

    public static string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "historial-proveedores.json");

    public static IReadOnlyList<ContactoProveedor> Guardados
    {
        get
        {
            lock (Candado)
            {
                return [.. _guardados];
            }
        }
    }

    public static IReadOnlyList<string> Ocultos
    {
        get
        {
            lock (Candado)
            {
                return [.. _ocultos];
            }
        }
    }

    public static void Cargar()
    {
        lock (Candado)
        {
            _guardados = [];
            _ocultos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(RutaArchivo))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(RutaArchivo);
                using var documento = JsonDocument.Parse(json);
                if (documento.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var cargados = JsonSerializer.Deserialize<List<ContactoProveedor>>(json, JsonOptions) ?? [];
                    AsignarGuardados(cargados);
                    return;
                }

                var archivo = JsonSerializer.Deserialize<DocumentoHistorial>(json, JsonOptions);
                if (archivo is null)
                {
                    return;
                }

                AsignarGuardados(archivo.Guardados);
                foreach (var clave in archivo.Ocultos ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(clave))
                    {
                        _ocultos.Add(clave);
                    }
                }
            }
            catch
            {
                ArchivoAtomico.AislarDañado(RutaArchivo);
                _guardados = [];
                _ocultos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public static bool Agregar(ContactoProveedor contacto)
    {
        var limpio = ContactoProveedor.Normalizar(contacto);
        if (limpio.EstaVacio)
        {
            return false;
        }

        lock (Candado)
        {
            _ocultos.Remove(limpio.Clave);
            if (_guardados.Any(c => string.Equals(c.Clave, limpio.Clave, StringComparison.OrdinalIgnoreCase)))
            {
                Persistir();
                return false;
            }

            _guardados.Insert(0, limpio);
            Persistir();
            return true;
        }
    }

    public static void ReemplazarPorNombre(string nombreAnterior, ContactoProveedor contacto)
    {
        var limpio = ContactoProveedor.Normalizar(contacto);
        if (limpio.EstaVacio)
        {
            return;
        }

        lock (Candado)
        {
            QuitarPorNombreInterno(nombreAnterior);
            QuitarPorNombreInterno(limpio.Nombre);
            _ocultos.Remove(limpio.Clave);
            _guardados.Insert(0, limpio);
            Persistir();
        }
    }

    public static void QuitarPorNombre(string nombre)
    {
        lock (Candado)
        {
            if (QuitarPorNombreInterno(nombre) == 0)
            {
                return;
            }

            Persistir();
        }
    }

    private static int QuitarPorNombreInterno(string nombre)
    {
        var limpio = (nombre ?? string.Empty).Trim();
        if (limpio.Length == 0)
        {
            return 0;
        }

        return _guardados.RemoveAll(c =>
            string.Equals(c.Nombre?.Trim(), limpio, StringComparison.OrdinalIgnoreCase));
    }

    public static bool Eliminar(ContactoProveedor contacto)
    {
        var limpio = ContactoProveedor.Normalizar(contacto);
        if (limpio.EstaVacio)
        {
            return false;
        }

        lock (Candado)
        {
            _guardados.RemoveAll(c =>
                string.Equals(c.Clave, limpio.Clave, StringComparison.OrdinalIgnoreCase));
            _ocultos.Add(limpio.Clave);
            Persistir();
            return true;
        }
    }

    private static void AsignarGuardados(IEnumerable<ContactoProveedor>? cargados)
    {
        _guardados =
        [
            .. (cargados ?? [])
                .Select(c => ContactoProveedor.Normalizar(c))
                .Where(c => !c.EstaVacio)
                .GroupBy(c => c.Clave, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
        ];
    }

    private static void Persistir()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        var archivo = new DocumentoHistorial
        {
            Guardados = _guardados,
            Ocultos = [.. _ocultos]
        };
        ArchivoAtomico.EscribirTexto(RutaArchivo, JsonSerializer.Serialize(archivo, JsonOptions));
    }

    private sealed class DocumentoHistorial
    {
        public List<ContactoProveedor> Guardados { get; set; } = [];
        public List<string> Ocultos { get; set; } = [];
    }
}
