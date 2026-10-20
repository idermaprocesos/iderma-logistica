using System.Globalization;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IdermaFichas.Pages;

public sealed partial class MantenimientoPage : Page
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    public MantenimientoPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ModulosNube.DatosCambiados -= AlLlegarNube;
            ModulosNube.DatosCambiados += AlLlegarNube;
            CargarLista();
        };
        Unloaded += (_, _) => ModulosNube.DatosCambiados -= AlLlegarNube;
    }

    private void AlLlegarNube() => DispatcherQueue.TryEnqueue(CargarLista);

    private void Filtro_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            CargarLista();
        }
    }

    private void CargarLista()
    {
        var filtro = (ComboFiltro.SelectedItem as ComboBoxItem)?.Content as string ?? "Todos";
        var equipos = App.Instance.ControlMantenimiento.Listar();
        var filas = equipos
            .Select(e => new FilaControlEquipo(e))
            .Where(f => filtro switch
            {
                "Preventivo" => f.Clase == "Preventivo",
                "Correctivo" => f.Clase == "Correctivo",
                "Crítico" => f.Clase == "Crítico",
                "Vencidos y por vencer" => f.Estado is "Vencido" or "Por vencer" or "Sin control",
                _ => true
            })
            .ToList();
        Lista.ItemsSource = filas;

        var vencidos = equipos.Count(e => ControlMantenimientoCalculo.Estado(e) is "Vencido" or "Sin control");
        var porVencer = equipos.Count(e => ControlMantenimientoCalculo.Estado(e) == "Por vencer");
        TextoResumen.Text = $"{equipos.Count.ToString(Cultura)} equipos · {vencidos.ToString(Cultura)} vencidos · {porVencer.ToString(Cultura)} por vencer";
    }

    private async void Agregar_Click(object sender, RoutedEventArgs e)
    {
        var equipo = await PedirEquipoAsync(null);
        if (equipo is null)
        {
            return;
        }

        var error = App.Instance.ControlMantenimiento.Agregar(equipo);
        if (error is not null)
        {
            await AvisoDialogoAsync(error);
            return;
        }

        Mostrar("Equipo agregado", equipo.Codigo + " quedó en el control y en Firestore.", InfoBarSeverity.Success);
        CargarLista();
    }

    private async void Editar_Click(object sender, RoutedEventArgs e)
    {
        if (!TryId(sender, out var id))
        {
            return;
        }

        var actual = App.Instance.ControlMantenimiento.Listar().FirstOrDefault(equipo => equipo.Id == id);
        if (actual is null)
        {
            return;
        }

        var editado = await PedirEquipoAsync(actual);
        if (editado is null)
        {
            return;
        }

        var error = App.Instance.ControlMantenimiento.Actualizar(editado);
        if (error is not null)
        {
            await AvisoDialogoAsync(error);
            return;
        }

        Mostrar("Equipo actualizado", "El plazo de " + editado.Codigo + " se publicó en Firestore.", InfoBarSeverity.Success);
        CargarLista();
    }

    private void Registrar_Click(object sender, RoutedEventArgs e)
    {
        if (!TryId(sender, out var equipoId))
        {
            return;
        }

        App.Instance.ControlMantenimiento.RegistrarControl(equipoId);
        Mostrar("Control registrado", "El próximo plazo usa los días de este equipo.", InfoBarSeverity.Success);
        CargarLista();
    }

    private async void Exhaustivo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string codigo } || string.IsNullOrWhiteSpace(codigo))
        {
            return;
        }

        var error = AbrirAppMantenimiento.Abrir(codigo);
        if (error.Length == 0)
        {
            Mostrar("Mantenimiento", "Se abrió el detalle de " + codigo + " en la app de Mantenimiento.", InfoBarSeverity.Success);
            return;
        }

        await AvisoDialogoAsync(error);
    }

    private async Task<EquipoControl?> PedirEquipoAsync(EquipoControl? actual)
    {
        var codigo = new TextBox
        {
            Header = "Código",
            Text = actual?.Codigo ?? string.Empty,
            PlaceholderText = "Si lo dejas vacío, se asigna EQ-…"
        };
        var nombre = new TextBox { Header = "Nombre", Text = actual?.Nombre ?? string.Empty };
        var ubicacion = new TextBox { Header = "Ubicación", Text = actual?.Ubicacion ?? string.Empty };
        var responsable = new TextBox { Header = "Responsable", Text = actual?.Responsable ?? string.Empty };
        var clase = new ComboBox { Header = "Clase", HorizontalAlignment = HorizontalAlignment.Stretch };
        clase.Items.Add("Preventivo");
        clase.Items.Add("Correctivo");
        clase.Items.Add("Crítico");
        clase.SelectedIndex = actual?.Clase switch
        {
            ClaseControlEquipo.Correctivo => 1,
            ClaseControlEquipo.Critico => 2,
            _ => 0
        };
        var sugerido = actual ?? new EquipoControl();
        sugerido.NormalizarPlazo();
        var cada = new NumberBox
        {
            Header = "Cada (días)",
            Minimum = 1,
            Maximum = 3650,
            Value = sugerido.IntervaloDias,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var aviso = new NumberBox
        {
            Header = "Avisar (días antes)",
            Minimum = 0,
            Maximum = 3650,
            Value = sugerido.AvisoDias,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var nota = new TextBox { Header = "Nota", Text = actual?.Nota ?? string.Empty, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };

        var plazos = new Grid { ColumnSpacing = 8 };
        plazos.ColumnDefinitions.Add(new ColumnDefinition());
        plazos.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(aviso, 1);
        plazos.Children.Add(cada);
        plazos.Children.Add(aviso);

        var panel = new StackPanel { Spacing = 8, Width = 440 };
        panel.Children.Add(codigo);
        panel.Children.Add(nombre);
        panel.Children.Add(ubicacion);
        panel.Children.Add(responsable);
        panel.Children.Add(clase);
        panel.Children.Add(plazos);
        panel.Children.Add(nota);

        var dialogo = new ContentDialog
        {
            Title = actual is null ? "Agregar equipo" : "Editar equipo",
            Content = new ScrollViewer { Content = panel, MaxHeight = 520 },
            PrimaryButtonText = "Guardar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        dialogo.PrimaryButtonClick += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(nombre.Text))
            {
                args.Cancel = true;
            }
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        return new EquipoControl
        {
            Id = actual?.Id ?? Guid.NewGuid(),
            Codigo = codigo.Text.Trim(),
            Nombre = nombre.Text.Trim(),
            Ubicacion = ubicacion.Text.Trim(),
            Responsable = responsable.Text.Trim(),
            Clase = clase.SelectedIndex switch
            {
                1 => ClaseControlEquipo.Correctivo,
                2 => ClaseControlEquipo.Critico,
                _ => ClaseControlEquipo.Preventivo
            },
            IntervaloDias = Dias(cada, sugerido.IntervaloDias),
            AvisoDias = Dias(aviso, sugerido.AvisoDias),
            UltimoControl = actual?.UltimoControl,
            Nota = nota.Text.Trim()
        };
    }

    private async Task AvisoDialogoAsync(string mensaje)
    {
        var dialogo = new ContentDialog
        {
            Title = "Mantenimiento",
            Content = mensaje,
            CloseButtonText = "Cerrar",
            XamlRoot = XamlRoot
        };
        await dialogo.ShowAsync();
    }

    private void Mostrar(string titulo, string mensaje, InfoBarSeverity gravedad)
    {
        Aviso.Title = titulo;
        Aviso.Message = mensaje;
        Aviso.Severity = gravedad;
        Aviso.IsOpen = true;
    }

    private static bool TryId(object sender, out Guid id)
    {
        id = Guid.Empty;
        return sender is Button { Tag: string texto } && Guid.TryParse(texto, out id);
    }

    private static int Dias(NumberBox caja, int respaldo)
    {
        if (double.IsNaN(caja.Value) || double.IsInfinity(caja.Value))
        {
            return respaldo;
        }

        return (int)Math.Round(caja.Value);
    }
}

public sealed class FilaControlEquipo
{
    public FilaControlEquipo(EquipoControl equipo)
    {
        Id = equipo.Id.ToString("D");
        Codigo = equipo.Codigo;
        Titulo = string.IsNullOrWhiteSpace(equipo.Codigo) ? equipo.Nombre : equipo.Codigo + " · " + equipo.Nombre;
        var lugar = string.IsNullOrWhiteSpace(equipo.Responsable)
            ? equipo.Ubicacion
            : equipo.Ubicacion + " · " + equipo.Responsable;
        Lugar = string.IsNullOrWhiteSpace(lugar) ? ControlMantenimientoCalculo.TextoClase(equipo.Clase) : lugar;
        Clase = ControlMantenimientoCalculo.TextoClase(equipo.Clase);
        Estado = ControlMantenimientoCalculo.Estado(equipo);
        Plazo = Clase + " · cada " + equipo.IntervaloDias.ToString(CultureInfo.GetCultureInfo("es-PE")) + " d · aviso " + equipo.AvisoDias.ToString(CultureInfo.GetCultureInfo("es-PE"));
        var proximo = ControlMantenimientoCalculo.Proximo(equipo);
        var ultimo = equipo.UltimoControl?.LocalDateTime.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("es-PE")) ?? "sin control";
        Fechas = proximo is null
            ? "Último " + ultimo
            : ultimo + " → " + proximo.Value.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("es-PE"));
    }

    public string Id { get; }
    public string Codigo { get; }
    public string Titulo { get; }
    public string Lugar { get; }
    public string Clase { get; }
    public string Estado { get; }
    public string Plazo { get; }
    public string Fechas { get; }
}
