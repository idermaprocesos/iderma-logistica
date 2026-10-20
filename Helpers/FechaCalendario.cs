using Microsoft.UI.Xaml.Data;

namespace IdermaFichas.Helpers;

public static class FechaCalendario
{
    public static DateTimeOffset? ParaPicker(DateTimeOffset? fecha)
    {
        if (fecha is not DateTimeOffset valor)
        {
            return null;
        }

        try
        {
            var local = valor.ToLocalTime();
            if (local.Year is < 1601 or > 9999)
            {
                return null;
            }

            return new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, TimeSpan.Zero);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public static DateTimeOffset ParaPickerObligatorio(DateTimeOffset? fecha) =>
        ParaPicker(fecha) ?? new DateTimeOffset(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 0, 0, 0, TimeSpan.Zero);
}

public sealed class FechaCalendarioConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language) =>
        FechaCalendario.ParaPicker(value as DateTimeOffset?);

    public object? ConvertBack(object value, Type targetType, object parameter, string language) =>
        FechaCalendario.ParaPicker(value as DateTimeOffset?);
}
