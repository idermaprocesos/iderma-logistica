namespace IdermaFichas.Models;

public interface IItemMarcable
{
    Guid Id { get; }
    bool EstaMarcada { get; set; }
}
