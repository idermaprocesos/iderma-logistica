using System.Runtime.InteropServices;
using IdermaFichas.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace IdermaFichas.Pages;

public sealed partial class AcercaDePage : Page
{
    private bool _abriendoJuego;

    public AcercaDePage()
    {
        InitializeComponent();
    }

    private async void TarjetaMarca_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        if (_abriendoJuego)
        {
            return;
        }

        _abriendoJuego = true;
        try
        {
            if (!ChocolateDoomLauncher.EstaListo)
            {
                var listo = await PrepararJuegoAsync();
                if (!listo)
                {
                    return;
                }
            }

            ChocolateDoomLauncher.Abrir();
        }
        catch (Exception ex)
        {
            await MostrarAvisoAsync("No se pudo abrir Chocolate Doom.", ex.Message);
        }
        finally
        {
            _abriendoJuego = false;
        }
    }

    private async Task<bool> PrepararJuegoAsync()
    {
        var texto = new TextBlock { Text = "Preparando Chocolate Doom…", TextWrapping = TextWrapping.Wrap };
        var dialogo = new ContentDialog
        {
            Title = "Chocolate Doom",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new ProgressRing { IsActive = true, Width = 36, Height = 36 },
                    texto
                }
            },
            CloseButtonText = "Cancelar",
            XamlRoot = XamlRoot
        };

        using var cancelacion = new CancellationTokenSource();
        dialogo.CloseButtonClick += (_, _) => cancelacion.Cancel();
        var progreso = new Progress<string>(mensaje => texto.Text = mensaje);
        var preparacion = ChocolateDoomLauncher.PrepararAsync(progreso, cancelacion.Token);
        var mostrar = dialogo.ShowAsync();
        try
        {
            await preparacion;
            dialogo.Hide();
            await mostrar;
            return true;
        }
        catch (OperationCanceledException)
        {
            dialogo.Hide();
            try
            {
                await mostrar;
            }
            catch (Exception)
            {
                // El diálogo ya se cerró.
            }

            return false;
        }
        catch (Exception ex)
        {
            dialogo.Hide();
            try
            {
                await mostrar;
            }
            catch (Exception)
            {
                // El diálogo ya se cerró.
            }

            await MostrarAvisoAsync("No se pudo preparar Chocolate Doom.", ex.Message);
            return false;
        }
    }

    private async Task MostrarAvisoAsync(string titulo, string mensaje)
    {
        var dialogo = new ContentDialog
        {
            Title = titulo,
            Content = mensaje,
            CloseButtonText = "Cerrar",
            XamlRoot = XamlRoot
        };
        await dialogo.ShowAsync();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var version = typeof(App).Assembly.GetName().Version;
        TextoEquipo.Text = Environment.MachineName;
        TextoUsuario.Text = $"{Environment.UserDomainName}\\{Environment.UserName}";
        TextoSistema.Text = RuntimeInformation.OSDescription;
        TextoRam.Text = FormatoRam();
        TextoProcesadores.Text = $"{Environment.ProcessorCount} lógicos · {RuntimeInformation.OSArchitecture}";
        TextoVersion.Text = version is null ? "V9.0.0" : $"V{version.Major}.{version.Minor}.{version.Build}";
    }

    private static string FormatoRam()
    {
        var estado = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref estado) || estado.ullTotalPhys == 0)
        {
            return "No disponible";
        }

        var totalGb = estado.ullTotalPhys / (1024d * 1024d * 1024d);
        var disponibleGb = estado.ullAvailPhys / (1024d * 1024d * 1024d);
        return $"{totalGb:0.0} GB  ·  {disponibleGb:0.0} GB libres";
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}
