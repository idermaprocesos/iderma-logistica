using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas.Pages;

internal static class RhUi
{
    public static RhRepository Repo => App.Instance.Rh;

    public static List<Colaborador> ComboColaboradores() =>
        Repo.Datos.Colaboradores.OrderBy(c => c.Nombre).ToList();
}
