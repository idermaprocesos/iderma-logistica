using System.Text.Json;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public sealed class RegistroInventarioRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly List<MovimientoInventario> _movimientos = [];
    private readonly Dictionary<Guid, List<MovimientoInventario>> _porFicha = [];
    private readonly object _sync = new();

    public string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "registro-ingresos.json");

    public void Cargar()
    {
        lock (_sync)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
            if (!File.Exists(RutaArchivo))
            {
                _movimientos.Clear();
                SembrarDesdeCatalogo();
                Guardar();
                ReconstruirIndice();
                return;
            }

            try
            {
                var json = File.ReadAllText(RutaArchivo);
                var cargados = JsonSerializer.Deserialize<List<MovimientoInventario>>(json, JsonOptions);
                _movimientos.Clear();
                if (cargados is not null)
                {
                    _movimientos.AddRange(cargados);
                }
            }
            catch
            {
                ArchivoAtomico.AislarDañado(RutaArchivo);
                _movimientos.Clear();
                Guardar();
            }

            ReconstruirIndice();
        }
    }

    public void Agregar(IEnumerable<MovimientoInventario> movimientos)
    {
        var lote = movimientos.Where(m => m.Cantidad > 0.0001).ToList();
        if (lote.Count == 0)
        {
            return;
        }

        lock (_sync)
        {
            _movimientos.AddRange(lote);
            Guardar();
            ReconstruirIndice();
            ModulosNube.PublicarRegistro(lote.Select(Clonar).ToList(), []);
        }
    }

    public IReadOnlyList<MovimientoInventario> PorFicha(Guid fichaId)
    {
        lock (_sync)
        {
            return _porFicha.TryGetValue(fichaId, out var lista)
                ? lista.ToList()
                : [];
        }
    }

    public double TotalIngresado(Guid fichaId)
    {
        lock (_sync)
        {
            return _porFicha.TryGetValue(fichaId, out var lista)
                ? lista.Sum(m => m.Cantidad)
                : 0;
        }
    }

    public IReadOnlyList<int> Anios()
    {
        lock (_sync)
        {
            return _movimientos
                .Select(AnioLocal)
                .Distinct()
                .OrderByDescending(a => a)
                .ToList();
        }
    }

    public IReadOnlyList<MovimientoInventario> IngresosEntre(DateTime desde, DateTime hasta)
    {
        var inicio = desde.Date;
        var fin = hasta.Date;
        if (fin < inicio)
        {
            (inicio, fin) = (fin, inicio);
        }

        lock (_sync)
        {
            return _movimientos
                .Where(m => !m.EsSalida && m.Cantidad > 0.0001 && EnDia(m.Fecha, inicio, fin))
                .ToList();
        }
    }

    private static int AnioLocal(MovimientoInventario movimiento)
    {
        try
        {
            return movimiento.Fecha.ToLocalTime().Year;
        }
        catch (ArgumentException)
        {
            return movimiento.Fecha.Year;
        }
    }

    private static bool EnDia(DateTimeOffset fecha, DateTime inicio, DateTime fin)
    {
        DateTime dia;
        try
        {
            dia = fecha.ToLocalTime().Date;
        }
        catch (ArgumentException)
        {
            dia = fecha.Date;
        }
        return dia >= inicio && dia <= fin;
    }

    public IReadOnlyList<MovimientoInventario> PorFicha(
        Guid fichaId,
        int? anio,
        IReadOnlySet<int> meses,
        DateTimeOffset? desde = null)
    {
        return FiltroPeriodoIngresos.Aplicar(PorFicha(fichaId), anio, meses, desde);
    }

    public double TotalIngresado(Guid fichaId, int? anio, IReadOnlySet<int> meses) =>
        PorFicha(fichaId, anio, meses).Sum(m => m.Cantidad);

    public MovimientoInventario? UltimoIngreso(Guid fichaId)
    {
        lock (_sync)
        {
            return _porFicha.TryGetValue(fichaId, out var lista) && lista.Count > 0
                ? lista[0]
                : null;
        }
    }

    public int Cantidad
    {
        get
        {
            lock (_sync)
            {
                return _movimientos.Count;
            }
        }
    }

    public void Vaciar()
    {
        lock (_sync)
        {
            var ids = _movimientos.Select(m => m.Id).ToList();
            _movimientos.Clear();
            Guardar();
            ReconstruirIndice();
            ReciboMovimientoService.EliminarVarios(ids);
            ModulosNube.PublicarRegistro([], [], true);
        }
    }

    public void ReconstruirDesdeExistencias()
    {
        lock (_sync)
        {
            var ids = _movimientos.Select(m => m.Id).ToList();
            _movimientos.Clear();
            ReciboMovimientoService.EliminarVarios(ids);
            SembrarDesdeCatalogo();
            Guardar();
            ReconstruirIndice();
            ModulosNube.PublicarRegistro(_movimientos.Select(Clonar).ToList(), [], true);
        }
    }

    public MovimientoInventario? Obtener(Guid id)
    {
        lock (_sync)
        {
            return _movimientos.FirstOrDefault(m => m.Id == id);
        }
    }

    public bool IntentarMutar(
        Guid fichaId,
        double existenciaActual,
        Action<List<MovimientoInventario>> mutar,
        out double existenciaNueva,
        out string? error)
    {
        lock (_sync)
        {
            var deFicha = _movimientos.Where(m => m.FichaId == fichaId).Select(Clonar).ToList();
            var antes = deFicha.ToDictionary(m => m.Id, Clonar);
            var inicial = existenciaActual - deFicha.Sum(m => m.EfectoExistencia);
            var idsAntes = deFicha.Select(m => m.Id).ToHashSet();
            mutar(deFicha);

            var orden = deFicha.OrderBy(m => m.Fecha).ThenBy(m => m.Id).ToList();
            var stock = inicial;
            foreach (var movimiento in orden)
            {
                stock += movimiento.EfectoExistencia;
                if (stock < -0.0001)
                {
                    existenciaNueva = existenciaActual;
                    error = "Con ese cambio la existencia quedaría negativa. Revise cantidades o el orden de los movimientos.";
                    return false;
                }

                movimiento.ExistenciaResultante = stock;
            }

            var idsDespues = deFicha.Select(m => m.Id).ToHashSet();
            _movimientos.RemoveAll(m => m.FichaId == fichaId);
            _movimientos.AddRange(deFicha);
            Guardar();
            ReconstruirIndice();
            ReciboMovimientoService.EliminarVarios(idsAntes.Except(idsDespues));
            var cambiados = deFicha
                .Where(m => !antes.TryGetValue(m.Id, out var previo) || !Igual(previo, m) || previo.RutaRecibo != m.RutaRecibo)
                .Select(Clonar)
                .ToList();
            ModulosNube.PublicarRegistro(cambiados, idsAntes.Except(idsDespues).ToList());
            existenciaNueva = stock;
            error = null;
            return true;
        }
    }

    private static MovimientoInventario Clonar(MovimientoInventario m) => new()
    {
        Id = m.Id,
        FichaId = m.FichaId,
        PartidaId = m.PartidaId,
        Lote = m.Lote,
        FechaCaducidad = m.FechaCaducidad,
        Area = m.Area,
        Fecha = m.Fecha,
        Cantidad = m.Cantidad,
        ExistenciaResultante = m.ExistenciaResultante,
        Tipo = m.Tipo,
        Origen = m.Origen,
        TipoDocumento = m.TipoDocumento,
        NumeroDocumento = m.NumeroDocumento,
        Nota = m.Nota,
        RutaRecibo = m.RutaRecibo,
        NombreRecibo = m.NombreRecibo
    };

    public void EliminarDeFichas(IEnumerable<Guid> ids)
    {
        var conjunto = ids.ToHashSet();
        if (conjunto.Count == 0)
        {
            return;
        }

        lock (_sync)
        {
            var idsMovimientos = _movimientos
                .Where(m => conjunto.Contains(m.FichaId))
                .Select(m => m.Id)
                .ToList();
            var n = _movimientos.RemoveAll(m => conjunto.Contains(m.FichaId));
            if (n == 0)
            {
                return;
            }

            Guardar();
            ReconstruirIndice();
            ReciboMovimientoService.EliminarVarios(idsMovimientos);
            ModulosNube.PublicarRegistro([], idsMovimientos);
        }
    }

    public List<MovimientoInventario> CopiarTodos()
    {
        lock (_sync)
        {
            return _movimientos.Select(Clonar).ToList();
        }
    }

    public bool TieneArchivoAnterior(Guid fichaId)
    {
        var limite = DateTimeOffset.UtcNow.AddDays(-SincronizadorNube.DiasVentanaHistorial);
        lock (_sync)
        {
            return _porFicha.TryGetValue(fichaId, out var lista)
                && lista.Any(m => m.Fecha < limite
                    && !string.Equals(m.Origen, "Stock inicial", StringComparison.OrdinalIgnoreCase));
        }
    }

    public bool AplicarDesdeNube(IReadOnlyList<MovimientoInventario> cambios, IReadOnlyCollection<Guid> eliminados, bool reemplazar)
    {
        var huboCambio = false;
        lock (_sync)
        {
            if (reemplazar)
            {
                _movimientos.Clear();
                _movimientos.AddRange(cambios.Select(Clonar));
                huboCambio = true;
            }
            else
            {
                var ids = eliminados.ToHashSet();
                if (_movimientos.RemoveAll(m => ids.Contains(m.Id)) > 0)
                {
                    huboCambio = true;
                }

                foreach (var cambio in cambios)
                {
                    var indice = _movimientos.FindIndex(m => m.Id == cambio.Id);
                    if (indice >= 0)
                    {
                        if (Igual(_movimientos[indice], cambio))
                        {
                            continue;
                        }

                        _movimientos[indice] = Clonar(cambio);
                    }
                    else
                    {
                        _movimientos.Add(Clonar(cambio));
                    }

                    huboCambio = true;
                }
            }

            if (huboCambio)
            {
                Guardar();
                ReconstruirIndice();
            }
        }

        return huboCambio;
    }

    private static bool Igual(MovimientoInventario actual, MovimientoInventario cambio) =>
        actual.FichaId == cambio.FichaId
        && actual.PartidaId == cambio.PartidaId
        && actual.Area == cambio.Area
        && actual.Cantidad == cambio.Cantidad
        && actual.ExistenciaResultante == cambio.ExistenciaResultante
        && actual.Fecha == cambio.Fecha
        && actual.FechaCaducidad == cambio.FechaCaducidad
        && actual.Tipo == cambio.Tipo
        && actual.Origen == cambio.Origen
        && actual.Lote == cambio.Lote
        && actual.TipoDocumento == cambio.TipoDocumento
        && actual.NumeroDocumento == cambio.NumeroDocumento
        && actual.Nota == cambio.Nota
        && actual.NombreRecibo == cambio.NombreRecibo;

    private void SembrarDesdeCatalogo()
    {
        foreach (var ficha in App.Instance.Repositorio.Todas)
        {
            if (ficha.Existencia <= 0)
            {
                continue;
            }

            _movimientos.Add(new MovimientoInventario
            {
                FichaId = ficha.Id,
                Area = ficha.Area,
                Fecha = ficha.FechaAdquisicion ?? ficha.FechaCreacion,
                Cantidad = ficha.Existencia,
                ExistenciaResultante = ficha.Existencia,
                Origen = "Stock inicial",
                Nota = "Existencia registrada al activar el historial de ingresos."
            });
        }
    }

    private void Guardar()
    {
        ArchivoAtomico.EscribirTexto(RutaArchivo, JsonSerializer.Serialize(_movimientos, JsonOptions));
    }

    private void ReconstruirIndice()
    {
        _porFicha.Clear();
        foreach (var movimiento in _movimientos)
        {
            if (!_porFicha.TryGetValue(movimiento.FichaId, out var lista))
            {
                lista = [];
                _porFicha[movimiento.FichaId] = lista;
            }

            lista.Add(movimiento);
        }

        foreach (var lista in _porFicha.Values)
        {
            lista.Sort((a, b) => b.Fecha.CompareTo(a.Fecha));
        }
    }
}
