using Microsoft.UI.Xaml;
using IdermaFichas.Helpers;

namespace IdermaFichas.Services;

public sealed class AutoguardadoService
{
    private readonly DispatcherTimer _temporizadorFicha = new();
    private readonly DispatcherTimer _temporizadorCopia = new();

    public AutoguardadoService()
    {
        _temporizadorFicha.Tick += TemporizadorFicha_Tick;
        _temporizadorCopia.Tick += TemporizadorCopia_Tick;
    }

    public void AplicarPreferencias()
    {
        var preferencias = App.Instance.Preferencias.Actual;
        _temporizadorFicha.Stop();
        _temporizadorCopia.Stop();

        if (preferencias.AutoguardadoActivo)
        {
            _temporizadorFicha.Interval = TimeSpan.FromMinutes(preferencias.IntervaloAutoguardadoMinutos);
            _temporizadorFicha.Start();
        }

        if (preferencias.CopiasActivas)
        {
            _temporizadorCopia.Interval = TimeSpan.FromHours(preferencias.IntervaloCopiasHoras);
            _temporizadorCopia.Start();
            if (App.Instance.Copias.DebeCrearAutomatica())
            {
                LanzarCopiaAutomatica();
            }
        }
    }

    private void TemporizadorFicha_Tick(object? sender, object e)
    {
        if (App.Instance.MainAppWindow?.PaginaActual is IEdicionProducto edicion)
        {
            edicion.AutoguardarSiEsPosible();
        }
    }

    private void TemporizadorCopia_Tick(object? sender, object e)
    {
        if (!App.Instance.Copias.DebeCrearAutomatica())
        {
            return;
        }

        LanzarCopiaAutomatica();
    }

    private static void LanzarCopiaAutomatica()
    {
        _ = Task.Run(() =>
        {
            try
            {
                App.Instance.Copias.Crear("automatica");
            }
            catch
            {
                // El temporizador reintenta.
            }
        });
    }
}
