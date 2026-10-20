namespace IdermaFichas.Models;

public sealed class ResultadoBusquedaGlobal
{
    public required string Titulo { get; init; }
    public required string Subtitulo { get; init; }
    public required string Categoria { get; init; }
    public required string Glifo { get; init; }
    public required string Destino { get; init; }
    public Guid? AreaId { get; init; }
    public Guid? FichaId { get; init; }
}
