using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdermaFichas.Models;

public static class AreasOperativas
{
    public static readonly Guid IdClinica = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static readonly Guid IdOficina = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public static readonly Guid IdLimpieza = Guid.Parse("33333333-3333-4333-8333-333333333333");
    public static readonly Guid IdInsumosMedicos = IdClinica;
    public static readonly Guid IdProductosVenta = Guid.Parse("44444444-4444-4444-8444-444444444444");
    public static readonly Guid IdLaboratorio = Guid.Parse("55555555-5555-4555-8555-555555555555");

    public const string EtiquetaSinArea = "area:none";
    public const string PrefijoEtiqueta = "area:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly object Candado = new();
    private static List<AreaDefinicion> _areas = CrearPredeterminadas();

    public static bool ArchivoExistiaAlAbrir { get; private set; }

    public static event Action? Cambiaron;

    public static event Action? PublicacionPedida;

    public static string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "areas-operativas.json");

    public static IReadOnlyList<AreaDefinicion> Todas
    {
        get
        {
            List<AreaDefinicion> copia;
            lock (Candado)
            {
                copia = [.. _areas];
            }

            return copia.OrderBy(a => a.Orden).ThenBy(a => a.Nombre).ToList();
        }
    }

    public static void Cargar()
    {
        ArchivoExistiaAlAbrir = File.Exists(RutaArchivo);
        if (!ArchivoExistiaAlAbrir)
        {
            lock (Candado)
            {
                _areas = CrearPredeterminadas();
            }

            EscribirArchivo();
            NormalizarGlifos();
            AsegurarListas();
            return;
        }

        var json = File.ReadAllText(RutaArchivo);
        var cargadas = JsonSerializer.Deserialize<List<AreaDefinicion>>(json, JsonOptions);
        lock (Candado)
        {
            _areas = cargadas is { Count: > 0 } ? cargadas : CrearPredeterminadas();
        }

        EscribirArchivo();
        NormalizarGlifos();
        AsegurarListas();
    }

    public static void ReemplazarDesdeNube(IReadOnlyList<AreaDefinicion> areas)
    {
        var entrantes = areas.ToList();
        lock (Candado)
        {
            if (MismaLista(_areas, entrantes))
            {
                return;
            }

            _areas = entrantes;
        }

        EscribirArchivo();
        foreach (var area in entrantes)
        {
            if (!Catalogos.TieneListaPropia(area.Id, Catalogos.CampoCategorias))
            {
                Catalogos.CopiarListasDePlantilla(area.Plantilla, area.Id);
            }
        }

        Cambiaron?.Invoke();
    }

    private static void Guardar()
    {
        EscribirArchivo();
        PublicacionPedida?.Invoke();
        Cambiaron?.Invoke();
    }

    private static void EscribirArchivo()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        List<AreaDefinicion> copia;
        lock (Candado)
        {
            copia = [.. _areas];
        }

        File.WriteAllText(RutaArchivo, JsonSerializer.Serialize(copia, JsonOptions));
    }

    public static AreaDefinicion? Obtener(Guid? id)
    {
        if (id is not Guid valor)
        {
            return null;
        }

        lock (Candado)
        {
            return _areas.FirstOrDefault(a => a.Id == valor);
        }
    }

    public static string Titulo(Guid? id) =>
        id is null ? "Sin área" : Obtener(id)?.Nombre ?? "Área";

    public static string Descripcion(Guid? id) =>
        id is null
            ? "Productos que no pertenecen a ningún área operativa."
            : Obtener(id)?.Descripcion ?? string.Empty;

    public static string Prefijo(Guid? id) =>
        Obtener(id)?.Prefijo.Trim().ToUpperInvariant() is { Length: > 0 } prefijo
            ? prefijo
            : "GEN";

    public static string Glifo(Guid? id) => Obtener(id)?.Glifo ?? "\uE8F1";

    public static AreaOperativa Plantilla(Guid? id) =>
        Obtener(id)?.Plantilla ?? AreaOperativa.Clinica;

    public static string EtiquetaMenu(Guid id) => $"{PrefijoEtiqueta}{id:D}";

    public static Guid? IdDesdeEtiqueta(string etiqueta)
    {
        if (string.Equals(etiqueta, EtiquetaSinArea, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (etiqueta.StartsWith(PrefijoEtiqueta, StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(etiqueta[PrefijoEtiqueta.Length..], out var id))
        {
            return id;
        }

        return etiqueta.ToLowerInvariant() switch
        {
            "clinica" => IdClinica,
            "oficina" => IdOficina,
            "limpieza" => IdLimpieza,
            _ => null
        };
    }

    public static Guid? ResolverId(string texto)
    {
        var normal = texto.Trim();
        if (Guid.TryParse(normal, out var id))
        {
            return Obtener(id)?.Id ?? id;
        }

        AreaDefinicion? porNombre;
        lock (Candado)
        {
            porNombre = _areas.FirstOrDefault(a =>
                a.Nombre.Equals(normal, StringComparison.CurrentCultureIgnoreCase) ||
                a.Prefijo.Equals(normal, StringComparison.OrdinalIgnoreCase));
        }
        if (porNombre is not null)
        {
            return porNombre.Id;
        }

        return normal.ToLowerInvariant() switch
        {
            "clinica" or "clínica" or "cli" => IdClinica,
            "oficina" or "ofi" => IdOficina,
            "limpieza" or "lim" => IdLimpieza,
            _ => null
        };
    }

    public static Guid? DesdeEnumAntiguo(int valor) => valor switch
    {
        0 => IdClinica,
        1 => IdOficina,
        2 => IdLimpieza,
        _ => null
    };

    public static AreaDefinicion Agregar(string nombre, string descripcion, string prefijo, AreaOperativa plantilla, string glifo)
    {
        var area = new AreaDefinicion
        {
            Id = Guid.NewGuid(),
            Nombre = nombre.Trim(),
            Descripcion = descripcion.Trim(),
            Prefijo = PrefijoLimpio(prefijo, nombre),
            Plantilla = plantilla,
            Glifo = GlifoOElegido(glifo, plantilla)
        };
        Catalogos.CopiarListasDePlantilla(plantilla, area.Id);
        lock (Candado)
        {
            area.Orden = _areas.Count == 0 ? 1 : _areas.Max(a => a.Orden) + 1;
            _areas.Add(area);
        }

        Guardar();
        return area;
    }

    public static void Actualizar(Guid id, string nombre, string descripcion, string prefijo, AreaOperativa plantilla, string glifo)
    {
        var area = Obtener(id) ?? throw new InvalidOperationException("El área ya no existe.");
        area.Nombre = nombre.Trim();
        area.Descripcion = descripcion.Trim();
        area.Prefijo = PrefijoLimpio(prefijo, nombre);
        area.Plantilla = plantilla;
        area.Glifo = GlifoOElegido(glifo, plantilla);
        Guardar();
    }

    public static bool Eliminar(Guid id)
    {
        int n;
        lock (Candado)
        {
            n = _areas.RemoveAll(a => a.Id == id);
        }

        if (n > 0)
        {
            Guardar();
        }

        return n > 0;
    }

    public static bool Mover(Guid id, int desplazamiento)
    {
        lock (Candado)
        {
            var ordenadas = _areas.OrderBy(a => a.Orden).ThenBy(a => a.Nombre).ToList();
            var indice = ordenadas.FindIndex(a => a.Id == id);
            var destino = indice + desplazamiento;
            if (indice < 0 || destino < 0 || destino >= ordenadas.Count)
            {
                return false;
            }

            (ordenadas[indice], ordenadas[destino]) = (ordenadas[destino], ordenadas[indice]);
            for (var i = 0; i < ordenadas.Count; i++)
            {
                ordenadas[i].Orden = i + 1;
            }

            _areas = ordenadas;
        }

        Guardar();
        return true;
    }

    private static bool MismaLista(IReadOnlyList<AreaDefinicion> actual, IReadOnlyList<AreaDefinicion> entrante)
    {
        if (actual.Count != entrante.Count)
        {
            return false;
        }

        foreach (var area in entrante)
        {
            var previa = actual.FirstOrDefault(a => a.Id == area.Id);
            if (previa is null ||
                previa.Orden != area.Orden ||
                previa.Plantilla != area.Plantilla ||
                !string.Equals(previa.Nombre, area.Nombre, StringComparison.Ordinal) ||
                !string.Equals(previa.Descripcion, area.Descripcion, StringComparison.Ordinal) ||
                !string.Equals(previa.Prefijo, area.Prefijo, StringComparison.Ordinal) ||
                !string.Equals(previa.Glifo, area.Glifo, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public static bool DifiereDePredeterminadas()
    {
        List<AreaDefinicion> copia;
        lock (Candado)
        {
            copia = [.. _areas];
        }

        return !MismaLista(CrearPredeterminadas(), copia);
    }

    private static string PrefijoLimpio(string prefijo, string nombre)
    {
        var limpio = LetrasYNumeros(prefijo, 6);
        if (limpio.Length >= 2)
        {
            return limpio;
        }

        var desdeNombre = LetrasYNumeros(nombre, 3);
        return desdeNombre.Length >= 2 ? desdeNombre : "GEN";
    }

    private static string LetrasYNumeros(string texto, int maximo) =>
        new string(texto.Trim()
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .Take(maximo)
            .ToArray());

    private static string GlifoOElegido(string glifo, AreaOperativa plantilla) =>
        string.IsNullOrWhiteSpace(glifo) ? GlifoPlantilla(plantilla) : GlifosWinUi.Caracter(glifo);

    private static string GlifoPlantilla(AreaOperativa plantilla) => plantilla switch
    {
        AreaOperativa.Oficina => "\uE8F1",
        AreaOperativa.Limpieza => "\uEA18",
        _ => "\uE95E"
    };

    private static void NormalizarGlifos()
    {
        lock (Candado)
        {
            foreach (var area in _areas)
            {
                area.Glifo = GlifosWinUi.Caracter(area.Glifo);
            }
        }
    }

    private static void AsegurarListas()
    {
        foreach (var area in Todas)
        {
            if (!Catalogos.TieneListaPropia(area.Id, Catalogos.CampoCategorias))
            {
                Catalogos.CopiarListasDePlantilla(area.Plantilla, area.Id);
            }
        }

        if (!Catalogos.TieneListaPropia(null, Catalogos.CampoCategorias))
        {
            Catalogos.CopiarListasDePlantilla(AreaOperativa.Clinica, null);
        }
    }

    private static List<AreaDefinicion> CrearPredeterminadas() =>
    [
        new()
        {
            Id = IdInsumosMedicos,
            Nombre = "Insumos Médicos",
            Descripcion = "Instrumental, insumos estériles, medicamentos y equipos de procedimiento capilar.",
            Prefijo = "CDE",
            Glifo = "\uE95E",
            Plantilla = AreaOperativa.Clinica,
            Orden = 1
        },
        new()
        {
            Id = IdProductosVenta,
            Nombre = "Productos de Venta",
            Descripcion = "Productos de venta y botica.",
            Prefijo = "CLB",
            Glifo = "\uE7BF",
            Plantilla = AreaOperativa.Clinica,
            Orden = 2
        },
        new()
        {
            Id = IdLaboratorio,
            Nombre = "Laboratorio",
            Descripcion = "Reactivos, soluciones y material de laboratorio.",
            Prefijo = "CDL",
            Glifo = "\uE90F",
            Plantilla = AreaOperativa.Clinica,
            Orden = 3
        },
        new()
        {
            Id = IdLimpieza,
            Nombre = "Materiales y Suministros",
            Descripcion = "Desinfectantes, detergentes, EPP y control de residuos de la clínica.",
            Prefijo = "CDLM",
            Glifo = "\uEA18",
            Plantilla = AreaOperativa.Limpieza,
            Orden = 4
        }
    ];
}
