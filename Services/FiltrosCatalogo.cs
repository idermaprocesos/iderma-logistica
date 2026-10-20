using IdermaFichas.Models;

namespace IdermaFichas.Services;

public enum FiltroStockCatalogo
{
    Todos,
    Critico,
    SinExistencia,
    ConExistencia
}

public enum FiltroCaducidadCatalogo
{
    Todas,
    Vencidas,
    Proximas,
    SinFecha,
    Vigentes
}

public static class FiltrosCatalogo
{
    public const string TodasCategorias = "Todas las categorías";
    public const string TodosProveedores = "Todos los proveedores";
    public const string TodasUbicaciones = "Todas las ubicaciones";
    public const string TodosLotes = "Todos los lotes";

    public static readonly IReadOnlyList<string> OpcionesStock =
    [
        "Todo el stock",
        "Stock crítico",
        "Sin existencia",
        "Con existencia"
    ];

    public static readonly IReadOnlyList<string> OpcionesCaducidad =
    [
        "Toda caducidad",
        "Vencidos",
        "Próximos 30 días",
        "Sin fecha",
        "Vigentes"
    ];

    public static bool Coincide(FichaTecnica ficha, FiltroStockCatalogo stock, FiltroCaducidadCatalogo caducidad,
        string? categoria, string? proveedor, string? ubicacion, string? lote)
    {
        if (!PasaStock(ficha, stock) || !PasaCaducidad(ficha, caducidad))
        {
            return false;
        }

        if (!PasaLista(ficha.Categoria, categoria, TodasCategorias))
        {
            return false;
        }

        if (!PasaLista(ficha.Proveedor, proveedor, TodosProveedores))
        {
            return false;
        }

        if (!PasaLista(ficha.Ubicacion, ubicacion, TodasUbicaciones))
        {
            return false;
        }

        return PasaLote(ficha, lote);
    }

    public static IReadOnlyList<string> ValoresUnicos(IEnumerable<FichaTecnica> fichas, Func<FichaTecnica, string> campo, string todas)
    {
        var valores = fichas
            .Select(campo)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(v => v, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        valores.Insert(0, todas);
        return valores;
    }

    private static bool PasaStock(FichaTecnica ficha, FiltroStockCatalogo filtro) => filtro switch
    {
        FiltroStockCatalogo.Critico => ficha.EstaStockCritico,
        FiltroStockCatalogo.SinExistencia => ficha.Existencia <= 0,
        FiltroStockCatalogo.ConExistencia => ficha.Existencia > 0,
        _ => true
    };

    private static bool PasaCaducidad(FichaTecnica ficha, FiltroCaducidadCatalogo filtro) => filtro switch
    {
        FiltroCaducidadCatalogo.Vencidas => ficha.EstaVencido,
        FiltroCaducidadCatalogo.Proximas => ficha.EstaProximoAVencer,
        FiltroCaducidadCatalogo.SinFecha => !TieneFechaCaducidad(ficha),
        FiltroCaducidadCatalogo.Vigentes => TieneFechaCaducidad(ficha) && ficha.Partidas.Any(p =>
            p.Cantidad > 0.0001 && p.FechaCaducidad.HasValue && !p.EstaVencido && !p.EstaProximoAVencer),
        _ => true
    };

    public static IReadOnlyList<string> LotesUnicos(IEnumerable<FichaTecnica> fichas)
    {
        var valores = fichas
            .SelectMany(PartidasInventario.NombresLote)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(v => v, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        valores.Insert(0, TodosLotes);
        return valores;
    }

    private static bool TieneFechaCaducidad(FichaTecnica ficha) =>
        ficha.Partidas.Any(p => p.FechaCaducidad.HasValue) || ficha.FechaCaducidad.HasValue;

    private static bool PasaLote(FichaTecnica ficha, string? lote)
    {
        if (string.IsNullOrWhiteSpace(lote) ||
            string.Equals(lote, TodosLotes, StringComparison.CurrentCultureIgnoreCase))
        {
            return true;
        }

        return PartidasInventario.TieneLote(ficha, lote);
    }

    private static bool PasaLista(string valor, string? seleccionado, string todas)
    {
        if (string.IsNullOrWhiteSpace(seleccionado) ||
            string.Equals(seleccionado, todas, StringComparison.CurrentCultureIgnoreCase))
        {
            return true;
        }

        return string.Equals(valor?.Trim(), seleccionado.Trim(), StringComparison.CurrentCultureIgnoreCase);
    }
}

public sealed record CatalogoNavegacion(
    Guid? FichaId = null,
    FiltroStockCatalogo Stock = FiltroStockCatalogo.Todos,
    FiltroCaducidadCatalogo Caducidad = FiltroCaducidadCatalogo.Todas);

public sealed record CaducidadNavegacion(Guid? FichaId = null, bool Proximos = false);
