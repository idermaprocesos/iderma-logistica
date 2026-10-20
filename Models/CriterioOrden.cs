namespace IdermaFichas.Models;

public enum CriterioOrden
{
    NombreAz,
    NombreZa,
    CodigoAz,
    ExistenciaAsc,
    ExistenciaDesc,
    CaducidadAsc,
    CaducidadDesc
}

public sealed record AreaNavegacion(Guid? AreaId, Guid? FichaId = null);

public sealed record ResultadoImportacion(int Nuevas, int Actualizadas, int Omitidas)
{
    public int Total => Nuevas + Actualizadas;

    public string Mensaje =>
        Omitidas == 0
            ? $"Se importaron {Total} fichas ({Nuevas} nuevas y {Actualizadas} actualizadas)."
            : $"Se importaron {Total} fichas ({Nuevas} nuevas y {Actualizadas} actualizadas). {Omitidas} fila(s) se omitieron por datos incompletos.";
}
