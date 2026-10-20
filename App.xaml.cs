using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            try
            {
                var carpeta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "IdermaCapilar");
                Directory.CreateDirectory(carpeta);
                File.AppendAllText(
                    Path.Combine(carpeta, "fallo-ui.txt"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{e.Exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
            }

            e.Handled = true;
        };
        try
        {
            Catalogos.Cargar();
            AlmacenHistorialProveedores.Cargar();
            AreasOperativas.Cargar();
        }
        catch
        {
        }
        try
        {
            Repositorio.Cargar();
        }
        catch
        {
            // El archivo dañado ya se aparta en Cargar; no se sustituye por ejemplos.
        }

        InventarioNube.Preparar(Repositorio);

        try
        {
            Rh.Cargar();
        }
        catch
        {
            Rh = new RhRepository();
            Rh.Cargar();
        }

        try
        {
            Preferencias.Cargar();
        }
        catch
        {
        }
        try
        {
            Registro.Cargar();
        }
        catch
        {
            Registro = new RegistroInventarioRepository();
            Registro.Cargar();
        }

        try
        {
            HistorialPrecios.Cargar();
        }
        catch
        {
            HistorialPrecios = new HistorialPreciosRepository();
            HistorialPrecios.Cargar();
        }

        try
        {
            OrdenesCompra.Cargar();
        }
        catch
        {
            OrdenesCompra = new OrdenesCompraRepository();
            OrdenesCompra.Cargar();
        }

        try
        {
            OrdenesServicio.Cargar();
        }
        catch
        {
            OrdenesServicio = new OrdenesServicioRepository();
            OrdenesServicio.Cargar();
        }

        try
        {
            Mermas.Cargar();
        }
        catch
        {
            Mermas = new MermaRepository();
            Mermas.Cargar();
        }

        try
        {
            ControlMantenimiento.Cargar();
        }
        catch
        {
            ControlMantenimiento = new ControlMantenimientoRepository();
            ControlMantenimiento.Cargar();
        }

        try
        {
            Proveedores.Cargar();
        }
        catch
        {
            Proveedores = new ProveedoresRepository();
            Proveedores.Cargar();
        }

        try
        {
            PersonalMedico.Cargar();
        }
        catch
        {
            PersonalMedico = new PersonalMedicoRepository();
            PersonalMedico.Cargar();
        }

        ActualizacionService.AplicarCopiaTrasActualizacion();
    }

    public static App Instance => (App)Current;

    public MainWindow? MainAppWindow => _window as MainWindow;

    public FichaRepository Repositorio { get; } = new();

    public RegistroInventarioRepository Registro { get; private set; } = new();

    public HistorialPreciosRepository HistorialPrecios { get; private set; } = new();

    public OrdenesCompraRepository OrdenesCompra { get; private set; } = new();

    public OrdenesServicioRepository OrdenesServicio { get; private set; } = new();

    public MermaRepository Mermas { get; private set; } = new();

    public ControlMantenimientoRepository ControlMantenimiento { get; private set; } = new();

    public ProveedoresRepository Proveedores { get; private set; } = new();

    public RhRepository Rh { get; private set; } = new();

    public PersonalMedicoRepository PersonalMedico { get; private set; } = new();

    public PreferenciasService Preferencias { get; } = new();

    public AutoguardadoService Autoguardado { get; } = new();

    public BackupService Copias { get; } = new();

    public static void RecargarDatos()
    {
        Catalogos.Cargar();
        AlmacenHistorialProveedores.Cargar();
        AreasOperativas.Cargar();
        try
        {
            Instance.Repositorio.Cargar();
        }
        catch
        {
        }
        try
        {
            Instance.Rh.Cargar();
        }
        catch
        {
            Instance.Rh = new RhRepository();
            Instance.Rh.Cargar();
        }

        try
        {
            Instance.Registro.Cargar();
        }
        catch
        {
            Instance.Registro = new RegistroInventarioRepository();
            Instance.Registro.Cargar();
        }

        try
        {
            Instance.HistorialPrecios.Cargar();
        }
        catch
        {
            Instance.HistorialPrecios = new HistorialPreciosRepository();
            Instance.HistorialPrecios.Cargar();
        }

        try
        {
            Instance.OrdenesCompra.Cargar();
        }
        catch
        {
            Instance.OrdenesCompra = new OrdenesCompraRepository();
            Instance.OrdenesCompra.Cargar();
        }

        try
        {
            Instance.OrdenesServicio.Cargar();
        }
        catch
        {
            Instance.OrdenesServicio = new OrdenesServicioRepository();
            Instance.OrdenesServicio.Cargar();
        }

        try
        {
            Instance.Mermas.Cargar();
        }
        catch
        {
            Instance.Mermas = new MermaRepository();
            Instance.Mermas.Cargar();
        }

        try
        {
            Instance.ControlMantenimiento.Cargar();
        }
        catch
        {
            Instance.ControlMantenimiento = new ControlMantenimientoRepository();
            Instance.ControlMantenimiento.Cargar();
        }

        try
        {
            Instance.Proveedores.Cargar();
        }
        catch
        {
            Instance.Proveedores = new ProveedoresRepository();
            Instance.Proveedores.Cargar();
        }

        try
        {
            Instance.PersonalMedico.Cargar();
        }
        catch
        {
            Instance.PersonalMedico = new PersonalMedicoRepository();
            Instance.PersonalMedico.Cargar();
        }

        Instance.MainAppWindow?.ActualizarMenuAreas();
        AreasOperativasNube.Publicar();
        CatalogosNube.Publicar();
        InventarioNube.ReemplazarCon(Instance.Repositorio);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
        try
        {
            var activacion = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activacion.Kind == ExtendedActivationKind.AppNotification)
            {
                AvisoWindowsSolicitudes.Recibir(activacion);
            }
        }
        catch
        {
        }
    }
}
