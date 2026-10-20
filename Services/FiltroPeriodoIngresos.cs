using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class FiltroPeriodoIngresos
{
    public static readonly string[] NombresMes =
    [
        "Ene", "Feb", "Mar", "Abr", "May", "Jun",
        "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"
    ];

    public static readonly string[] NombresMesCompletos =
    [
        "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    ];

    public static IReadOnlyList<MovimientoInventario> Aplicar(
        IEnumerable<MovimientoInventario> movimientos,
        int? anio,
        IReadOnlySet<int> meses,
        DateTimeOffset? desde = null)
    {
        return movimientos.Where(m => Pasa(m.Fecha, anio, meses, desde)).ToList();
    }

    public static DateTimeOffset InicioVentanaMeses(int meses)
    {
        var hoy = DateTime.Today;
        var inicio = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-(Math.Max(1, meses) - 1));
        return new DateTimeOffset(inicio);
    }

    public static bool Pasa(DateTimeOffset fecha, int? anio, IReadOnlySet<int> meses, DateTimeOffset? desde = null)
    {
        DateTimeOffset local;
        try
        {
            local = fecha.ToLocalTime();
        }
        catch (ArgumentException)
        {
            return false;
        }
        if (desde is DateTimeOffset inicio)
        {
            return local >= inicio && local <= DateTimeOffset.Now;
        }

        if (anio is int a && local.Year != a)
        {
            return false;
        }

        return meses.Count == 0 || meses.Contains(local.Month);
    }

    public static string Describir(int? anio, IReadOnlySet<int> meses, DateTimeOffset? desde = null, int? atajoMeses = null)
    {
        if (atajoMeses is int n && desde is DateTimeOffset inicio)
        {
            return $"Últimos {n} meses (desde {inicio:dd/MM/yyyy})";
        }

        var parteAnio = anio is int a ? a.ToString() : "todos los años";
        if (meses.Count == 0)
        {
            return anio is null ? "Todo el historial" : $"Año {parteAnio}";
        }

        var nombres = meses.OrderBy(m => m).Select(m => NombresMesCompletos[m - 1]);
        var parteMeses = string.Join(", ", nombres);
        return anio is null
            ? $"Meses: {parteMeses} (todos los años)"
            : $"{parteMeses} de {parteAnio}";
    }
}
