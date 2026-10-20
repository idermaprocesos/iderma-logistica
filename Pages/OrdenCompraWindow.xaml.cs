using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas.Pages;

public sealed partial class OrdenCompraWindow : Page
{
    private readonly ObservableCollection<LineaOrdenCompra> _lineas = [];
    private Guid _ordenId = Guid.NewGuid();
    private DateTimeOffset? _fechaRegistro;
    private bool _exportando;
    private bool _cargada;

    public OrdenCompraWindow()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (_cargada)
        {
            return;
        }

        IEnumerable<LineaOrdenCompra>? lineas = e.Parameter switch
        {
            OrdenCompraEdicion edicion => PrepararEdicion(edicion),
            IEnumerable<LineaOrdenCompra> nuevas => nuevas,
            _ => null
        };
        if (lineas is null)
        {
            if (Frame?.CanGoBack == true)
            {
                Frame.GoBack();
            }

            return;
        }

        _cargada = true;
        foreach (var linea in lineas)
        {
            Agregar(linea);
        }

        if (e.Parameter is not OrdenCompraEdicion)
        {
            PrepararEncabezado();
        }

        Lista.ItemsSource = _lineas;
        ResumenProductos.Text = _lineas.Count == 1
            ? "1 producto"
            : $"{_lineas.Count} productos";
        ActualizarTotal();
    }

    private void Volver_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private IEnumerable<LineaOrdenCompra> PrepararEdicion(OrdenCompraEdicion edicion)
    {
        _ordenId = edicion.Id == Guid.Empty ? Guid.NewGuid() : edicion.Id;
        _fechaRegistro = edicion.FechaRegistro;
        var documento = edicion.Documento;
        if (documento is null)
        {
            PrepararEncabezado();
            return edicion.Lineas;
        }

        CajaMes.Text = documento.MesAnio;
        CajaCobertura.Text = documento.PeriodoCobertura;
        CajaElaborado.Text = documento.ElaboradoPor;
        CajaRevisado.Text = documento.RevisadoPor;
        if (DateTime.TryParseExact(documento.FechaElaboracion, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
        {
            FechaElaboracion.Date = new DateTimeOffset(fecha);
        }
        else
        {
            FechaElaboracion.Date = DateTimeOffset.Now;
        }

        ComboEstado.ItemsSource = CalculoOrdenCompra.EstadosDocumento;
        var indice = Array.FindIndex(
            CalculoOrdenCompra.EstadosDocumento,
            estado => estado.Equals(documento.Estado, StringComparison.OrdinalIgnoreCase));
        ComboEstado.SelectedIndex = indice >= 0 ? indice : 0;
        return edicion.Lineas;
    }

    private void Agregar(LineaOrdenCompra linea)
    {
        linea.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LineaOrdenCompra.CantidadPedir))
            {
                ActualizarTotal();
            }
        };
        _lineas.Add(linea);
        ResumenProductos.Text = _lineas.Count == 1 ? "1 producto" : $"{_lineas.Count} productos";
        ActualizarTotal();
    }

    private void BuscarInventario_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        var texto = sender.Text?.Trim() ?? string.Empty;
        sender.ItemsSource = texto.Length < 2
            ? null
            : App.Instance.Repositorio.BuscarLimitado(texto, 12)
                .Select(f => $"{f.Codigo}  {f.Nombre}")
                .ToList();
    }

    private void BuscarInventario_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        var texto = args.SelectedItem as string ?? string.Empty;
        var codigo = texto.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var ficha = App.Instance.Repositorio.Todas.FirstOrDefault(f =>
            f.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase));
        if (ficha is null)
        {
            return;
        }

        var existente = _lineas.FirstOrDefault(l => l.FichaId == ficha.Id);
        if (existente is null)
        {
            Agregar(OrdenCompraArmado.DesdeFicha(ficha, 1));
        }
        else
        {
            existente.CantidadPedir += 1;
        }

        sender.Text = string.Empty;
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (_lineas.Count == 0)
        {
            Mostrar("Agregue al menos un producto antes de guardar.", InfoBarSeverity.Warning);
            return;
        }

        if (ComboEstado.SelectedIndex < 0 && ComboEstado.Items.Count > 0)
        {
            ComboEstado.SelectedIndex = 0;
        }

        try
        {
            var documento = Capturar();
            Registrar(documento);
            Mostrar($"La orden quedó guardada como {documento.Estado}.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar("No se pudo guardar: " + ex.Message, InfoBarSeverity.Error);
        }
    }

    private void Mostrar(string mensaje, InfoBarSeverity severidad)
    {
        BarraAviso.Severity = severidad;
        BarraAviso.Message = mensaje;
        BarraAviso.IsOpen = true;
    }

    private void PrepararEncabezado()
    {
        var hoy = DateTime.Today;
        var inicio = new DateTime(hoy.Year, hoy.Month, 1);
        var fin = inicio.AddMonths(1).AddDays(-1);
        CajaMes.Text = hoy.ToString("MM / yyyy");
        CajaCobertura.Text = $"{inicio:dd/MM/yyyy} al {fin:dd/MM/yyyy}";
        FechaElaboracion.Date = new DateTimeOffset(hoy);
        ComboEstado.ItemsSource = CalculoOrdenCompra.EstadosDocumento;
        ComboEstado.SelectedIndex = 0;
    }

    private void ActualizarTotal()
    {
        var grupos = _lineas
            .Where(l => l.TienePrecio)
            .GroupBy(l => MonedaPrecio.Normalizar(l.Moneda))
            .Select(g => MonedaPrecio.Formato(CalculoOrdenCompra.Redondear(g.Sum(l => l.ImporteConIgv)), g.Key))
            .ToList();
        TotalGeneral.Text = grupos.Count == 0
            ? "Total con IGV: sin precios registrados"
            : "Total con IGV: " + string.Join("   ·   ", grupos);
    }

    private async void ExportarPdf_Click(object sender, RoutedEventArgs e) =>
        await ExportarAsync(pdf: true);

    private async void ExportarExcel_Click(object sender, RoutedEventArgs e) =>
        await ExportarAsync(pdf: false);

    private async Task ExportarAsync(bool pdf)
    {
        if (_exportando)
        {
            return;
        }

        _exportando = true;
        try
        {
            var documento = Capturar();
            var resultado = await ExcelUi.ExportarOrdenCompraAsync(documento, pdf);
            if (resultado is null)
            {
                return;
            }

            Registrar(documento);
            BarraAviso.Severity = InfoBarSeverity.Success;
            BarraAviso.Message = pdf
                ? "Se generó el PDF y la orden quedó en Órdenes anteriores."
                : "Se generó el Excel y la orden quedó en Órdenes anteriores.";
            BarraAviso.IsOpen = true;
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            BarraAviso.Severity = InfoBarSeverity.Error;
            BarraAviso.Message = $"No se pudo exportar: {ex.Message}";
            BarraAviso.IsOpen = true;
        }
        finally
        {
            _exportando = false;
        }
    }

    private OrdenCompraDocumento Capturar()
    {
        var elegido = FechaElaboracion.Date;
        var fecha = elegido.Year < 2000 ? DateTime.Today : elegido.LocalDateTime.Date;
        return new OrdenCompraDocumento
        {
            MesAnio = (CajaMes.Text ?? string.Empty).Trim(),
            FechaElaboracion = fecha.ToString("dd/MM/yyyy"),
            ElaboradoPor = (CajaElaborado.Text ?? string.Empty).Trim(),
            RevisadoPor = (CajaRevisado.Text ?? string.Empty).Trim(),
            PeriodoCobertura = (CajaCobertura.Text ?? string.Empty).Trim(),
            Estado = ComboEstado.SelectedItem as string ?? "Borrador",
            Lineas = _lineas.Select(l =>
            {
                l.Ruc = OrdenCompraArmado.RucDe(l.Proveedor);
                return l.Exportar();
            }).ToList()
        };
    }

    private void Registrar(OrdenCompraDocumento documento)
    {
        var fecha = _fechaRegistro ?? DateTimeOffset.Now;
        _fechaRegistro = fecha;
        App.Instance.OrdenesCompra.Guardar(new OrdenCompraRegistrada
        {
            Id = _ordenId,
            FechaRegistro = fecha,
            Documento = documento
        });
        _ = SolicitudesIncidenciasNube.SincronizarEstadoOrdenAsync(_ordenId, documento.Estado);
    }
}
