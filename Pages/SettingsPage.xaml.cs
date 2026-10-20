using Iderma.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace IdermaFichas.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private IReadOnlyList<ListaDesplegable> _listasArea = [];

    private bool _cargandoPreferencias;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        CargarIconosArea();
        ActualizarResumen();
        CargarAreas();
        CargarListasDelArea();
        CargarPreferencias();
        CargarListaCopias();
        ActualizarResumenMedicacion();
        TextoVersionActual.Text =
            $"Versión instalada: {ActualizacionService.TextoVersionInstalada}. Origen: github.com/{ActualizacionService.Repositorio}";
        MostrarInstaladorPendiente();
    }

    private async void ComprobarBase_Click(object sender, RoutedEventArgs e)
    {
        BotonComprobarBase.IsEnabled = false;
        AnilloBase.IsActive = true;
        AnilloBase.Visibility = Visibility.Visible;
        BarraBase.IsOpen = false;
        TextoErrorBase.Visibility = Visibility.Collapsed;
        TextoSolucionBase.Visibility = Visibility.Collapsed;

        try
        {
            var resultado = await DiagnosticoAccesoBase.EjecutarAsync();
            BarraBase.Severity = resultado.Correcto ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            BarraBase.Title = "Base de datos";
            BarraBase.Message = resultado.Resumen;
            BarraBase.IsOpen = true;

            if (!string.IsNullOrWhiteSpace(resultado.Error))
            {
                TextoErrorBase.Text = "Error: " + resultado.Error;
                TextoErrorBase.Visibility = Visibility.Visible;
            }

            if (!string.IsNullOrWhiteSpace(resultado.Solucion))
            {
                TextoSolucionBase.Text = resultado.Correcto
                    ? resultado.Solucion
                    : "Cómo solucionarlo: " + resultado.Solucion;
                TextoSolucionBase.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            BarraBase.Severity = InfoBarSeverity.Error;
            BarraBase.Title = "Base de datos";
            BarraBase.Message = "La comprobación no pudo terminar.";
            BarraBase.IsOpen = true;
            TextoErrorBase.Text = "Error: " + ex.Message;
            TextoErrorBase.Visibility = Visibility.Visible;
            TextoSolucionBase.Text = "Cierra logística, ábrela de nuevo y repite la comprobación. Si el mensaje se repite, compártelo con quien administra Iderma.";
            TextoSolucionBase.Visibility = Visibility.Visible;
        }
        finally
        {
            AnilloBase.IsActive = false;
            AnilloBase.Visibility = Visibility.Collapsed;
            BotonComprobarBase.IsEnabled = true;
        }
    }

    private async void ActualizarPrograma_Click(object sender, RoutedEventArgs e) =>
        await DescargarDesdeGithubAsync(buscarActualizacion: true);

    private async void RestaurarVersionAnterior_Click(object sender, RoutedEventArgs e) =>
        await DescargarDesdeGithubAsync(buscarActualizacion: false);

    private async void InstalarActualizacion_Click(object sender, RoutedEventArgs e)
    {
        var paquete = ActualizacionService.LeerInstaladorPendiente();
        if (paquete is null)
        {
            Mostrar("No hay un instalador descargado. Busque primero la actualización.", InfoBarSeverity.Warning);
            MostrarInstaladorPendiente();
            return;
        }

        var dialogo = new ContentDialog
        {
            Title = paquete.Restaurar ? "Restaurar versión anterior" : "Instalar actualización",
            Content =
                $"Se instalará {paquete.Etiqueta}.\n\n" +
                "Se creará una copia de seguridad y se cerrará el programa para poder actualizar los archivos. " +
                "Al abrir de nuevo se cargarán esos datos.",
            PrimaryButtonText = "Instalar y cerrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        BotonActualizar.IsEnabled = false;
        BotonRestaurar.IsEnabled = false;
        BotonInstalar.IsEnabled = false;
        BarraActualizacion.Visibility = Visibility.Visible;
        TextoProgresoActualizacion.Visibility = Visibility.Visible;
        try
        {
            TextoProgresoActualizacion.Text = "Creando copia de seguridad…";
            BarraActualizacion.Value = 20;
            var origen = paquete.Restaurar ? "antes-de-restaurar" : "antes-de-actualizar";
            var copia = await Task.Run(() => ActualizacionService.PrepararCopiaYMarcar(paquete.Version, origen));
            CargarListaCopias();
            BarraActualizacion.Value = 80;
            TextoProgresoActualizacion.Text = "Cerrando el programa para instalar…";
            if (paquete.Restaurar)
            {
                ActualizacionService.LanzarRestauracionYCerrar(paquete.Ruta);
            }
            else
            {
                ActualizacionService.LanzarInstaladorYCerrar(paquete.Ruta);
            }
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo instalar: {ex.Message}", InfoBarSeverity.Error);
            TextoProgresoActualizacion.Text = ex.Message;
            BotonActualizar.IsEnabled = true;
            BotonRestaurar.IsEnabled = true;
            BotonInstalar.IsEnabled = true;
        }
    }

    private async Task DescargarDesdeGithubAsync(bool buscarActualizacion)
    {
        BotonActualizar.IsEnabled = false;
        BotonRestaurar.IsEnabled = false;
        BotonInstalar.IsEnabled = false;
        BarraActualizacion.Visibility = Visibility.Visible;
        TextoProgresoActualizacion.Visibility = Visibility.Visible;
        BarraActualizacion.Value = 0;
        TextoProgresoActualizacion.Text = "Consultando GitHub Releases…";
        try
        {
            IProgress<(double Porcentaje, string Mensaje)> progreso = new Progress<(double Porcentaje, string Mensaje)>(p =>
            {
                BarraActualizacion.Value = p.Porcentaje;
                TextoProgresoActualizacion.Text = p.Mensaje;
            });

            ReleaseDisponible release;
            string titulo;
            string mensaje;
            string confirmar;
            bool restaurar;
            if (buscarActualizacion)
            {
                release = await ActualizacionService.ConsultarAsync();
                if (!ActualizacionService.HayActualizacion(release))
                {
                    Mostrar(
                        $"Ya tiene la última versión ({ActualizacionService.TextoVersionInstalada}).",
                        InfoBarSeverity.Success);
                    TextoProgresoActualizacion.Text = "No hay una versión más reciente.";
                    BarraActualizacion.Value = 100;
                    return;
                }

                titulo = "Descargar actualización";
                mensaje =
                    $"Hay una versión nueva: {release.Etiqueta}{Tamano(release)}.\n\n" +
                    "Se descargará el instalador. Cuando termine, pulse Instalar: " +
                    "entonces se cerrará el programa y se aplicará la actualización.";
                confirmar = "Descargar";
                restaurar = false;
            }
            else
            {
                release = await ActualizacionService.ConsultarPenultimaAsync();
                if (ActualizacionService.MismaVersion(release))
                {
                    Mostrar(
                        $"Ya está en la versión anterior publicada ({release.Etiqueta}).",
                        InfoBarSeverity.Informational);
                    TextoProgresoActualizacion.Text = "No hay otra versión anterior que instalar.";
                    BarraActualizacion.Value = 100;
                    return;
                }

                titulo = "Descargar versión anterior";
                mensaje =
                    $"Se descargará la penúltima versión publicada: {release.Etiqueta}{Tamano(release)}.\n\n" +
                    "Cuando termine, pulse Instalar. Se cerrará el programa, se desinstalará la versión actual " +
                    "(los datos en AppData se quedan) y se abrirá ese instalador.";
                confirmar = "Descargar";
                restaurar = true;
            }

            var dialogo = new ContentDialog
            {
                Title = titulo,
                Content = mensaje,
                PrimaryButtonText = confirmar,
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };
            if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            {
                TextoProgresoActualizacion.Text = "Descarga cancelada.";
                return;
            }

            var ruta = await ActualizacionService.DescargarInstaladorAsync(release, progreso);
            ActualizacionService.GuardarInstaladorPendiente(ruta, release, restaurar);
            MostrarInstaladorPendiente();
            BarraActualizacion.Value = 100;
            TextoProgresoActualizacion.Text =
                $"Descarga lista ({release.Etiqueta}). Pulse Instalar para cerrar el programa y aplicarla.";
            Mostrar(
                $"Se descargó {release.Etiqueta}. Pulse Instalar cuando quiera cerrar el programa y actualizar.",
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            var verbo = buscarActualizacion ? "descargar la actualización" : "descargar la versión anterior";
            Mostrar($"No se pudo {verbo}: {ex.Message}", InfoBarSeverity.Error);
            TextoProgresoActualizacion.Text = ex.Message;
        }
        finally
        {
            BotonActualizar.IsEnabled = true;
            BotonRestaurar.IsEnabled = true;
            MostrarInstaladorPendiente();
        }
    }

    private void MostrarInstaladorPendiente()
    {
        var paquete = ActualizacionService.LeerInstaladorPendiente();
        if (paquete is null)
        {
            BotonInstalar.Visibility = Visibility.Collapsed;
            return;
        }

        BotonInstalar.Visibility = Visibility.Visible;
        BotonInstalar.IsEnabled = true;
        BotonInstalar.Content = $"Instalar {paquete.Etiqueta}";
        BarraActualizacion.Visibility = Visibility.Visible;
        TextoProgresoActualizacion.Visibility = Visibility.Visible;
        BarraActualizacion.Value = 100;
        if (string.IsNullOrWhiteSpace(TextoProgresoActualizacion.Text) ||
            TextoProgresoActualizacion.Visibility != Visibility.Visible)
        {
            TextoProgresoActualizacion.Text =
                $"Hay un instalador listo ({paquete.Etiqueta}). Pulse Instalar para cerrar el programa y aplicarlo.";
        }
    }

    private static string Tamano(ReleaseDisponible release) =>
        release.TamanoBytes > 0 ? $" ({release.TamanoBytes / 1_048_576d:0.0} MB)" : string.Empty;

    public void RecargarAreas() => CargarAreas();

    public void RecargarDesplegables() => CargarListasDelArea();

    private void CargarAreas(Guid? seleccionar = null)
    {
        var areaLista = seleccionar
            ?? (ListaAreas.SelectedItem as AreaDefinicion)?.Id
            ?? (ComboAreaListas.SelectedItem as AreaDefinicion)?.Id;
        var areas = AreasOperativas.Todas.ToList();
        ListaAreas.ItemsSource = areas;
        ComboAreaListas.ItemsSource = areas;
        if (areaLista is Guid id)
        {
            var coincidencia = areas.FirstOrDefault(a => a.Id == id);
            if (coincidencia is not null)
            {
                ListaAreas.SelectedItem = coincidencia;
                ComboAreaListas.SelectedItem = coincidencia;
                return;
            }
        }

        if (areas.Count > 0 && ComboAreaListas.SelectedIndex < 0)
        {
            ComboAreaListas.SelectedIndex = 0;
        }
    }

    private Guid? AreaActual =>
        ComboAreaListas.SelectedItem is AreaDefinicion area ? area.Id : AreasOperativas.Todas.FirstOrDefault()?.Id;

    private void ComboAreaListas_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        CargarListasDelArea();

    private void ListaAreas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListaAreas.SelectedItem is not AreaDefinicion area)
        {
            return;
        }

        CajaNombreArea.Text = area.Nombre;
        CajaDescripcionArea.Text = area.Descripcion;
        CajaPrefijoArea.Text = area.Prefijo;
        ComboPlantillaArea.SelectedIndex = area.Plantilla switch
        {
            AreaOperativa.Oficina => 1,
            AreaOperativa.Limpieza => 2,
            _ => 0
        };
        SeleccionarIcono(area.Glifo);
    }

    private void CargarIconosArea()
    {
        CuadriculaIconoArea.ItemsSource = IconosArea.Opciones;
        if (CuadriculaIconoArea.SelectedIndex < 0)
        {
            CuadriculaIconoArea.SelectedIndex = 0;
        }
    }

    private void SeleccionarIcono(string glifo)
    {
        var indice = IconosArea.IndiceDe(IconosArea.Opciones, glifo);
        if (indice >= 0)
        {
            CuadriculaIconoArea.ItemsSource = IconosArea.Opciones;
            CuadriculaIconoArea.SelectedIndex = indice;
            return;
        }

        var extra = IconosArea.Opciones.ToList();
        extra.Insert(0, new IconoAreaOpcion("Icono actual", glifo));
        CuadriculaIconoArea.ItemsSource = extra;
        CuadriculaIconoArea.SelectedIndex = 0;
    }

    private string GlifoElegido() =>
        CuadriculaIconoArea.SelectedItem is IconoAreaOpcion opcion
            ? opcion.Glifo
            : "\uE8F1";

    private AreaOperativa PlantillaElegida() =>
        ComboPlantillaArea.SelectedItem is ComboBoxItem { Tag: string etiqueta } &&
        Enum.TryParse<AreaOperativa>(etiqueta, out var plantilla)
            ? plantilla
            : AreaOperativa.Clinica;

    private void AgregarArea_Click(object sender, RoutedEventArgs e)
    {
        var nombre = CajaNombreArea.Text?.Trim() ?? string.Empty;
        if (nombre.Length == 0)
        {
            Mostrar("Escriba el nombre de la nueva área.", InfoBarSeverity.Warning);
            return;
        }

        var creada = AreasOperativas.Agregar(nombre, CajaDescripcionArea.Text ?? string.Empty, CajaPrefijoArea.Text ?? string.Empty, PlantillaElegida(), GlifoElegido());
        RefrescarAreasEnApp(creada.Id);
        Mostrar($"Se agregó el área «{nombre}» (código {creada.Prefijo}_001) al menú lateral.", InfoBarSeverity.Success);
    }

    private void GuardarArea_Click(object sender, RoutedEventArgs e)
    {
        if (ListaAreas.SelectedItem is not AreaDefinicion area)
        {
            Mostrar("Seleccione un área de la lista para cambiarle el nombre.", InfoBarSeverity.Warning);
            return;
        }

        var nombre = CajaNombreArea.Text?.Trim() ?? string.Empty;
        if (nombre.Length == 0)
        {
            Mostrar("El nombre del área no puede quedar vacío.", InfoBarSeverity.Warning);
            return;
        }

        AreasOperativas.Actualizar(area.Id, nombre, CajaDescripcionArea.Text ?? string.Empty, CajaPrefijoArea.Text ?? string.Empty, PlantillaElegida(), GlifoElegido());
        var guardada = AreasOperativas.Obtener(area.Id);
        App.Instance.Repositorio.ActualizarIndiceBusqueda();
        RefrescarAreasEnApp(area.Id);
        var prefijo = guardada?.Prefijo ?? string.Empty;
        Mostrar($"Se guardó «{nombre}» con prefijo de código {prefijo}. Las fichas nuevas usarán {prefijo}_001, {prefijo}_002…", InfoBarSeverity.Success);
    }

    private async void BorrarArea_Click(object sender, RoutedEventArgs e)
    {
        if (ListaAreas.SelectedItem is not AreaDefinicion area)
        {
            Mostrar("Seleccione un área para borrarla.", InfoBarSeverity.Warning);
            return;
        }

        var productos = App.Instance.Repositorio.Contar(area.Id);
        var dialogo = new ContentDialog
        {
            Title = "Borrar área",
            Content = productos == 0
                ? $"Se eliminará «{area.Nombre}» del menú."
                : $"Se eliminará «{area.Nombre}». {productos} producto(s) quedarán sin área y seguirán en el catálogo.",
            PrimaryButtonText = "Borrar área",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var nombreBorrado = area.Nombre;
        App.Instance.Repositorio.ReasignarArea(area.Id, null);
        AreasOperativas.Eliminar(area.Id);
        RefrescarAreasEnApp();
        Mostrar($"Se borró «{nombreBorrado}». Los productos de esa lista quedaron sin área.", InfoBarSeverity.Success);
    }

    private void SubirArea_Click(object sender, RoutedEventArgs e) => MoverAreaSeleccionada(-1);

    private void BajarArea_Click(object sender, RoutedEventArgs e) => MoverAreaSeleccionada(1);

    private void MoverAreaSeleccionada(int desplazamiento)
    {
        if (ListaAreas.SelectedItem is not AreaDefinicion area)
        {
            Mostrar("Seleccione un área para cambiar su orden.", InfoBarSeverity.Warning);
            return;
        }

        if (!AreasOperativas.Mover(area.Id, desplazamiento))
        {
            Mostrar(desplazamiento < 0
                ? "Esa área ya está al inicio de la lista."
                : "Esa área ya está al final de la lista.", InfoBarSeverity.Informational);
            return;
        }

        RefrescarAreasEnApp(area.Id);
        Mostrar($"El orden del menú ahora pone «{area.Nombre}» en la posición {AreasOperativas.Obtener(area.Id)?.Orden}.", InfoBarSeverity.Success);
    }

    private void RefrescarAreasEnApp(Guid? seleccionar = null)
    {
        CargarAreas(seleccionar);
        CargarListasDelArea();
        ActualizarResumen();
        App.Instance.MainAppWindow?.ActualizarMenuAreas();
    }

    private void CargarPreferencias()
    {
        _cargandoPreferencias = true;
        var preferencias = App.Instance.Preferencias.Actual;
        ToggleAutoguardado.IsOn = preferencias.AutoguardadoActivo;
        ComboIntervalo.SelectedIndex = preferencias.IntervaloAutoguardadoMinutos switch
        {
            5 => 0,
            20 => 2,
            30 => 3,
            _ => 1
        };
        ComboIntervalo.IsEnabled = preferencias.AutoguardadoActivo;
        ToggleCopias.IsOn = preferencias.CopiasActivas;
        ToggleImagenesCopia.IsOn = preferencias.CopiasIncluyenImagenes;
        ComboIntervaloCopia.SelectedIndex = preferencias.IntervaloCopiasHoras switch
        {
            1 => 0,
            12 => 2,
            24 => 3,
            _ => 1
        };
        ComboMaximoCopias.SelectedIndex = preferencias.MaximoCopias switch
        {
            5 => 0,
            10 => 1,
            40 => 3,
            _ => 2
        };
        ComboIntervaloCopia.IsEnabled = preferencias.CopiasActivas;
        ComboMaximoCopias.IsEnabled = preferencias.CopiasActivas;
        ToggleImagenesCopia.IsEnabled = preferencias.CopiasActivas;
        CargarListaCopias();
        _cargandoPreferencias = false;
    }

    private void Copias_Cambiado(object sender, RoutedEventArgs e)
    {
        if (_cargandoPreferencias)
        {
            return;
        }

        var preferencias = App.Instance.Preferencias.Actual;
        preferencias.CopiasActivas = ToggleCopias.IsOn;
        preferencias.CopiasIncluyenImagenes = ToggleImagenesCopia.IsOn;
        ComboIntervaloCopia.IsEnabled = preferencias.CopiasActivas;
        ComboMaximoCopias.IsEnabled = preferencias.CopiasActivas;
        ToggleImagenesCopia.IsEnabled = preferencias.CopiasActivas;
        if (ComboIntervaloCopia.SelectedItem is ComboBoxItem { Tag: string horasTexto }
            && int.TryParse(horasTexto, out var horas))
        {
            preferencias.IntervaloCopiasHoras = horas;
        }

        if (ComboMaximoCopias.SelectedItem is ComboBoxItem { Tag: string maxTexto }
            && int.TryParse(maxTexto, out var maximo))
        {
            preferencias.MaximoCopias = maximo;
        }

        App.Instance.Preferencias.Guardar();
        App.Instance.Autoguardado.AplicarPreferencias();
        var estado = preferencias.CopiasActivas
            ? $"Copias automáticas cada {preferencias.IntervaloCopiasHoras} hora(s). Se conservan {preferencias.MaximoCopias}."
            : "Copias automáticas desactivadas. Puede crear una manualmente.";
        Mostrar(estado, InfoBarSeverity.Success);
    }

    private void CargarListaCopias()
    {
        var copias = App.Instance.Copias.Listar();
        ListaCopias.ItemsSource = copias;
        var ultima = App.Instance.Preferencias.Actual.UltimaCopiaUtc;
        TextoUltimaCopia.Text = ultima is DateTimeOffset fecha
            ? $"Última copia: {fecha.ToLocalTime():dd/MM/yyyy HH:mm}. Carpeta: {App.Instance.Copias.CarpetaRaiz}"
            : $"Aún no hay copias automáticas. Carpeta: {App.Instance.Copias.CarpetaRaiz}";
    }

    private void CrearCopia_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var copia = App.Instance.Copias.Crear("manual");
            CargarListaCopias();
            ActualizarResumen();
            Mostrar($"Copia creada ({copia.Titulo}): {copia.Fichas} ficha(s), {copia.Movimientos} movimiento(s) y {copia.Areas} área(s).", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo crear la copia: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void RestaurarCopia_Click(object sender, RoutedEventArgs e)
    {
        if (ListaCopias.SelectedItem is not CopiaSeguridadInfo copia)
        {
            Mostrar("Seleccione una copia de la lista para restaurarla.", InfoBarSeverity.Warning);
            return;
        }

        var dialogo = new ContentDialog
        {
            Title = "Restaurar copia de seguridad",
            Content = $"Se reemplazarán fichas, características, áreas, desplegables, RR. HH., el registro de ingresos y salidas y los recibos por la copia del {copia.Titulo}. Antes se crea otra copia del estado actual.",
            PrimaryButtonText = "Restaurar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            App.Instance.Copias.Crear("antes-de-restaurar");
            App.Instance.Copias.Restaurar(copia.Carpeta);
            CargarAreas();
            CargarListasDelArea();
            CargarListaCopias();
            ActualizarResumen();
            Mostrar($"Se restauró la copia del {copia.Titulo}.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo restaurar: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void AbrirCarpetaCopias_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(App.Instance.Copias.CarpetaRaiz);
        await Windows.System.Launcher.LaunchFolderPathAsync(App.Instance.Copias.CarpetaRaiz);
    }

    private void Autoguardado_Cambiado(object sender, RoutedEventArgs e)
    {
        if (_cargandoPreferencias)
        {
            return;
        }

        var preferencias = App.Instance.Preferencias.Actual;
        preferencias.AutoguardadoActivo = ToggleAutoguardado.IsOn;
        ComboIntervalo.IsEnabled = preferencias.AutoguardadoActivo;
        if (ComboIntervalo.SelectedItem is ComboBoxItem { Tag: string etiqueta }
            && int.TryParse(etiqueta, out var minutos))
        {
            preferencias.IntervaloAutoguardadoMinutos = minutos;
        }

        App.Instance.Preferencias.Guardar();
        App.Instance.Autoguardado.AplicarPreferencias();
        var estado = preferencias.AutoguardadoActivo
            ? $"Autoguardado cada {preferencias.IntervaloAutoguardadoMinutos} minutos."
            : "Autoguardado desactivado.";
        Mostrar(estado, InfoBarSeverity.Success);
    }

    private ListaDesplegable? ListaActual =>
        ComboListas.SelectedIndex >= 0 && ComboListas.SelectedIndex < _listasArea.Count
            ? _listasArea[ComboListas.SelectedIndex]
            : null;

    private void CargarListasDelArea()
    {
        if (ComboListas is null)
        {
            return;
        }

        _listasArea = Catalogos.ListasDe(AreaActual);
        ComboListas.ItemsSource = _listasArea.Select(l => l.Titulo).ToList();
        ComboListas.SelectedIndex = _listasArea.Count > 0 ? 0 : -1;
        CargarValores();
    }

    private void ActualizarResumen()
    {
        var repo = App.Instance.Repositorio;
        RutaDatos.Text = repo.RutaArchivo;
        ResumenDatos.Text = $"{repo.Todas.Count} fichas en catálogo.";
        RutaCopias.Text = $"Copias de seguridad: {App.Instance.Copias.CarpetaRaiz}";
    }

    private void ComboListas_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        CargarValores();

    private List<string> ValoresEditables()
    {
        if (ListaActual is null)
        {
            return [];
        }

        var propia = Catalogos.Copia(ListaActual.Clave);
        if (propia.Count > 0)
        {
            return propia;
        }

        var separador = ListaActual.Clave.IndexOf('_');
        if (separador < 0)
        {
            return propia;
        }

        return Catalogos.Obtener(AreaActual, ListaActual.Clave[(separador + 1)..]).ToList();
    }

    private void CargarValores()
    {
        if (ListaActual is null)
        {
            return;
        }

        ListaValores.ItemsSource = ValoresEditables();
        CajaValor.Text = string.Empty;
    }

    private void ListaValores_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListaValores.SelectedItem is string valor)
        {
            CajaValor.Text = valor;
        }
    }

    private void AgregarValor_Click(object sender, RoutedEventArgs e)
    {
        if (ListaActual is null)
        {
            return;
        }

        var valor = CajaValor.Text.Trim();
        if (valor.Length == 0)
        {
            Mostrar("Escriba un valor para agregarlo.", InfoBarSeverity.Warning);
            return;
        }

        var lista = ValoresEditables();
        if (lista.Any(v => v.Equals(valor, StringComparison.CurrentCultureIgnoreCase)))
        {
            Mostrar("Ese valor ya existe en la lista.", InfoBarSeverity.Warning);
            return;
        }

        lista.Add(valor);
        Catalogos.Reemplazar(ListaActual.Clave, lista);
        CargarValores();
        Mostrar("Valor agregado al desplegable.", InfoBarSeverity.Success);
    }

    private void EditarValor_Click(object sender, RoutedEventArgs e)
    {
        if (ListaActual is null || ListaValores.SelectedItem is not string anterior)
        {
            Mostrar("Seleccione un valor de la lista para editarlo.", InfoBarSeverity.Warning);
            return;
        }

        var nuevo = CajaValor.Text.Trim();
        if (nuevo.Length == 0)
        {
            Mostrar("El valor no puede quedar vacío.", InfoBarSeverity.Warning);
            return;
        }

        var lista = ValoresEditables();
        var indice = lista.FindIndex(v => v.Equals(anterior, StringComparison.CurrentCultureIgnoreCase));
        if (indice < 0)
        {
            return;
        }

        lista[indice] = nuevo;
        Catalogos.Reemplazar(ListaActual.Clave, lista);
        CargarValores();
        Mostrar("Valor actualizado.", InfoBarSeverity.Success);
    }

    private void BorrarValor_Click(object sender, RoutedEventArgs e)
    {
        if (ListaActual is null || ListaValores.SelectedItem is not string valor)
        {
            Mostrar("Seleccione un valor para borrarlo.", InfoBarSeverity.Warning);
            return;
        }

        var lista = ValoresEditables();
        lista.RemoveAll(v => v.Equals(valor, StringComparison.CurrentCultureIgnoreCase));
        Catalogos.Reemplazar(ListaActual.Clave, lista);
        CargarValores();
        Mostrar("Valor eliminado del desplegable.", InfoBarSeverity.Success);
    }

    private void RestaurarLista_Click(object sender, RoutedEventArgs e)
    {
        if (ListaActual is null)
        {
            return;
        }

        Catalogos.Restaurar(ListaActual.Clave);
        CargarValores();
        Mostrar($"Se restauró la lista «{ListaActual.Titulo}».", InfoBarSeverity.Success);
    }

    private void SeleccionarTodoExportar_Click(object sender, RoutedEventArgs e) =>
        MarcarExportacion(true);

    private void QuitarTodoExportar_Click(object sender, RoutedEventArgs e) =>
        MarcarExportacion(false);

    private void MarcarExportacion(bool marcado)
    {
        CheckExportarFichas.IsChecked = marcado;
        CheckExportarCaracteristicas.IsChecked = marcado;
        CheckExportarAreas.IsChecked = marcado;
        CheckExportarDesplegables.IsChecked = marcado;
        CheckExportarRh.IsChecked = marcado;
        CheckExportarRegistro.IsChecked = marcado;
        CheckExportarRecibos.IsChecked = marcado;
        CheckExportarFotos.IsChecked = marcado;
        CheckExportarPreferencias.IsChecked = marcado;
    }

    private async void ExportarSeleccion_Click(object sender, RoutedEventArgs e)
    {
        var opciones = new ExportacionOpciones
        {
            Fichas = CheckExportarFichas.IsChecked == true,
            Caracteristicas = CheckExportarCaracteristicas.IsChecked == true,
            Areas = CheckExportarAreas.IsChecked == true,
            Desplegables = CheckExportarDesplegables.IsChecked == true,
            RecursosHumanos = CheckExportarRh.IsChecked == true,
            Registro = CheckExportarRegistro.IsChecked == true,
            Recibos = CheckExportarRecibos.IsChecked == true,
            Fotos = CheckExportarFotos.IsChecked == true,
            Preferencias = CheckExportarPreferencias.IsChecked == true
        };
        if (!opciones.HaySeleccion)
        {
            Mostrar("Marque al menos un tipo de información, o pulse Seleccionar todo.", InfoBarSeverity.Warning);
            return;
        }

        var selector = new FileSavePicker();
        VentanaHelper.AsociarSelector(selector);
        VentanaHelper.ConfigurarInicio(selector);
        selector.FileTypeChoices.Add("Archivo ZIP", [".zip"]);
        selector.DefaultFileExtension = ".zip";
        selector.SuggestedFileName = $"iderma-exportacion-{DateTime.Now:yyyyMMdd-HHmm}";

        var archivo = await selector.PickSaveFileAsync();
        if (archivo is null)
        {
            return;
        }

        var temporal = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip");
        try
        {
            App.Instance.Repositorio.Guardar();
            App.Instance.Rh.Guardar();
            App.Instance.PersonalMedico.Guardar();
            App.Instance.Preferencias.Guardar();
            App.Instance.HistorialPrecios.Guardar();
            await Task.Run(() => ExportacionDatosService.CrearZip(temporal, opciones));
            await ExcelUi.CopiarArchivoAsync(temporal, archivo);

            await ExcelUi.OfrecerAbrirAsync(XamlRoot, new ResultadoExportacion(archivo, ".zip"));
            Mostrar("Se exportó la información seleccionada.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo exportar: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            if (File.Exists(temporal))
            {
                try
                {
                    File.Delete(temporal);
                }
                catch
                {
                }
            }
        }
    }

    private async void Exportar_Click(object sender, RoutedEventArgs e)
    {
        var selector = new FileSavePicker();
        VentanaHelper.AsociarSelector(selector);
        VentanaHelper.ConfigurarInicio(selector);
        selector.FileTypeChoices.Add("JSON", [".json"]);
        selector.DefaultFileExtension = ".json";
        selector.SuggestedFileName = "fichas-tecnicas-iderma";

        var archivo = await selector.PickSaveFileAsync();
        if (archivo is null)
        {
            return;
        }

        await FileIO.WriteTextAsync(archivo, App.Instance.Repositorio.ExportarJson());
        Mostrar("Inventario exportado.", InfoBarSeverity.Success);
    }

    private async void Importar_Click(object sender, RoutedEventArgs e)
    {
        var selector = new FileOpenPicker();
        VentanaHelper.AsociarSelector(selector);
        VentanaHelper.ConfigurarInicio(selector);
        selector.FileTypeFilter.Add(".json");

        var archivo = await selector.PickSingleFileAsync();
        if (archivo is null)
        {
            return;
        }

        try
        {
            var json = await FileIO.ReadTextAsync(archivo);
            var cantidad = App.Instance.Repositorio.ImportarJson(json);
            ActualizarResumen();
            Mostrar($"Se importaron {cantidad} fichas.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo importar el archivo: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void ImportarExcel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var resultado = await ExcelUi.ImportarAsync();
            if (resultado is null)
            {
                return;
            }

            ActualizarResumen();
            Mostrar(resultado.Mensaje, InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo importar el Excel: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void PlantillaExcel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (await ExcelUi.GuardarPlantillaAsync())
            {
                Mostrar("Se guardó la plantilla con los productos del inventario.", InfoBarSeverity.Success);
            }
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo descargar la plantilla: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void Restaurar_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new ContentDialog
        {
            Title = "Restaurar ejemplos",
            Content = "Se reemplazará el catálogo actual por las fichas de ejemplo de Iderma Capilar.",
            PrimaryButtonText = "Restaurar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        App.Instance.Repositorio.RestaurarEjemplos();
        ActualizarResumen();
        Mostrar("Se restauraron las fichas de ejemplo.", InfoBarSeverity.Success);
    }

    private async void BorrarBase_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new ContentDialog
        {
            Title = "Borrar base de datos",
            Content = "Se vaciará el catálogo de fichas. Antes se crea una copia completa (fichas, áreas, desplegables y RR. HH.).",
            PrimaryButtonText = "Borrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var copia = App.Instance.Repositorio.VaciarConCopiaSeguridad();
        CargarListaCopias();
        ActualizarResumen();
        Mostrar($"Base vaciada. Copia: {Path.GetFileName(copia)}", InfoBarSeverity.Success);
    }

    private void ActualizarResumenMedicacion()
    {
        var n = App.Instance.PersonalMedico.MedicacionesOrdenadas().Count;
        ResumenMedicacionComun.Text = n == 0
            ? "Aún no hay medicación común. Descargue la plantilla e impórtela."
            : n == 1
                ? "1 medicamento común listo para sugerencias en Recetas."
                : $"{n} medicamentos comunes listos para sugerencias en Recetas.";
    }

    private async void PlantillaMedicacion_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var resultado = await ExcelUi.GuardarExcelAsync(
                "plantilla-medicacion-comun",
                ExcelMedicacionComunService.CrearPlantilla);
            if (resultado is null)
            {
                return;
            }

            Mostrar("Se guardó la plantilla de medicación común.", InfoBarSeverity.Success);
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo descargar la plantilla: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void ImportarMedicacion_Click(object sender, RoutedEventArgs e)
    {
        var temporal = await ExcelUi.ElegirExcelTemporalAsync();
        if (temporal is null)
        {
            return;
        }

        try
        {
            var filas = ExcelMedicacionComunService.Leer(temporal);
            var n = App.Instance.PersonalMedico.ImportarMedicaciones(filas);
            ActualizarResumenMedicacion();
            Mostrar(
                n == 0
                    ? "No se encontraron medicamentos en el Excel."
                    : $"Se cargaron {n} medicamentos comunes. En Recetas aparecerán al escribir el nombre.",
                n == 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar("No se pudo leer el Excel: " + ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            if (File.Exists(temporal))
            {
                File.Delete(temporal);
            }
        }
    }

    private void IniciarTourTrabajador_Click(object sender, RoutedEventArgs e)
    {
        App.Instance.MainAppWindow?.IniciarTourTrabajadorNuevo();
    }

    private void Mostrar(string mensaje, InfoBarSeverity severidad)
    {
        BarraEstado.Severity = severidad;
        BarraEstado.Message = mensaje;
        BarraEstado.IsOpen = true;
    }
}
