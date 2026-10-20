using System.Collections.ObjectModel;
using System.Globalization;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IdermaFichas.Pages;

public sealed partial class OrdenServicioWindow : Page
{
    private readonly ObservableCollection<LineaOrdenServicio> _lineas = [];
    private Guid _ordenId = Guid.NewGuid();
    private DateTimeOffset _fechaRegistro = DateTimeOffset.Now;
    private bool _aplicada;

    public OrdenServicioWindow()
    {
        InitializeComponent();
        Lista.ItemsSource = _lineas;
        ComboEstado.ItemsSource = CalculoOrdenServicio.EstadosDocumento;
        ComboEstado.SelectedIndex = 0;
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        _lineas.Clear();
        if (e.Parameter is OrdenServicioEdicion edicion)
        {
            _ordenId = edicion.Id == Guid.Empty ? Guid.NewGuid() : edicion.Id;
            _fechaRegistro = edicion.FechaRegistro == default ? DateTimeOffset.Now : edicion.FechaRegistro;
            _aplicada = edicion.AmpliacionAplicada;
            if (edicion.Documento is { } documento)
            {
                Cargar(documento);
            }
            else
            {
                Nuevo();
            }

            foreach (var linea in edicion.Lineas)
            {
                Agregar(linea);
            }
        }
        else
        {
            Nuevo();
        }

        ActualizarTotal();
    }

    private void Nuevo()
    {
        var hoy = DateTime.Today;
        CajaMes.Text = hoy.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("es-PE"));
        FechaElaboracion.Date = DateTimeOffset.Now;
        CajaCobertura.Text = $"Ampliación desde {hoy:dd/MM/yyyy}";
        ComboEstado.SelectedIndex = 0;
    }

    private void Cargar(OrdenServicioDocumento documento)
    {
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

        var indice = Array.FindIndex(
            CalculoOrdenServicio.EstadosDocumento,
            estado => estado.Equals(documento.Estado, StringComparison.OrdinalIgnoreCase));
        ComboEstado.SelectedIndex = indice >= 0 ? indice : 0;
    }

    private void Agregar(LineaOrdenServicio linea)
    {
        if (linea.ProveedorId != Guid.Empty && _lineas.Any(l => l.ProveedorId == linea.ProveedorId))
        {
            return;
        }

        linea.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(LineaOrdenServicio.Meses) or nameof(LineaOrdenServicio.MontoMensual))
            {
                ActualizarTotal();
            }
        };
        _lineas.Add(linea);
        ResumenLineas.Text = _lineas.Count == 1 ? "1 contrato" : $"{_lineas.Count} contratos";
    }

    private void ActualizarTotal()
    {
        TotalGeneral.Text = $"Total con IGV: {_lineas.Sum(l => l.ImporteConIgv).ToString("C", CultureInfo.GetCultureInfo("es-PE"))}";
        ResumenLineas.Text = _lineas.Count == 1 ? "1 contrato" : $"{_lineas.Count} contratos";
    }

    private void BuscarProveedor_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        var texto = sender.Text.Trim();
        sender.ItemsSource = texto.Length < 2
            ? null
            : App.Instance.Proveedores.Listar()
                .Where(p => p.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase) || p.Ruc.Contains(texto, StringComparison.Ordinal))
                .Take(8)
                .Select(p => p.Nombre)
                .ToList();
    }

    private void BuscarProveedor_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        var nombre = args.SelectedItem as string;
        var proveedor = App.Instance.Proveedores.Listar().FirstOrDefault(p => p.Nombre == nombre);
        if (proveedor is null)
        {
            return;
        }

        Agregar(OrdenesServicioRepository.DesdeProveedor(proveedor));
        ActualizarTotal();
        sender.Text = string.Empty;
    }

    private void Quitar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button boton || boton.Tag is not Guid id)
        {
            return;
        }

        var linea = _lineas.FirstOrDefault(l => l.Id == id);
        if (linea is null)
        {
            return;
        }

        _lineas.Remove(linea);
        ActualizarTotal();
    }

    private void Volver_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private void Guardar_Click(object sender, RoutedEventArgs e) => Registrar(Capturar());

    private async void Pdf_Click(object sender, RoutedEventArgs e)
    {
        var documento = Capturar();
        if (!Registrar(documento))
        {
            return;
        }

        var resultado = await ExcelUi.ExportarOrdenServicioAsync(documento, pdf: true);
        if (resultado is not null)
        {
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
    }

    private async void Excel_Click(object sender, RoutedEventArgs e)
    {
        var documento = Capturar();
        if (!Registrar(documento))
        {
            return;
        }

        var resultado = await ExcelUi.ExportarOrdenServicioAsync(documento, pdf: false);
        if (resultado is not null)
        {
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
    }

    private bool Registrar(OrdenServicioDocumento documento)
    {
        if (_lineas.Count == 0)
        {
            Aviso.Severity = InfoBarSeverity.Warning;
            Aviso.Title = "Sin contratos";
            Aviso.Message = "Agregue al menos un proveedor.";
            Aviso.IsOpen = true;
            return false;
        }

        var orden = new OrdenServicioRegistrada
        {
            Id = _ordenId,
            FechaRegistro = _fechaRegistro,
            AmpliacionAplicada = _aplicada,
            Documento = documento
        };
        if (documento.Estado.Equals("Aprobada", StringComparison.OrdinalIgnoreCase) && !_aplicada)
        {
            orden.AmpliacionAplicada = OrdenesServicioRepository.AplicarAmpliacion(documento) > 0;
            _aplicada = orden.AmpliacionAplicada;
        }

        App.Instance.OrdenesServicio.Guardar(orden);
        Aviso.Severity = InfoBarSeverity.Success;
        Aviso.Title = _aplicada && documento.Estado == "Aprobada"
            ? "Orden aprobada y contratos ampliados"
            : "Orden de servicio guardada";
        Aviso.Message = $"{documento.Estado} · {documento.Lineas.Count} contrato(s).";
        Aviso.IsOpen = true;
        return true;
    }

    private OrdenServicioDocumento Capturar()
    {
        var fecha = FechaElaboracion.Date.DateTime;
        return new OrdenServicioDocumento
        {
            MesAnio = CajaMes.Text.Trim(),
            FechaElaboracion = fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ElaboradoPor = CajaElaborado.Text.Trim(),
            RevisadoPor = CajaRevisado.Text.Trim(),
            PeriodoCobertura = CajaCobertura.Text.Trim(),
            Estado = ComboEstado.SelectedItem as string ?? "Borrador",
            Lineas = _lineas.Select(l =>
            {
                var registro = l.ProveedorId == Guid.Empty ? null : App.Instance.Proveedores.Obtener(l.ProveedorId);
                var ruc = string.IsNullOrWhiteSpace(registro?.Ruc) ? OrdenCompraArmado.RucDe(l.Proveedor) : registro.Ruc.Trim();
                if (ruc.Length > 0)
                {
                    l.Ruc = ruc;
                }

                return l.Exportar();
            }).ToList()
        };
    }
}
