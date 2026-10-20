using System.Runtime.InteropServices;
using IdermaFichas.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace IdermaFichas;

public static class Program
{
    private const string ClaveInstancia = "IdermaCapilarApp";

    [DllImport("Microsoft.WindowsAppRuntime.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int WindowsAppRuntime_EnsureIsLoaded();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetEnvironmentVariable(string lpName, string lpValue);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint dwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MessageBoxW(nint hWnd, string lpText, string lpCaption, uint uType);

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
            SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY_PID", GetCurrentProcessId().ToString());
            _ = WindowsAppRuntime_EnsureIsLoaded();

            if (RedirigirSiYaEstaAbierta())
            {
                return;
            }

            Application.Start(p =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
        catch (Exception ex)
        {
            MessageBoxW(0, ex.ToString(), "Iderma Capilar App", 0x10);
        }
    }

    /// <summary>
    /// Un clic en el aviso de Windows inicia otro proceso. Si la app ya está abierta,
    /// ese proceso le pasa la activación a la ventana existente y se cierra.
    /// </summary>
    private static bool RedirigirSiYaEstaAbierta()
    {
        try
        {
            var actual = AppInstance.GetCurrent();
            var argumentos = actual.GetActivatedEventArgs();
            var principal = AppInstance.FindOrRegisterForKey(ClaveInstancia);
            if (principal.IsCurrent)
            {
                principal.Activated += (_, e) => AvisoWindowsSolicitudes.Recibir(e);
                return false;
            }

            AllowSetForegroundWindow(principal.ProcessId);
            Redirigir(argumentos, principal);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Redirigir(AppActivationArguments argumentos, AppInstance principal)
    {
        using var listo = new Semaphore(0, 1);
        Task.Run(() =>
        {
            try
            {
                principal.RedirectActivationToAsync(argumentos).AsTask().Wait();
            }
            finally
            {
                listo.Release();
            }
        });
        listo.WaitOne();
    }
}
