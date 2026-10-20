using System.Text.Json;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public sealed class HistorialPreciosRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly List<PrecioProducto> _precios = [];
    private readonly object _candado = new();

    public string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "historial-precios.json");

    public void Cargar()
    {
        lock (_candado)
        {
            _precios.Clear();
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
            if (!File.Exists(RutaArchivo))
            {
                GuardarInterno();
                return;
            }

            try
            {
                var json = File.ReadAllText(RutaArchivo);
                var cargados = JsonSerializer.Deserialize<List<PrecioProducto>>(json, JsonOptions);
                if (cargados is not null)
                {
                    _precios.AddRange(cargados);
                }
            }
            catch
            {
                ArchivoAtomico.AislarDañado(RutaArchivo);
                GuardarInterno();
            }
        }
    }

    public void Guardar()
    {
        lock (_candado)
        {
            GuardarInterno();
        }
    }

    public IReadOnlyList<PrecioProducto> DeFicha(Guid fichaId)
    {
        lock (_candado)
        {
            return _precios
                .Where(p => p.FichaId == fichaId)
                .OrderByDescending(p => p.Fecha)
                .ThenByDescending(p => p.Id)
                .Select(p => p.Clonar())
                .ToList();
        }
    }

    public PrecioProducto? Ultimo(Guid fichaId) =>
        DeFicha(fichaId).FirstOrDefault();

    public bool TieneArchivoAnterior(Guid fichaId)
    {
        var limite = DateTimeOffset.UtcNow.AddDays(-SincronizadorNube.DiasVentanaHistorial);
        lock (_candado)
        {
            return _precios.Any(p => p.FichaId == fichaId && p.Fecha < limite);
        }
    }

    public PrecioProducto GuardarPrecio(PrecioProducto precio)
    {
        lock (_candado)
        {
            precio.Moneda = MonedaPrecio.Normalizar(precio.Moneda);
            precio.Nota = (precio.Nota ?? string.Empty).Trim();
            var indice = _precios.FindIndex(p => p.Id == precio.Id);
            if (indice >= 0)
            {
                _precios[indice] = precio.Clonar();
            }
            else
            {
                if (precio.Id == Guid.Empty)
                {
                    precio.Id = Guid.NewGuid();
                }

                _precios.Add(precio.Clonar());
            }

            GuardarInterno();
            var guardado = precio.Clonar();
            ModulosNube.PublicarPrecio(guardado);
            return guardado;
        }
    }

    public bool Eliminar(Guid id)
    {
        lock (_candado)
        {
            var quitados = _precios.RemoveAll(p => p.Id == id);
            if (quitados > 0)
            {
                GuardarInterno();
                ModulosNube.EliminarPrecio(id);
            }

            return quitados > 0;
        }
    }

    private void GuardarInterno()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        ArchivoAtomico.EscribirTexto(RutaArchivo, JsonSerializer.Serialize(_precios, JsonOptions));
    }

    public List<PrecioProducto> CopiarTodos()
    {
        lock (_candado)
        {
            return _precios.Select(p => p.Clonar()).ToList();
        }
    }

    public bool AplicarDesdeNube(IReadOnlyList<PrecioProducto> cambios, IReadOnlyCollection<Guid> eliminados, bool reemplazar)
    {
        var huboCambio = false;
        lock (_candado)
        {
            if (reemplazar)
            {
                _precios.Clear();
                _precios.AddRange(cambios.Select(p => p.Clonar()));
                huboCambio = true;
            }
            else
            {
                var ids = eliminados.ToHashSet();
                if (_precios.RemoveAll(p => ids.Contains(p.Id)) > 0)
                {
                    huboCambio = true;
                }

                foreach (var cambio in cambios)
                {
                    cambio.Moneda = MonedaPrecio.Normalizar(cambio.Moneda);
                    var indice = _precios.FindIndex(p => p.Id == cambio.Id);
                    if (indice >= 0)
                    {
                        var actual = _precios[indice];
                        if (actual.FichaId == cambio.FichaId
                            && actual.Monto == cambio.Monto
                            && actual.Moneda == cambio.Moneda
                            && actual.Nota == cambio.Nota
                            && actual.Fecha == cambio.Fecha)
                        {
                            continue;
                        }

                        _precios[indice] = cambio.Clonar();
                    }
                    else
                    {
                        _precios.Add(cambio.Clonar());
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
}
