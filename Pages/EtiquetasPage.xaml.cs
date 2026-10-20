using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Windows.Storage;
using Windows.Storage.Streams;

namespace IdermaFichas.Pages;

public sealed partial class EtiquetasPage : Page
{
    private static readonly (string Texto, TipoSimboloEtiqueta Tipo)[] Tipos =
    [
        ("Código QR", TipoSimboloEtiqueta.Qr),
        ("Código de barras (Code 128)", TipoSimboloEtiqueta.Code128),
        ("Code 39", TipoSimboloEtiqueta.Code39),
        ("EAN-13", TipoSimboloEtiqueta.Ean13),
        ("Data Matrix", TipoSimboloEtiqueta.DataMatrix),
        ("PDF417", TipoSimboloEtiqueta.Pdf417),
        ("Aztec", TipoSimboloEtiqueta.Aztec)
    ];

    private static readonly (string Texto, TamanoHojaEtiqueta Hoja)[] Hojas =
    [
        ("A4 (predeterminada)", TamanoHojaEtiqueta.A4),
        ("Carta", TamanoHojaEtiqueta.Carta),
        ("Oficio / Legal", TamanoHojaEtiqueta.Oficio),
        ("A5", TamanoHojaEtiqueta.A5)
    ];

    private static readonly (string Texto, DisposicionEtiqueta Disposicion)[] Disposiciones =
    [
        ("Vertical (código arriba)", DisposicionEtiqueta.Vertical),
        ("Horizontal (código a la izquierda)", DisposicionEtiqueta.Horizontal)
    ];

    private IReadOnlyList<FilaEtiqueta> _filas = [];
    private TipoSimboloEtiqueta? _previewTipo;

    public EtiquetasPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ComboTipo.ItemsSource = Tipos.Select(t => t.Texto).ToList();
        ComboTipo.SelectedIndex = 0;
        ComboHoja.ItemsSource = Hojas.Select(h => h.Texto).ToList();
        ComboHoja.SelectedIndex = 0;
        ComboDisposicion.ItemsSource = Disposiciones.Select(d => d.Texto).ToList();
        ComboDisposicion.SelectedIndex = 0;
        ActualizarResumen();
    }

    private OpcionesEtiqueta OpcionesActuales()
    {
        var tipo = ComboTipo.SelectedIndex >= 0 && ComboTipo.SelectedIndex < Tipos.Length
            ? Tipos[ComboTipo.SelectedIndex].Tipo
            : TipoSimboloEtiqueta.Qr;
        var hoja = ComboHoja.SelectedIndex >= 0 && ComboHoja.SelectedIndex < Hojas.Length
            ? Hojas[ComboHoja.SelectedIndex].Hoja
            : TamanoHojaEtiqueta.A4;

        var disposicion = ComboDisposicion is { SelectedIndex: >= 0 } && ComboDisposicion.SelectedIndex < Disposiciones.Length
            ? Disposiciones[ComboDisposicion.SelectedIndex].Disposicion
            : DisposicionEtiqueta.Vertical;

        return new OpcionesEtiqueta
        {
            Tipo = tipo,
            Hoja = hoja,
            Columnas = (int)Math.Clamp(double.IsNaN(CajaColumnas.Value) ? 3 : CajaColumnas.Value, 1, 8),
            Filas = (int)Math.Clamp(double.IsNaN(CajaFilas.Value) ? 8 : CajaFilas.Value, 1, 16),
            TamanoMm = (float)Math.Clamp(SliderTamano.Value, 10, 70),
            Disposicion = disposicion,
            GuiaCorte = ToggleGuiaCorte?.IsOn == true
        };
    }

    private void Opciones_Changed(object sender, SelectionChangedEventArgs e) => ActualizarResumen();

    private void GuiaCorte_Toggled(object sender, RoutedEventArgs e) => ActualizarResumen();

    private void Number_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) => ActualizarResumen();

    private void SliderTamano_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        TextoTamano.Text = $"Tamaño del código: {SliderTamano.Value:0} mm";
        ActualizarResumen();
    }

    private void ActualizarResumen()
    {
        if (ResumenOpciones is null || ComboTipo is null || ImagenPreview is null)
        {
            return;
        }

        var opciones = OpcionesActuales();
        var etiquetas = _filas.Sum(f => f.Cantidad);
        var omitidas = _filas.Count(f => f.Cantidad == 0);
        var porPagina = opciones.Columnas * opciones.Filas;
        var paginas = etiquetas == 0 ? 0 : (int)Math.Ceiling(etiquetas / (double)porPagina);

        var disposicion = opciones.Disposicion == DisposicionEtiqueta.Horizontal
            ? "horizontal"
            : "vertical";
        ResumenOpciones.Text =
            $"{SimboloEtiquetaService.Titulo(opciones.Tipo)} · {opciones.Columnas} × {opciones.Filas} por hoja {Hojas.First(h => h.Hoja == opciones.Hoja).Texto} · " +
            $"código de {opciones.TamanoMm:0} mm · {disposicion}" +
            (opciones.GuiaCorte ? " · con guía de corte" : string.Empty) +
            (etiquetas > 0 ? $" · se imprimirán {etiquetas} etiqueta(s) (la cantidad de cada fila) en {paginas} página(s)" : string.Empty) +
            (omitidas > 0 ? $" · {omitidas} fila(s) con cantidad 0 no se imprimen" : string.Empty);

        _ = ActualizarPreviewAsync(opciones);
    }

    private async Task ActualizarPreviewAsync(OpcionesEtiqueta opciones)
    {
        var muestra = _filas.FirstOrDefault(f => f.Cantidad > 0);
        var codigo = muestra?.Codigo ?? "CDB_002";
        var producto = muestra?.Producto ?? "MONALISA (HARD TYPE) FUCSIA";
        if (opciones.Tipo == TipoSimboloEtiqueta.Ean13 && codigo.Count(char.IsDigit) is not (12 or 13))
        {
            codigo = "750123456789";
        }

        PreviewFormato.Text = SimboloEtiquetaService.Titulo(opciones.Tipo);
        PreviewCodigo.Text = codigo;
        PreviewProducto.Text = producto;
        AplicarDisposicionPreview(opciones);
        if (GuiaPreview is not null)
        {
            GuiaPreview.Visibility = opciones.GuiaCorte ? Visibility.Visible : Visibility.Collapsed;
        }

        var copias = muestra?.Cantidad ?? 0;
        PreviewCuadricula.Text = copias > 0
            ? $"Este producto se imprimirá {copias} vez/veces, como indica su fila. La hoja se llena {opciones.Columnas} × {opciones.Filas}."
            : $"Así se verá cada etiqueta. La cantidad de copias la define la columna CANTIDAD de cada fila, no la lista sola.";

        var lado = Math.Clamp(opciones.TamanoMm * 3.6, 72, 220);
        if (SimboloEtiquetaService.EsBidimensional(opciones.Tipo))
        {
            ImagenPreview.Width = lado;
            ImagenPreview.Height = lado;
        }
        else
        {
            ImagenPreview.Width = Math.Min(280, lado * 2.6);
            ImagenPreview.Height = Math.Max(44, lado * 0.55);
        }

        if (_previewTipo == opciones.Tipo && ImagenPreview.Source is not null)
        {
            return;
        }

        try
        {
            var png = SimboloEtiquetaService.Generar(codigo, opciones.Tipo, Math.Max(24, opciones.TamanoMm));
            using var flujo = new InMemoryRandomAccessStream();
            await flujo.WriteAsync(png.AsBuffer());
            flujo.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(flujo);
            ImagenPreview.Source = bitmap;
            _previewTipo = opciones.Tipo;
        }
        catch
        {
            var png = SimboloEtiquetaService.Generar(codigo, TipoSimboloEtiqueta.Code128, 24);
            using var flujo = new InMemoryRandomAccessStream();
            await flujo.WriteAsync(png.AsBuffer());
            flujo.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(flujo);
            ImagenPreview.Source = bitmap;
            _previewTipo = opciones.Tipo;
        }
    }

    private void AplicarDisposicionPreview(OpcionesEtiqueta opciones)
    {
        if (ImagenPreview is null || PreviewCodigo is null || PreviewProducto is null)
        {
            return;
        }

        var horizontal = opciones.Disposicion == DisposicionEtiqueta.Horizontal;
        if (horizontal)
        {
            Grid.SetRow(ImagenPreview, 0);
            Grid.SetRowSpan(ImagenPreview, 2);
            Grid.SetColumn(ImagenPreview, 0);
            Grid.SetColumnSpan(ImagenPreview, 1);
            ImagenPreview.HorizontalAlignment = HorizontalAlignment.Left;
            ImagenPreview.Margin = new Thickness(0, 0, 10, 0);

            Grid.SetRow(PreviewCodigo, 0);
            Grid.SetColumn(PreviewCodigo, 1);
            Grid.SetColumnSpan(PreviewCodigo, 1);
            PreviewCodigo.HorizontalAlignment = HorizontalAlignment.Left;
            PreviewCodigo.TextAlignment = TextAlignment.Left;
            PreviewCodigo.VerticalAlignment = VerticalAlignment.Bottom;
            PreviewCodigo.Margin = new Thickness(0, 0, 0, 0);

            Grid.SetRow(PreviewProducto, 1);
            Grid.SetColumn(PreviewProducto, 1);
            Grid.SetColumnSpan(PreviewProducto, 1);
            PreviewProducto.HorizontalAlignment = HorizontalAlignment.Left;
            PreviewProducto.TextAlignment = TextAlignment.Left;
            PreviewProducto.VerticalAlignment = VerticalAlignment.Top;
            PreviewProducto.Margin = new Thickness(0, 4, 0, 0);
            PreviewProducto.MaxWidth = 150;
            return;
        }

        Grid.SetRow(ImagenPreview, 0);
        Grid.SetRowSpan(ImagenPreview, 1);
        Grid.SetColumn(ImagenPreview, 0);
        Grid.SetColumnSpan(ImagenPreview, 2);
        ImagenPreview.HorizontalAlignment = HorizontalAlignment.Center;
        ImagenPreview.Margin = new Thickness(0);

        Grid.SetRow(PreviewCodigo, 1);
        Grid.SetColumn(PreviewCodigo, 0);
        Grid.SetColumnSpan(PreviewCodigo, 2);
        PreviewCodigo.HorizontalAlignment = HorizontalAlignment.Center;
        PreviewCodigo.TextAlignment = TextAlignment.Center;
        PreviewCodigo.VerticalAlignment = VerticalAlignment.Top;
        PreviewCodigo.Margin = new Thickness(0, 2, 0, 0);

        Grid.SetRow(PreviewProducto, 2);
        Grid.SetColumn(PreviewProducto, 0);
        Grid.SetColumnSpan(PreviewProducto, 2);
        PreviewProducto.HorizontalAlignment = HorizontalAlignment.Center;
        PreviewProducto.TextAlignment = TextAlignment.Center;
        PreviewProducto.VerticalAlignment = VerticalAlignment.Top;
        PreviewProducto.Margin = new Thickness(0, 4, 0, 0);
        PreviewProducto.MaxWidth = 160;
    }

    private async void AdjuntarExcel_Click(object sender, RoutedEventArgs e)
    {
        string? temporal = null;
        try
        {
            temporal = await ExcelUi.ElegirExcelTemporalAsync();
            if (temporal is null)
            {
                return;
            }

            _filas = ExcelEtiquetaService.Leer(temporal);
            _previewTipo = null;
            ListaProductos.ItemsSource = _filas;
            NombreArchivo.Text = "Excel validado. Encabezados: CÓDIGO · ÁREA · PRODUCTO · CANTIDAD.";
            var total = _filas.Sum(f => f.Cantidad);
            ResumenArchivo.Text = $"{_filas.Count} producto(s) en la lista · se imprimirán {total} etiqueta(s) según la columna CANTIDAD de cada fila.";
            ActualizarResumen();
            Mostrar("El archivo cumple la estructura y ya puede generar las etiquetas.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            _filas = [];
            _previewTipo = null;
            ListaProductos.ItemsSource = null;
            NombreArchivo.Text = "Ningún archivo adjunto.";
            ResumenArchivo.Text = string.Empty;
            ActualizarResumen();
            Mostrar(ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            if (temporal is not null && File.Exists(temporal))
            {
                File.Delete(temporal);
            }
        }
    }

    private async void ImportarRegistro_Click(object sender, RoutedEventArgs e)
    {
        var hoy = DateTimeOffset.Now;
        var inicioMes = new DateTimeOffset(new DateTime(hoy.Year, hoy.Month, 1));
        var desde = new CalendarDatePicker
        {
            Header = "Desde",
            Date = inicioMes,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var hasta = new CalendarDatePicker
        {
            Header = "Hasta",
            Date = hoy,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var unoPorProducto = new RadioButton
        {
            Content = "1 QR por producto",
            GroupName = "ModoEtiquetaRegistro",
            IsChecked = true
        };
        var porCantidad = new RadioButton
        {
            Content = "La cantidad agregada al inventario",
            GroupName = "ModoEtiquetaRegistro"
        };
        var error = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };
        var fechas = new Grid { ColumnSpacing = 12 };
        fechas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fechas.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(hasta, 1);
        fechas.Children.Add(desde);
        fechas.Children.Add(hasta);

        var panel = new StackPanel { Spacing = 12, MinWidth = 460 };
        panel.Children.Add(new TextBlock
        {
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap,
            Text = "Se toman los ingresos del registro en ese rango y se cargan en la lista. Después pulse Generar etiquetas para crear los QR."
        });
        panel.Children.Add(fechas);
        panel.Children.Add(unoPorProducto);
        panel.Children.Add(porCantidad);
        panel.Children.Add(error);

        var dialogo = new ContentDialog
        {
            Title = "Importar del registro",
            Content = panel,
            PrimaryButtonText = "Cargar a la lista",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        dialogo.PrimaryButtonClick += (_, args) =>
        {
            if (desde.Date is null || hasta.Date is null)
            {
                args.Cancel = true;
                error.Text = "Elija la fecha inicial y la final.";
                error.Visibility = Visibility.Visible;
                return;
            }

            if (desde.Date.Value.Date > hasta.Date.Value.Date)
            {
                args.Cancel = true;
                error.Text = "La fecha inicial no puede ser posterior a la final.";
                error.Visibility = Visibility.Visible;
                return;
            }

            error.Visibility = Visibility.Collapsed;
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary ||
            desde.Date is null ||
            hasta.Date is null)
        {
            return;
        }

        var inicio = desde.Date.Value.Date;
        var fin = hasta.Date.Value.Date;
        var unaSola = unoPorProducto.IsChecked == true;
        var movimientos = App.Instance.Registro.IngresosEntre(inicio, fin);
        var filas = FilasDesdeRegistro(movimientos, unaSola, out var omitidos);
        if (filas.Count == 0)
        {
            Mostrar(
                omitidos > 0
                    ? "En ese rango solo hay ingresos de productos sin código o que ya no están en el catálogo."
                    : "No hay ingresos en ese rango de fechas.",
                InfoBarSeverity.Warning);
            return;
        }

        _filas = filas;
        _previewTipo = null;
        ListaProductos.ItemsSource = _filas;
        ComboTipo.SelectedIndex = 0;
        NombreArchivo.Text = $"Registro del {inicio:dd/MM/yyyy} al {fin:dd/MM/yyyy}.";
        var total = _filas.Sum(f => f.Cantidad);
        var modo = unaSola ? "1 QR por producto" : "la cantidad ingresada al inventario";
        ResumenArchivo.Text = $"{_filas.Count} producto(s) · se imprimirán {total} etiqueta(s) ({modo}).";
        ActualizarResumen();
        var aviso = omitidos > 0
            ? $" Se omitieron {omitidos} producto(s) sin código o que ya no están en el catálogo."
            : string.Empty;
        Mostrar($"La lista ya está lista para generar los QR.{aviso}", InfoBarSeverity.Success);
    }

    private static List<FilaEtiqueta> FilasDesdeRegistro(
        IReadOnlyList<MovimientoInventario> movimientos,
        bool unaPorProducto,
        out int omitidos)
    {
        omitidos = 0;
        var porFicha = new Dictionary<Guid, double>();
        foreach (var movimiento in movimientos)
        {
            porFicha.TryGetValue(movimiento.FichaId, out var suma);
            porFicha[movimiento.FichaId] = suma + movimiento.Cantidad;
        }

        var filas = new List<FilaEtiqueta>();
        foreach (var (fichaId, suma) in porFicha)
        {
            var ficha = App.Instance.Repositorio.Obtener(fichaId);
            var codigo = ficha?.Codigo.Trim() ?? string.Empty;
            if (ficha is null || codigo.Length == 0)
            {
                omitidos++;
                continue;
            }

            var cantidad = unaPorProducto
                ? 1
                : Math.Max(1, (int)Math.Round(suma, MidpointRounding.AwayFromZero));
            filas.Add(new FilaEtiqueta(
                codigo,
                AreasOperativas.Prefijo(ficha.Area),
                ficha.Nombre.Trim(),
                cantidad));
        }

        filas.Sort((a, b) =>
        {
            var area = string.Compare(a.Area, b.Area, StringComparison.OrdinalIgnoreCase);
            return area != 0
                ? area
                : string.Compare(a.Codigo, b.Codigo, StringComparison.OrdinalIgnoreCase);
        });
        return filas;
    }

    private async void TablaNombreQr_Click(object sender, RoutedEventArgs e)
    {
        string? temporal = null;
        try
        {
            temporal = await ExcelUi.ElegirExcelTemporalAsync();
            if (temporal is null)
            {
                return;
            }

            var codigos = await Task.Run(() => ExcelEtiquetaService.LeerCodigos(temporal));
            var productos = ResolverProductos(codigos);
            var orden = await ElegirOrdenTablaAsync(productos);
            if (orden is null)
            {
                return;
            }

            var ordenados = OrdenarProductos(productos, orden.Value);
            var archivo = await ExcelUi.ElegirGuardarPdfAsync("tabla-nombre-qr-iderma");
            if (archivo is null)
            {
                return;
            }

            var resultado = await EjecutarConProgresoAsync(
                "Generando la tabla…",
                progreso =>
                {
                    var salida = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
                    PdfEtiquetaService.ExportarTablaNombreQr(salida, ordenados, orden.Value, progreso);
                    return salida;
                },
                archivo);

            if (resultado is null)
            {
                return;
            }

            var sinNombre = ordenados.Count(p => !p.EnCatalogo);
            var aviso = sinNombre > 0
                ? $" {sinNombre} código(s) no tienen nombre en el Excel ni en el catálogo."
                : string.Empty;
            var modo = orden.Value == OrdenTablaQr.Alfabetico ? "orden alfabético" : "orden del código";
            Mostrar($"Se exportó la tabla con {ordenados.Count} producto(s) en {modo}.{aviso}", InfoBarSeverity.Success);
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            Mostrar(ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            if (temporal is not null && File.Exists(temporal))
            {
                File.Delete(temporal);
            }
        }
    }

    private static List<ProductoQrLista> ResolverProductos(IReadOnlyList<(string Codigo, string Nombre)> filas)
    {
        var catalogo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ficha in App.Instance.Repositorio.Todas)
        {
            var codigo = ficha.Codigo.Trim();
            var nombre = ficha.Nombre.Trim();
            if (codigo.Length == 0 || nombre.Length == 0)
            {
                continue;
            }

            catalogo.TryAdd(codigo, nombre);
            catalogo.TryAdd(ClaveCodigo(codigo), nombre);
        }

        var productos = new List<ProductoQrLista>(filas.Count);
        foreach (var (codigo, nombreExcel) in filas)
        {
            if (nombreExcel.Length > 0 && !string.Equals(nombreExcel, codigo, StringComparison.OrdinalIgnoreCase))
            {
                productos.Add(new ProductoQrLista(codigo, nombreExcel, true));
                continue;
            }

            if ((catalogo.TryGetValue(codigo, out var nombre) || catalogo.TryGetValue(ClaveCodigo(codigo), out nombre))
                && nombre.Length > 0)
            {
                productos.Add(new ProductoQrLista(codigo, nombre, true));
                continue;
            }

            productos.Add(new ProductoQrLista(codigo, codigo, false));
        }

        return productos;
    }

    private static string ClaveCodigo(string codigo)
    {
        var limpio = new string(codigo.Where(c => !char.IsWhiteSpace(c)).ToArray());
        return limpio.Replace('-', '_').ToUpperInvariant();
    }

    private static List<ProductoQrLista> OrdenarProductos(IReadOnlyList<ProductoQrLista> productos, OrdenTablaQr orden)
    {
        var lista = productos.ToList();
        if (orden == OrdenTablaQr.Alfabetico)
        {
            lista.Sort((a, b) =>
            {
                var nombre = string.Compare(a.Nombre, b.Nombre, StringComparison.CurrentCultureIgnoreCase);
                return nombre != 0 ? nombre : CompararCodigo(a.Codigo, b.Codigo);
            });
            return lista;
        }

        lista.Sort((a, b) => CompararCodigo(a.Codigo, b.Codigo));
        return lista;
    }

    private static int CompararCodigo(string izquierda, string derecha)
    {
        var ia = 0;
        var ib = 0;
        while (ia < izquierda.Length && ib < derecha.Length)
        {
            var digitoA = char.IsDigit(izquierda[ia]);
            var digitoB = char.IsDigit(derecha[ib]);
            if (digitoA && digitoB)
            {
                var inicioA = ia;
                var inicioB = ib;
                while (ia < izquierda.Length && char.IsDigit(izquierda[ia]))
                {
                    ia++;
                }

                while (ib < derecha.Length && char.IsDigit(derecha[ib]))
                {
                    ib++;
                }

                var na = izquierda.AsSpan(inicioA, ia - inicioA).TrimStart('0');
                var nb = derecha.AsSpan(inicioB, ib - inicioB).TrimStart('0');
                if (na.Length == 0)
                {
                    na = "0";
                }

                if (nb.Length == 0)
                {
                    nb = "0";
                }

                var porLargo = na.Length.CompareTo(nb.Length);
                if (porLargo != 0)
                {
                    return porLargo;
                }

                var porValor = na.CompareTo(nb, StringComparison.Ordinal);
                if (porValor != 0)
                {
                    return porValor;
                }

                continue;
            }

            var caracter = char.ToUpperInvariant(izquierda[ia]).CompareTo(char.ToUpperInvariant(derecha[ib]));
            if (caracter != 0)
            {
                return caracter;
            }

            ia++;
            ib++;
        }

        return (izquierda.Length - ia).CompareTo(derecha.Length - ib);
    }

    private async Task<OrdenTablaQr?> ElegirOrdenTablaAsync(IReadOnlyList<ProductoQrLista> productos)
    {
        var alfabetico = new RadioButton
        {
            Content = "Orden alfabético (nombre del producto)",
            GroupName = "OrdenTablaQr",
            IsChecked = true
        };
        var porCodigo = new RadioButton
        {
            Content = "Orden del código",
            GroupName = "OrdenTablaQr"
        };
        var sinCatalogo = productos.Count(p => !p.EnCatalogo);
        var panel = new StackPanel { Spacing = 10, MinWidth = 420 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.78,
            Text = $"Se leyeron {productos.Count} código(s). Cada fila muestra tres productos: el nombre y, al lado, su QR. El nombre sale de la columna PRODUCTO del Excel o, si falta, del catálogo."
        });
        if (sinCatalogo > 0)
        {
            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = $"{sinCatalogo} código(s) no tienen nombre en el Excel ni en el catálogo. En esas filas se imprime el código."
            });
        }

        panel.Children.Add(alfabetico);
        panel.Children.Add(porCodigo);

        var dialogo = new ContentDialog
        {
            Title = "Tabla nombre y QR",
            Content = panel,
            PrimaryButtonText = "Exportar PDF",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        return porCodigo.IsChecked == true ? OrdenTablaQr.Codigo : OrdenTablaQr.Alfabetico;
    }

    private async void Plantilla_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var resultado = await ExcelUi.GuardarExcelAsync(
                "plantilla-etiquetas-iderma",
                ExcelEtiquetaService.CrearPlantilla);
            if (resultado is null)
            {
                return;
            }

            Mostrar("Se exportó la plantilla con los encabezados CÓDIGO, ÁREA, PRODUCTO y CANTIDAD.", InfoBarSeverity.Success);
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo descargar la plantilla: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void PruebaImpresion_Click(object sender, RoutedEventArgs e)
    {
        var archivo = await ExcelUi.ElegirGuardarPdfAsync("prueba-impresion-calibracion-iderma");
        if (archivo is null)
        {
            return;
        }

        try
        {
            var resultado = await EjecutarConProgresoAsync(
                "Generando prueba de impresión…",
                progreso =>
                {
                    progreso.Report((10, "Preparando la hoja de calibración…"));
                    var temporal = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
                    progreso.Report((40, "Dibujando colores, grosores y QR…"));
                    PdfEtiquetaService.ExportarPruebaImpresion(temporal);
                    progreso.Report((90, "Guardando el PDF…"));
                    return temporal;
                },
                archivo);

            if (resultado is null)
            {
                return;
            }

            Mostrar("Se generó la prueba de calibración (colores, tinta, grosores, QR y estándares).", InfoBarSeverity.Success);
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo generar la prueba: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void Generar_Click(object sender, RoutedEventArgs e)
    {
        if (_filas.Count == 0)
        {
            Mostrar("Adjunte un Excel o importe los ingresos del registro.", InfoBarSeverity.Warning);
            return;
        }

        var archivo = await ExcelUi.ElegirGuardarPdfAsync("etiquetas-iderma");
        if (archivo is null)
        {
            return;
        }

        try
        {
            var opciones = OpcionesActuales();
            var filas = _filas.ToList();
            var resultado = await EjecutarConProgresoAsync(
                "Generando etiquetas…",
                progreso =>
                {
                    var temporal = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
                    PdfEtiquetaService.ExportarEtiquetas(temporal, filas, opciones, progreso);
                    return temporal;
                },
                archivo);

            if (resultado is null)
            {
                return;
            }

            var total = filas.Sum(f => f.Cantidad);
            Mostrar($"Se generó el PDF con {total} etiqueta(s): cada fila se repite las veces que indica su CANTIDAD.", InfoBarSeverity.Success);
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            Mostrar(ex.Message, InfoBarSeverity.Error);
        }
    }

    private async Task<ResultadoExportacion?> EjecutarConProgresoAsync(
        string titulo,
        Func<IProgress<(int Porcentaje, string Mensaje)>, string> trabajo,
        StorageFile archivo)
    {
        TextoProgreso.Text = titulo;
        BarraProgreso.IsIndeterminate = true;
        BarraProgreso.Value = 0;
        PanelProgreso.Visibility = Visibility.Visible;
        if (BotonGenerar is not null)
        {
            BotonGenerar.IsEnabled = false;
        }

        if (BotonTablaQr is not null)
        {
            BotonTablaQr.IsEnabled = false;
        }

        var textoDialogo = new TextBlock { Text = titulo, TextWrapping = TextWrapping.Wrap };
        var barraDialogo = new ProgressBar { Minimum = 0, Maximum = 100, IsIndeterminate = true, Height = 10 };
        var contenido = new StackPanel { Spacing = 12 };
        contenido.Children.Add(textoDialogo);
        contenido.Children.Add(barraDialogo);

        var dialogo = new ContentDialog
        {
            Title = titulo,
            Content = contenido,
            XamlRoot = XamlRoot
        };
        var mostrar = dialogo.ShowAsync();

        var progreso = new Progress<(int Porcentaje, string Mensaje)>(estado =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                BarraProgreso.IsIndeterminate = false;
                BarraProgreso.Value = estado.Porcentaje;
                TextoProgreso.Text = estado.Mensaje;
                barraDialogo.IsIndeterminate = false;
                barraDialogo.Value = estado.Porcentaje;
                textoDialogo.Text = estado.Mensaje;
            });
        });

        try
        {
            var temporal = await Task.Run(() => trabajo(progreso));
            DispatcherQueue.TryEnqueue(() =>
            {
                BarraProgreso.IsIndeterminate = false;
                BarraProgreso.Value = 100;
                TextoProgreso.Text = "Guardando el archivo…";
                barraDialogo.Value = 100;
                textoDialogo.Text = "Guardando el archivo…";
            });
            return await ExcelUi.CopiarPdfAsync(temporal, archivo);
        }
        finally
        {
            dialogo.Hide();
            try
            {
                await mostrar;
            }
            catch (Exception)
            {
                // El diálogo se cerró al terminar el proceso.
            }

            PanelProgreso.Visibility = Visibility.Collapsed;
            BarraProgreso.IsIndeterminate = false;
            if (BotonGenerar is not null)
            {
                BotonGenerar.IsEnabled = true;
            }

            if (BotonTablaQr is not null)
            {
                BotonTablaQr.IsEnabled = true;
            }
        }
    }

    private void Mostrar(string mensaje, InfoBarSeverity severidad)
    {
        BarraEstado.Severity = severidad;
        BarraEstado.Message = mensaje;
        BarraEstado.IsOpen = true;
    }
}
