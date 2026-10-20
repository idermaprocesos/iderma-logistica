using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IdermaFichas.Models;

public sealed class FichaListaItem : INotifyPropertyChanged, IItemMarcable
{
    private bool _estaMarcada;

    public required FichaTecnica Ficha { get; init; }

    public Guid Id => Ficha.Id;

    public string ExistenciaTexto => Ficha.Existencia.ToString("0.##");

    public bool EstaMarcada
    {
        get => _estaMarcada;
        set
        {
            if (_estaMarcada == value)
            {
                return;
            }

            _estaMarcada = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? nombre = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
}
