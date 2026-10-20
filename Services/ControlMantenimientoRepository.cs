using System.Text.Json;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public sealed class ControlMantenimientoRepository
{
    public const string IdDocumentoRangos = "rangos";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly object _candado = new();
    private RangosControlEquipo _rangos = new();
    private readonly List<EquipoControl> _equipos = [];

    public string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "control-mantenimiento.json");

    public void Cargar()
    {
        lock (_candado)
        {
            _equipos.Clear();
            _rangos = new RangosControlEquipo();
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
            if (!File.Exists(RutaArchivo))
            {
                _rangos = EjemplosControlMantenimiento.Rangos();
                _equipos.AddRange(EjemplosControlMantenimiento.Equipos());
                GuardarInterno();
                return;
            }

            try
            {
                var archivo = JsonSerializer.Deserialize<ArchivoControlMantenimiento>(File.ReadAllText(RutaArchivo), JsonOptions);
                if (archivo?.Rangos is not null)
                {
                    _rangos = archivo.Rangos;
                }

                if (archivo?.Equipos is { Count: > 0 })
                {
                    _equipos.AddRange(archivo.Equipos);
                }
            }
            catch
            {
                ArchivoAtomico.AislarDañado(RutaArchivo);
                _rangos = EjemplosControlMantenimiento.Rangos();
                _equipos.AddRange(EjemplosControlMantenimiento.Equipos());
                GuardarInterno();
                return;
            }

            _rangos.Normalizar();
            var ejemplos = EjemplosControlMantenimiento.Equipos().ToDictionary(e => e.Id);
            var migrar = false;
            foreach (var equipo in _equipos)
            {
                if (equipo.IntervaloDias <= 0 && ejemplos.TryGetValue(equipo.Id, out var ejemplo))
                {
                    equipo.IntervaloDias = ejemplo.IntervaloDias;
                    equipo.AvisoDias = ejemplo.AvisoDias;
                    migrar = true;
                }
                else if (equipo.IntervaloDias <= 0)
                {
                    equipo.NormalizarPlazo(_rangos);
                    migrar = true;
                }
                else
                {
                    equipo.NormalizarPlazo();
                }
            }

            if (migrar)
            {
                GuardarInterno();
            }
        }
    }

    public IReadOnlyList<EquipoControl> Listar()
    {
        lock (_candado)
        {
            return _equipos
                .Select(Copiar)
                .OrderBy(e => e.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }

    public string? Agregar(EquipoControl equipo)
    {
        var error = Preparar(equipo, null);
        if (error is not null)
        {
            return error;
        }

        lock (_candado)
        {
            error = CodigoRepetido(equipo);
            if (error is not null)
            {
                return error;
            }

            if (string.IsNullOrWhiteSpace(equipo.Codigo))
            {
                equipo.Codigo = SiguienteCodigo();
            }

            _equipos.Add(Copiar(equipo));
            GuardarInterno();
        }

        ModulosNube.PublicarControlMantenimiento();
        return null;
    }

    public string? Actualizar(EquipoControl equipo)
    {
        var error = Preparar(equipo, equipo.Id);
        if (error is not null)
        {
            return error;
        }

        lock (_candado)
        {
            var indice = _equipos.FindIndex(e => e.Id == equipo.Id);
            if (indice < 0)
            {
                return "Ese equipo ya no está en la lista.";
            }

            error = CodigoRepetido(equipo);
            if (error is not null)
            {
                return error;
            }

            if (string.IsNullOrWhiteSpace(equipo.Codigo))
            {
                equipo.Codigo = _equipos[indice].Codigo;
            }

            _equipos[indice] = Copiar(equipo);
            GuardarInterno();
        }

        ModulosNube.PublicarControlMantenimiento();
        return null;
    }

    public void RegistrarControl(Guid id)
    {
        lock (_candado)
        {
            var equipo = _equipos.FirstOrDefault(e => e.Id == id);
            if (equipo is null)
            {
                return;
            }

            equipo.UltimoControl = DateTimeOffset.Now;
            GuardarInterno();
        }

        ModulosNube.PublicarControlMantenimiento();
    }

    public bool AplicarDesdeNube(
        IReadOnlyList<EquipoControl> equipos,
        IReadOnlyCollection<Guid> eliminados,
        bool reemplazar)
    {
        var huboCambio = false;
        lock (_candado)
        {
            if (reemplazar)
            {
                _equipos.Clear();
                _equipos.AddRange(equipos.Select(Copiar));
                huboCambio = true;
            }
            else
            {
                var ids = eliminados.ToHashSet();
                if (_equipos.RemoveAll(e => ids.Contains(e.Id)) > 0)
                {
                    huboCambio = true;
                }

                foreach (var cambio in equipos)
                {
                    cambio.NormalizarPlazo(_rangos);
                    var indice = _equipos.FindIndex(e => e.Id == cambio.Id);
                    if (indice >= 0)
                    {
                        _equipos[indice] = Copiar(cambio);
                    }
                    else
                    {
                        _equipos.Add(Copiar(cambio));
                    }

                    huboCambio = true;
                }
            }

            if (huboCambio)
            {
                GuardarInterno();
            }
        }

        return huboCambio;
    }

    private string? Preparar(EquipoControl equipo, Guid? propio)
    {
        equipo.Nombre = (equipo.Nombre ?? string.Empty).Trim();
        equipo.Codigo = (equipo.Codigo ?? string.Empty).Trim();
        equipo.Ubicacion = (equipo.Ubicacion ?? string.Empty).Trim();
        equipo.Responsable = (equipo.Responsable ?? string.Empty).Trim();
        equipo.Nota = (equipo.Nota ?? string.Empty).Trim();
        if (equipo.Nombre.Length == 0)
        {
            return "Escribe el nombre del equipo.";
        }

        if (equipo.Id == Guid.Empty)
        {
            equipo.Id = Guid.NewGuid();
        }

        equipo.NormalizarPlazo();
        _ = propio;
        return null;
    }

    private string? CodigoRepetido(EquipoControl equipo)
    {
        if (equipo.Codigo.Length == 0)
        {
            return null;
        }

        var repetido = _equipos.Any(e =>
            e.Id != equipo.Id &&
            string.Equals(e.Codigo, equipo.Codigo, StringComparison.OrdinalIgnoreCase));
        return repetido ? "Ya hay un equipo con el código " + equipo.Codigo + "." : null;
    }

    private string SiguienteCodigo()
    {
        var mayor = 0;
        foreach (var equipo in _equipos)
        {
            if (equipo.Codigo.StartsWith("EQ-", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(equipo.Codigo[3..], out var numero))
            {
                mayor = Math.Max(mayor, numero);
            }
        }

        return "EQ-" + (mayor + 1).ToString("000");
    }

    private void GuardarInterno()
    {
        var archivo = new ArchivoControlMantenimiento
        {
            Rangos = Copiar(_rangos),
            Equipos = _equipos.Select(Copiar).ToList()
        };
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        ArchivoAtomico.EscribirTexto(RutaArchivo, JsonSerializer.Serialize(archivo, JsonOptions));
    }

    private static RangosControlEquipo Copiar(RangosControlEquipo origen) => new()
    {
        PreventivoDias = origen.PreventivoDias,
        AvisoPreventivoDias = origen.AvisoPreventivoDias,
        CorrectivoDias = origen.CorrectivoDias,
        AvisoCorrectivoDias = origen.AvisoCorrectivoDias,
        CriticoDias = origen.CriticoDias,
        AvisoCriticoDias = origen.AvisoCriticoDias
    };

    private static EquipoControl Copiar(EquipoControl origen) => new()
    {
        Id = origen.Id,
        Codigo = origen.Codigo,
        Nombre = origen.Nombre,
        Ubicacion = origen.Ubicacion,
        Responsable = origen.Responsable,
        Clase = origen.Clase,
        IntervaloDias = origen.IntervaloDias,
        AvisoDias = origen.AvisoDias,
        UltimoControl = origen.UltimoControl,
        Nota = origen.Nota
    };

    private sealed class ArchivoControlMantenimiento
    {
        public RangosControlEquipo Rangos { get; set; } = new();
        public List<EquipoControl> Equipos { get; set; } = [];
    }
}
