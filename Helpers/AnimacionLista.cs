using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

namespace IdermaFichas.Helpers;

public static class AnimacionLista
{
    public static void Revelar(FrameworkElement? elemento)
    {
        if (elemento is null)
        {
            return;
        }

        elemento.Opacity = 1;
        var animacion = new DoubleAnimation
        {
            From = 0.35,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(280)),
            FillBehavior = FillBehavior.Stop
        };
        Storyboard.SetTarget(animacion, elemento);
        Storyboard.SetTargetProperty(animacion, "Opacity");
        var guion = new Storyboard();
        guion.Children.Add(animacion);
        try
        {
            guion.Begin();
        }
        catch
        {
            elemento.Opacity = 1;
        }
    }
}
