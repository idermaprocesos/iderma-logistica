using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace IdermaFichas.Helpers;

public sealed class PanelAjuste : Panel
{
    public static readonly DependencyProperty EspacioHorizontalProperty =
        DependencyProperty.Register(nameof(EspacioHorizontal), typeof(double), typeof(PanelAjuste),
            new PropertyMetadata(6d, (_, _) => { }));

    public static readonly DependencyProperty EspacioVerticalProperty =
        DependencyProperty.Register(nameof(EspacioVertical), typeof(double), typeof(PanelAjuste),
            new PropertyMetadata(6d, (_, _) => { }));

    public double EspacioHorizontal
    {
        get => (double)GetValue(EspacioHorizontalProperty);
        set => SetValue(EspacioHorizontalProperty, value);
    }

    public double EspacioVertical
    {
        get => (double)GetValue(EspacioVerticalProperty);
        set => SetValue(EspacioVerticalProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var x = 0d;
        var y = 0d;
        var fila = 0d;
        var ancho = 0d;
        var espacioX = EspacioHorizontal;
        var espacioY = EspacioVertical;
        var maxAncho = double.IsInfinity(availableSize.Width) ? double.MaxValue : availableSize.Width;

        foreach (var hijo in Children)
        {
            hijo.Measure(availableSize);
            var tam = hijo.DesiredSize;
            if (x > 0 && x + tam.Width > maxAncho)
            {
                ancho = Math.Max(ancho, x - espacioX);
                y += fila + espacioY;
                x = 0;
                fila = 0;
            }

            x += tam.Width + espacioX;
            fila = Math.Max(fila, tam.Height);
        }

        ancho = Math.Max(ancho, Math.Max(0, x - espacioX));
        return new Size(ancho, y + fila);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0d;
        var y = 0d;
        var fila = 0d;
        var espacioX = EspacioHorizontal;
        var espacioY = EspacioVertical;

        foreach (var hijo in Children)
        {
            var tam = hijo.DesiredSize;
            if (x > 0 && x + tam.Width > finalSize.Width)
            {
                y += fila + espacioY;
                x = 0;
                fila = 0;
            }

            hijo.Arrange(new Rect(x, y, tam.Width, tam.Height));
            x += tam.Width + espacioX;
            fila = Math.Max(fila, tam.Height);
        }

        return finalSize;
    }
}
