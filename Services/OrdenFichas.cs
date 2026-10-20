using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class OrdenFichas
{
    public static IReadOnlyList<(CriterioOrden Criterio, string Texto)> Opciones { get; } =
    [
        (CriterioOrden.NombreAz, "Nombre A–Z"),
        (CriterioOrden.NombreZa, "Nombre Z–A"),
        (CriterioOrden.CodigoAz, "Código A–Z"),
        (CriterioOrden.ExistenciaAsc, "Existencia: menor a mayor"),
        (CriterioOrden.ExistenciaDesc, "Existencia: mayor a menor"),
        (CriterioOrden.CaducidadAsc, "Caducidad: más próxima"),
        (CriterioOrden.CaducidadDesc, "Caducidad: más lejana")
    ];

    public static CriterioOrden Predeterminado => CriterioOrden.CodigoAz;

    public static int IndicePredeterminado { get; } =
        Opciones.Select((o, i) => (o.Criterio, i)).First(x => x.Criterio == CriterioOrden.CodigoAz).i;

    public static IEnumerable<FichaTecnica> Aplicar(IEnumerable<FichaTecnica> fichas, CriterioOrden criterio)
    {
        return criterio switch
        {
            CriterioOrden.NombreZa => fichas
                .OrderByDescending(f => f.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(f => f.Codigo),
            CriterioOrden.CodigoAz => fichas
                .OrderBy(f => f.Codigo, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(f => f.Nombre),
            CriterioOrden.ExistenciaAsc => fichas
                .OrderBy(f => f.Existencia)
                .ThenBy(f => f.Nombre),
            CriterioOrden.ExistenciaDesc => fichas
                .OrderByDescending(f => f.Existencia)
                .ThenBy(f => f.Nombre),
            CriterioOrden.CaducidadAsc => fichas
                .OrderBy(f => f.FechaCaducidad ?? DateTimeOffset.MaxValue)
                .ThenBy(f => f.Nombre),
            CriterioOrden.CaducidadDesc => fichas
                .OrderByDescending(f => f.FechaCaducidad ?? DateTimeOffset.MinValue)
                .ThenBy(f => f.Nombre),
            CriterioOrden.NombreAz => fichas
                .OrderBy(f => f.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(f => f.Codigo),
            _ => fichas
                .OrderBy(f => f.Codigo, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(f => f.Nombre)
        };
    }
}
