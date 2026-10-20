using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class SalidasSolicitudClinica
{
    public static string Registrar(SolicitudClinica solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (solicitud.Items.Count == 0)
        {
            throw new InvalidOperationException("La solicitud no tiene productos para retirar.");
        }

        var repo = App.Instance.Repositorio;
        var preparadas = new List<(FichaTecnica Ficha, List<PartidasInventario.ConsumoPartida> Consumos, LineaSolicitudClinica Linea)>();
        var faltantes = new List<string>();

        foreach (var linea in solicitud.Items.Where(i => CantidadAEntregar(i) > 0))
        {
            var original = BuscarFicha(repo.Todas, linea);
            if (original is null)
            {
                faltantes.Add(NombreLinea(linea) + " (no está en inventario)");
                continue;
            }

            var ficha = original.Clonar();
            PartidasInventario.Normalizar(ficha);
            if (!PartidasInventario.IntentarSalidaFefo(
                    ficha,
                    CantidadAEntregar(linea),
                    partidaId: null,
                    lote: null,
                    fecha: null,
                    automatico: true,
                    out var consumos,
                    out var error)
                || consumos.Count == 0)
            {
                faltantes.Add($"{NombreLinea(linea)}: {error ?? "sin existencia suficiente"}");
                continue;
            }

            preparadas.Add((ficha, consumos, linea));
        }

        if (faltantes.Count > 0)
        {
            throw new InvalidOperationException(
                "No se pudo aceptar: faltan productos o stock.\n" + string.Join("\n", faltantes));
        }

        if (preparadas.Count == 0)
        {
            throw new InvalidOperationException("No hay cantidades válidas para registrar la salida.");
        }

        var movimientos = new List<MovimientoInventario>();
        var ahora = DateTimeOffset.Now;
        var tipoDocumento = "Solicitud clínica";
        var numero = string.IsNullOrWhiteSpace(solicitud.Folio) ? solicitud.Id : solicitud.Folio;
        var nota = string.IsNullOrWhiteSpace(solicitud.Enfermera)
            ? "Salida por solicitud de clínica"
            : $"Salida por solicitud de clínica · {solicitud.Enfermera}";

        foreach (var (ficha, consumos, _) in preparadas)
        {
            var resultante = ficha.Existencia + consumos.Sum(c => c.Cantidad);
            foreach (var consumo in consumos)
            {
                resultante -= consumo.Cantidad;
                movimientos.Add(new MovimientoInventario
                {
                    FichaId = ficha.Id,
                    PartidaId = consumo.Partida.Id,
                    Lote = consumo.Partida.Lote,
                    FechaCaducidad = consumo.Partida.FechaCaducidad,
                    Area = ficha.Area,
                    Fecha = ahora,
                    Cantidad = consumo.Cantidad,
                    ExistenciaResultante = resultante,
                    Tipo = "Salida",
                    Origen = "Salida",
                    TipoDocumento = tipoDocumento,
                    NumeroDocumento = numero,
                    Nota = nota
                });
            }
        }

        repo.GuardarFichas(preparadas.Select(p => p.Ficha), registrarIngresoAutomatico: false);
        App.Instance.Registro.Agregar(movimientos);

        var piezas = movimientos.Sum(m => m.Cantidad);
        return $"Se registraron {movimientos.Count} salida{(movimientos.Count == 1 ? "" : "s")} " +
               $"({piezas.ToString("0.##")} pzas) en Registro por {numero}.";
    }

    private static FichaTecnica? BuscarFicha(IReadOnlyList<FichaTecnica> fichas, LineaSolicitudClinica linea)
    {
        if (!string.IsNullOrWhiteSpace(linea.Codigo))
        {
            var porCodigo = fichas
                .Where(f => f.Codigo.Equals(linea.Codigo.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (porCodigo.Count == 1)
            {
                return porCodigo[0];
            }

            if (porCodigo.Count > 1)
            {
                return porCodigo.FirstOrDefault(f =>
                           !string.IsNullOrWhiteSpace(linea.Area) &&
                           f.AreaTitulo.Equals(linea.Area.Trim(), StringComparison.OrdinalIgnoreCase))
                       ?? porCodigo.FirstOrDefault(f =>
                           !string.IsNullOrWhiteSpace(linea.Nombre) &&
                           f.Nombre.Equals(linea.Nombre.Trim(), StringComparison.OrdinalIgnoreCase))
                       ?? porCodigo[0];
            }
        }

        if (string.IsNullOrWhiteSpace(linea.Nombre))
        {
            return null;
        }

        var porNombre = fichas
            .Where(f => f.Nombre.Equals(linea.Nombre.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (porNombre.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(linea.Area))
        {
            var conArea = porNombre.FirstOrDefault(f =>
                f.AreaTitulo.Equals(linea.Area.Trim(), StringComparison.OrdinalIgnoreCase));
            if (conArea is not null)
            {
                return conArea;
            }
        }

        return porNombre.Count == 1 ? porNombre[0] : null;
    }

    private static string NombreLinea(LineaSolicitudClinica linea)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(linea.Codigo))
        {
            partes.Add(linea.Codigo);
        }

        if (!string.IsNullOrWhiteSpace(linea.Nombre))
        {
            partes.Add(linea.Nombre);
        }

        partes.Add($"x{CantidadAEntregar(linea)}");
        return string.Join(" ", partes);
    }

    private static int CantidadAEntregar(LineaSolicitudClinica linea) =>
        Math.Max(0, linea.CantidadRespuesta ?? linea.Cantidad);
}
