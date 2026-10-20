using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using Windows.Storage.Pickers;

namespace IdermaFichas.Helpers;

public static class VentanaHelper
{
    public static void AsociarSelector(object selector, Window? ventana = null)
    {
        var hwnd = ventana is null ? 0 : WindowNative.GetWindowHandle(ventana);
        if (hwnd == 0)
        {
            hwnd = ObtenerHwnd();
        }

        if (hwnd == 0)
        {
            throw new InvalidOperationException("No hay una ventana activa para abrir el selector de archivos.");
        }

        InitializeWithWindow.Initialize(selector, hwnd);
    }

    public static void ConfigurarInicio(FileSavePicker selector) =>
        selector.SuggestedStartLocation = PickerLocationId.Downloads;

    public static void ConfigurarInicio(FileOpenPicker selector) =>
        selector.SuggestedStartLocation = PickerLocationId.Downloads;

    private static nint ObtenerHwnd()
    {
        if (App.Instance.MainAppWindow is Window ventana)
        {
            var hwnd = WindowNative.GetWindowHandle(ventana);
            if (hwnd != 0)
            {
                return hwnd;
            }
        }

        return GetActiveWindow();
    }

    [DllImport("user32.dll")]
    private static extern nint GetActiveWindow();
}
