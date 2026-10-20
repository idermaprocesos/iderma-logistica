using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;
using IdermaFichas.Models;

namespace IdermaFichas.Helpers;

public static class SeleccionListaFichas
{
    public static bool CtrlIzquierdoPulsado =>
        TeclaPulsada(VirtualKey.LeftControl) || TeclaPulsada(VirtualKey.Control);

    public static bool MayusPulsado =>
        TeclaPulsada(VirtualKey.LeftShift) || TeclaPulsada(VirtualKey.Shift);

    public static bool DebeMarcarSinAbrir(bool modoSeleccionar) =>
        modoSeleccionar || CtrlIzquierdoPulsado || MayusPulsado;

    public static void AplicarClic<T>(
        IReadOnlyList<T> visibles,
        HashSet<Guid> marcadas,
        T item,
        ref int ancla)
        where T : class, IItemMarcable
    {
        var indice = IndexOf(visibles, item);
        if (indice < 0)
        {
            return;
        }

        var ctrl = CtrlIzquierdoPulsado;
        var mayus = MayusPulsado;

        if (mayus && ancla >= 0 && ancla < visibles.Count)
        {
            var desde = Math.Min(ancla, indice);
            var hasta = Math.Max(ancla, indice);
            if (!ctrl)
            {
                Limpiar(visibles, marcadas);
            }

            for (var i = desde; i <= hasta; i++)
            {
                Marcar(visibles[i], marcadas, true);
            }

            return;
        }

        if (ctrl)
        {
            Marcar(item, marcadas, !item.EstaMarcada);
            ancla = indice;
            return;
        }

        Limpiar(visibles, marcadas);
        Marcar(item, marcadas, true);
        ancla = indice;
    }

    public static void SeleccionarTodas<T>(IReadOnlyList<T> visibles, HashSet<Guid> marcadas)
        where T : IItemMarcable
    {
        foreach (var item in visibles)
        {
            Marcar(item, marcadas, true);
        }
    }

    public static void DeseleccionarTodas<T>(IReadOnlyList<T> visibles, HashSet<Guid> marcadas)
        where T : IItemMarcable =>
        Limpiar(visibles, marcadas);

    public static void SeleccionarCruzada<T>(IReadOnlyList<T> visibles, HashSet<Guid> marcadas)
        where T : IItemMarcable
    {
        for (var i = 0; i < visibles.Count; i++)
        {
            Marcar(visibles[i], marcadas, i % 2 == 0);
        }
    }

    public static void SeleccionarArmonia<T>(IReadOnlyList<T> visibles, HashSet<Guid> marcadas)
        where T : IItemMarcable
    {
        for (var i = 0; i < visibles.Count; i++)
        {
            Marcar(visibles[i], marcadas, i % 3 != 2);
        }
    }

    private static void Limpiar<T>(IReadOnlyList<T> visibles, HashSet<Guid> marcadas)
        where T : IItemMarcable
    {
        foreach (var item in visibles)
        {
            Marcar(item, marcadas, false);
        }
    }

    private static void Marcar<T>(T item, HashSet<Guid> marcadas, bool marcado)
        where T : IItemMarcable
    {
        item.EstaMarcada = marcado;
        if (marcado)
        {
            marcadas.Add(item.Id);
        }
        else
        {
            marcadas.Remove(item.Id);
        }
    }

    private static int IndexOf<T>(IReadOnlyList<T> visibles, T item)
        where T : IItemMarcable
    {
        for (var i = 0; i < visibles.Count; i++)
        {
            if (ReferenceEquals(visibles[i], item) || visibles[i].Id == item.Id)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TeclaPulsada(VirtualKey tecla) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(tecla).HasFlag(CoreVirtualKeyStates.Down);
}
