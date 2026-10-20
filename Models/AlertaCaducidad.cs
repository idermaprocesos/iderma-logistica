namespace IdermaFichas.Models;

public sealed class AlertaCaducidad
{
    public required FichaTecnica Ficha { get; init; }
    public required PartidaInventario Partida { get; init; }

    public string Nombre => Ficha.Nombre;
    public string Resumen => string.IsNullOrWhiteSpace(Ficha.Codigo)
        ? Ficha.AreaTitulo
        : $"{Ficha.Codigo} · {Ficha.AreaTitulo}";
    public string LoteTexto => $"{Partida.LoteTexto} · {Partida.Cantidad.ToString("0.##")} {Ficha.UnidadMedida}".Trim();
    public string EstadoCaducidadTexto => Partida.EstadoCaducidadTexto;
    public string FechaCaducidadTexto => Partida.FechaCaducidadTexto;
}
