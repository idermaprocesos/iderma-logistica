using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class PartidasInventario
{
    public const double Epsilon = 0.0001;

    public static void Normalizar(FichaTecnica ficha)
    {
        ficha.Partidas ??= [];
        foreach (var partida in ficha.Partidas)
        {
            if (partida.Id == Guid.Empty)
            {
                partida.Id = Guid.NewGuid();
            }

            partida.Lote = (partida.Lote ?? string.Empty).Trim();
            if (partida.Cantidad < 0)
            {
                partida.Cantidad = 0;
            }
        }

        MigrarSiVacio(ficha);
        FusionarIguales(ficha);
        AplicarCamposDerivados(ficha);
    }

    public static void PrepararParaGuardar(FichaTecnica ficha)
    {
        ficha.Partidas ??= [];
        ficha.Partidas.RemoveAll(p =>
            p.Cantidad <= Epsilon
            && string.IsNullOrWhiteSpace(p.Lote)
            && p.FechaCaducidad is null);

        if (ficha.Partidas.Count == 0)
        {
            if (ficha.Existencia > Epsilon
                || !string.IsNullOrWhiteSpace(ficha.Lote)
                || ficha.FechaCaducidad.HasValue)
            {
                ficha.Partidas.Add(NuevaDesdeFicha(ficha));
            }
        }

        FusionarIguales(ficha);
        AplicarCamposDerivados(ficha);
    }

    public static int Quitar(FichaTecnica ficha, IEnumerable<Guid> ids)
    {
        var conjunto = ids.Where(id => id != Guid.Empty).ToHashSet();
        if (conjunto.Count == 0)
        {
            return 0;
        }

        ficha.Partidas ??= [];
        var quitados = ficha.Partidas.RemoveAll(p => conjunto.Contains(p.Id));
        AplicarCamposDerivados(ficha);
        return quitados;
    }

    public static void ConservarLotesAusentes(FichaTecnica destino, IEnumerable<PartidaInventario> previos)
    {
        destino.Partidas ??= [];
        foreach (var previa in previos)
        {
            var clave = Clave(previa.Lote, previa.FechaCaducidad);
            if (destino.Partidas.Any(p => Clave(p.Lote, p.FechaCaducidad) == clave))
            {
                continue;
            }

            destino.Partidas.Add(previa.Clonar());
        }

        FusionarIguales(destino);
        AplicarCamposDerivados(destino);
    }

    public static void FusionarEn(FichaTecnica destino, IEnumerable<PartidaInventario> extras)
    {
        destino.Partidas ??= [];
        foreach (var extra in extras)
        {
            Upsert(destino, extra.Lote, extra.FechaCaducidad, extra.Cantidad, extra.Id, reemplazarCantidad: true);
        }

        FusionarIguales(destino);
        AplicarCamposDerivados(destino);
    }

    public static PartidaInventario Upsert(
        FichaTecnica ficha,
        string? lote,
        DateTimeOffset? fecha,
        double cantidad,
        Guid? idPreferido = null,
        bool reemplazarCantidad = false,
        bool sumar = false)
    {
        ficha.Partidas ??= [];
        lote = (lote ?? string.Empty).Trim();
        var existente = Buscar(ficha, idPreferido, lote, fecha);
        if (existente is null)
        {
            existente = new PartidaInventario
            {
                Id = idPreferido is Guid id && id != Guid.Empty ? id : Guid.NewGuid(),
                Lote = lote,
                FechaCaducidad = fecha,
                Cantidad = Math.Max(0, cantidad)
            };
            ficha.Partidas.Add(existente);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(lote))
            {
                existente.Lote = lote;
            }

            if (fecha.HasValue)
            {
                existente.FechaCaducidad = fecha;
            }

            if (reemplazarCantidad)
            {
                existente.Cantidad = Math.Max(0, cantidad);
            }
            else if (sumar)
            {
                existente.Cantidad = Math.Max(0, existente.Cantidad + cantidad);
            }
        }

        return existente;
    }

    public static bool IntentarSalida(
        FichaTecnica ficha,
        Guid? partidaId,
        string? lote,
        DateTimeOffset? fecha,
        double cantidad,
        out PartidaInventario? partida,
        out string? error)
    {
        partida = null;
        error = null;
        if (!IntentarSalidaFefo(ficha, cantidad, partidaId, lote, fecha, automatico: false, out var consumos, out error)
            || consumos.Count == 0)
        {
            return false;
        }

        partida = consumos[0].Partida;
        return true;
    }

    public static bool IntentarSalidaFefo(
        FichaTecnica ficha,
        double cantidad,
        Guid? partidaId,
        string? lote,
        DateTimeOffset? fecha,
        bool automatico,
        out List<ConsumoPartida> consumos,
        out string? error)
    {
        consumos = [];
        error = null;
        if (cantidad <= Epsilon)
        {
            error = "Indique una cantidad mayor a cero.";
            return false;
        }

        Normalizar(ficha);
        if (automatico)
        {
            return ConsumirPorCaducidad(ficha, cantidad, consumos, out error);
        }

        var partida = Buscar(ficha, partidaId, lote, fecha);
        if (partida is not null)
        {
            if (partida.Cantidad + Epsilon < cantidad)
            {
                error = $"No hay existencia suficiente en ese lote. Disponible: {partida.Cantidad.ToString("0.##")}.";
                return false;
            }

            partida.Cantidad -= cantidad;
            consumos.Add(new ConsumoPartida(partida, cantidad));
            AplicarCamposDerivados(ficha);
            return true;
        }

        return ConsumirPorCaducidad(ficha, cantidad, consumos, out error);
    }

    public readonly record struct ConsumoPartida(PartidaInventario Partida, double Cantidad);

    public static PartidaInventario AplicarIngreso(
        FichaTecnica ficha,
        string? lote,
        DateTimeOffset? fecha,
        double cantidad,
        Guid? partidaId = null)
    {
        var partida = Upsert(ficha, lote, fecha, cantidad, partidaId, sumar: true);
        FusionarIguales(ficha);
        AplicarCamposDerivados(ficha);
        return partida;
    }

    public static void Revertir(FichaTecnica ficha, MovimientoInventario movimiento)
    {
        if (movimiento.Cantidad <= Epsilon)
        {
            return;
        }

        if (movimiento.EsSalida)
        {
            AplicarIngreso(ficha, movimiento.Lote, movimiento.FechaCaducidad, movimiento.Cantidad, movimiento.PartidaId);
            return;
        }

        IntentarSalida(
            ficha,
            movimiento.PartidaId,
            movimiento.Lote,
            movimiento.FechaCaducidad,
            movimiento.Cantidad,
            out _,
            out _);
        AplicarCamposDerivados(ficha);
    }

    public static IEnumerable<PartidaInventario> OrdenFefo(FichaTecnica ficha) =>
        ficha.Partidas
            .Where(p => p.Cantidad > Epsilon)
            .OrderBy(p => p.FechaCaducidad ?? DateTimeOffset.MaxValue)
            .ThenBy(p => p.Lote, StringComparer.CurrentCultureIgnoreCase);

    public static PartidaInventario? PreferidaParaSalida(FichaTecnica ficha) =>
        OrdenFefo(ficha).FirstOrDefault();

    public static PartidaInventario? Referencia(FichaTecnica ficha) =>
        PreferidaParaSalida(ficha)
        ?? ficha.Partidas
            .OrderBy(p => p.FechaCaducidad ?? DateTimeOffset.MaxValue)
            .ThenBy(p => p.Lote, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();

    public static IEnumerable<AlertaCaducidad> Vencidas(IEnumerable<FichaTecnica> fichas) =>
        Alertas(fichas, p => p.Cantidad > Epsilon && p.EstaVencido);

    public static IEnumerable<AlertaCaducidad> Proximas(IEnumerable<FichaTecnica> fichas, int dias) =>
        ProximasHasta(fichas, DateTimeOffset.Now.Date.AddDays(dias));

    public static IEnumerable<AlertaCaducidad> ProximasHasta(IEnumerable<FichaTecnica> fichas, DateTimeOffset limite)
    {
        var tope = limite.Date;
        return Alertas(
            fichas,
            p => p.Cantidad > Epsilon
                 && p.FechaCaducidad.HasValue
                 && !p.EstaVencido
                 && p.FechaCaducidad.Value.Date <= tope);
    }

    public static IEnumerable<string> NombresLote(FichaTecnica ficha)
    {
        if (ficha.Partidas.Count > 0)
        {
            return ficha.Partidas
                .Select(p => p.Lote)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim());
        }

        return string.IsNullOrWhiteSpace(ficha.Lote) ? [] : [ficha.Lote.Trim()];
    }

    public static bool TieneLote(FichaTecnica ficha, string lote) =>
        NombresLote(ficha).Any(l => string.Equals(l, lote.Trim(), StringComparison.CurrentCultureIgnoreCase));

    public static string Clave(string? lote, DateTimeOffset? fecha) =>
        $"{(lote ?? string.Empty).Trim().ToUpperInvariant()}|{fecha?.ToUniversalTime().Date:yyyy-MM-dd}";

    private static IEnumerable<AlertaCaducidad> Alertas(
        IEnumerable<FichaTecnica> fichas,
        Func<PartidaInventario, bool> predicado)
    {
        foreach (var ficha in fichas)
        {
            foreach (var partida in ficha.Partidas.Where(predicado).OrderBy(p => p.FechaCaducidad))
            {
                yield return new AlertaCaducidad { Ficha = ficha, Partida = partida };
            }
        }
    }

    private static void MigrarSiVacio(FichaTecnica ficha)
    {
        if (ficha.Partidas.Count > 0)
        {
            return;
        }

        if (ficha.Existencia > Epsilon
            || !string.IsNullOrWhiteSpace(ficha.Lote)
            || ficha.FechaCaducidad.HasValue)
        {
            ficha.Partidas.Add(NuevaDesdeFicha(ficha));
        }
    }

    private static PartidaInventario NuevaDesdeFicha(FichaTecnica ficha) => new()
    {
        Lote = ficha.Lote?.Trim() ?? string.Empty,
        FechaCaducidad = ficha.FechaCaducidad,
        Cantidad = Math.Max(0, ficha.Existencia)
    };

    private static void FusionarIguales(FichaTecnica ficha)
    {
        if (ficha.Partidas.Count < 2)
        {
            return;
        }

        var agrupadas = new List<PartidaInventario>();
        foreach (var partida in ficha.Partidas)
        {
            var clave = Clave(partida.Lote, partida.FechaCaducidad);
            var previa = agrupadas.FirstOrDefault(p => Clave(p.Lote, p.FechaCaducidad) == clave);
            if (previa is null)
            {
                agrupadas.Add(partida);
            }
            else
            {
                previa.Cantidad += partida.Cantidad;
            }
        }

        ficha.Partidas = agrupadas;
    }

    private static void AplicarCamposDerivados(FichaTecnica ficha)
    {
        ficha.Existencia = ficha.Partidas.Sum(p => p.Cantidad);
        var referencia = Referencia(ficha);
        if (referencia is null)
        {
            ficha.Lote = string.Empty;
            ficha.FechaCaducidad = null;
            return;
        }

        ficha.Lote = referencia.Lote;
        ficha.FechaCaducidad = referencia.FechaCaducidad;
    }

    private static bool ConsumirPorCaducidad(
        FichaTecnica ficha,
        double cantidad,
        List<ConsumoPartida> consumos,
        out string? error)
    {
        var disponible = OrdenFefo(ficha).Sum(p => p.Cantidad);
        if (disponible + Epsilon < cantidad)
        {
            error = $"No hay existencia suficiente. Disponible: {disponible.ToString("0.##")}.";
            return false;
        }

        var restante = cantidad;
        foreach (var partida in OrdenFefo(ficha).ToList())
        {
            if (restante <= Epsilon)
            {
                break;
            }

            var tomar = Math.Min(partida.Cantidad, restante);
            partida.Cantidad -= tomar;
            restante -= tomar;
            consumos.Add(new ConsumoPartida(partida, tomar));
        }

        AplicarCamposDerivados(ficha);
        error = null;
        return true;
    }

    private static PartidaInventario? Buscar(
        FichaTecnica ficha,
        Guid? id,
        string? lote,
        DateTimeOffset? fecha)
    {
        if (id is Guid partidaId && partidaId != Guid.Empty)
        {
            var porId = ficha.Partidas.FirstOrDefault(p => p.Id == partidaId);
            if (porId is not null)
            {
                return porId;
            }
        }

        var clave = Clave(lote, fecha);
        return ficha.Partidas.FirstOrDefault(p => Clave(p.Lote, p.FechaCaducidad) == clave);
    }
}
