using System.Text.Json;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

/// <summary>
/// Guarda las características extra en un archivo aparte para no inflar el catálogo
/// y poder indexarlas sin recargar el producto completo en cada búsqueda.
/// </summary>
public static class AlmacenCaracteristicas
{
    public static string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "fichas-caracteristicas.json");

    public static Dictionary<Guid, List<CaracteristicaProducto>> Cargar()
    {
        if (!File.Exists(RutaArchivo))
        {
            return [];
        }

        var json = File.ReadAllText(RutaArchivo);
        return JsonSerializer.Deserialize<Dictionary<Guid, List<CaracteristicaProducto>>>(json)
               ?? [];
    }

    public static void Guardar(IEnumerable<FichaTecnica> fichas)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        var mapa = fichas
            .Where(f => f.Caracteristicas.Count > 0)
            .ToDictionary(
                f => f.Id,
                f => f.Caracteristicas
                    .Where(c => !string.IsNullOrWhiteSpace(c.Nombre) || !string.IsNullOrWhiteSpace(c.Valor))
                    .Take(CaracteristicaProducto.MaximoPorProducto)
                    .Select(c => c.Clonar())
                    .ToList());

        ArchivoAtomico.EscribirTexto(RutaArchivo, JsonSerializer.Serialize(mapa));
    }

    public static void Aplicar(IEnumerable<FichaTecnica> fichas, Dictionary<Guid, List<CaracteristicaProducto>> mapa)
    {
        foreach (var ficha in fichas)
        {
            if (ficha.Caracteristicas.Count > 0)
            {
                ficha.Caracteristicas = NormalizarLista(ficha.Caracteristicas);
                continue;
            }

            if (mapa.TryGetValue(ficha.Id, out var lista))
            {
                ficha.Caracteristicas = NormalizarLista(lista);
            }
        }
    }

    public static List<CaracteristicaProducto> NormalizarLista(IEnumerable<CaracteristicaProducto>? origen)
    {
        var lista = new List<CaracteristicaProducto>(CaracteristicaProducto.MaximoPorProducto);
        if (origen is null)
        {
            return lista;
        }

        foreach (var item in origen)
        {
            if (string.IsNullOrWhiteSpace(item.Nombre) && string.IsNullOrWhiteSpace(item.Valor))
            {
                continue;
            }

            lista.Add(new CaracteristicaProducto
            {
                Nombre = item.Nombre?.Trim() ?? string.Empty,
                Valor = item.Valor?.Trim() ?? string.Empty
            });

            if (lista.Count >= CaracteristicaProducto.MaximoPorProducto)
            {
                break;
            }
        }

        return lista;
    }

    public static string SerializarLinea(IEnumerable<CaracteristicaProducto> caracteristicas) =>
        string.Join(" | ", caracteristicas
            .Where(c => !string.IsNullOrWhiteSpace(c.Nombre) || !string.IsNullOrWhiteSpace(c.Valor))
            .Select(c => string.IsNullOrWhiteSpace(c.Nombre) ? c.Valor : $"{c.Nombre}: {c.Valor}"));

    public static List<CaracteristicaProducto> ParsearLinea(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return [];
        }

        var partes = texto.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var lista = new List<CaracteristicaProducto>(CaracteristicaProducto.MaximoPorProducto);
        foreach (var parte in partes)
        {
            var sep = parte.IndexOf(':');
            lista.Add(sep < 0
                ? new CaracteristicaProducto { Nombre = parte, Valor = string.Empty }
                : new CaracteristicaProducto
                {
                    Nombre = parte[..sep].Trim(),
                    Valor = parte[(sep + 1)..].Trim()
                });

            if (lista.Count >= CaracteristicaProducto.MaximoPorProducto)
            {
                break;
            }
        }

        return lista;
    }
}
