using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace IdermaFichas.Helpers;

public sealed record ResultadoExportacion(StorageFile Archivo, string Extension)
{
    public string Ruta => Archivo.Path;
}

public static class ExcelUi
{
    public static async Task<ResultadoImportacion?> ImportarAsync(Guid? area = null)
    {
        var selector = new FileOpenPicker();
        VentanaHelper.AsociarSelector(selector);
        VentanaHelper.ConfigurarInicio(selector);
        selector.FileTypeFilter.Add(".xlsx");

        var archivo = await selector.PickSingleFileAsync();
        if (archivo is null)
        {
            return null;
        }

        var temporal = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
        await using (var origen = await archivo.OpenStreamForReadAsync())
        await using (var destino = File.Create(temporal))
        {
            await origen.CopyToAsync(destino);
        }

        try
        {
            var fichas = ExcelFichaService.Leer(temporal, area);
            return App.Instance.Repositorio.ImportarFichas(fichas, area);
        }
        finally
        {
            if (File.Exists(temporal))
            {
                File.Delete(temporal);
            }
        }
    }

    public static Task<ResultadoExportacion?> GuardarPdfAsync(string nombreSugerido, Action<string> escribir) =>
        GuardarDocumentoAsync(nombreSugerido, (ruta, _) => escribir(ruta), soloPdf: true);

    public static async Task<StorageFile?> ElegirGuardarPdfAsync(string nombreSugerido)
    {
        var selector = new FileSavePicker();
        VentanaHelper.AsociarSelector(selector);
        VentanaHelper.ConfigurarInicio(selector);
        selector.FileTypeChoices.Add("PDF", [".pdf"]);
        selector.DefaultFileExtension = ".pdf";
        selector.SuggestedFileName = nombreSugerido;
        return await selector.PickSaveFileAsync();
    }

    public static async Task<ResultadoExportacion> CopiarPdfAsync(string rutaTemporal, StorageFile archivo)
    {
        try
        {
            await CopiarArchivoAsync(rutaTemporal, archivo);
            return new ResultadoExportacion(archivo, ".pdf");
        }
        finally
        {
            if (File.Exists(rutaTemporal))
            {
                File.Delete(rutaTemporal);
            }
        }
    }

    public static Task<ResultadoExportacion?> GuardarExcelAsync(string nombreSugerido, Action<string> escribir) =>
        GuardarDocumentoAsync(nombreSugerido, (ruta, _) => escribir(ruta), soloExcel: true);

    public static async Task<string?> ElegirExcelTemporalAsync()
    {
        var selector = new FileOpenPicker();
        VentanaHelper.AsociarSelector(selector);
        VentanaHelper.ConfigurarInicio(selector);
        selector.FileTypeFilter.Add(".xlsx");

        var archivo = await selector.PickSingleFileAsync();
        if (archivo is null)
        {
            return null;
        }

        var temporal = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
        await using (var origen = await archivo.OpenStreamForReadAsync())
        await using (var destino = File.Create(temporal))
        {
            await origen.CopyToAsync(destino);
        }

        return temporal;
    }

    public static async Task<bool> GuardarPlantillaAsync()
    {
        await InventarioNube.CargarCompletasAsync(App.Instance.Repositorio.CopiarTodas());
        var fichas = App.Instance.Repositorio.CopiarTodas();
        return await GuardarExcelAsync(
            "plantilla-fichas-iderma",
            ruta => ExcelFichaService.CrearPlantilla(ruta, fichas)) is not null;
    }

    public static Task<ResultadoExportacion?> ExportarFichaAsync(FichaTecnica ficha) =>
        GuardarDocumentoAsync(
            $"ficha-{Sanitizar(ficha.Codigo)}",
            (ruta, extension) =>
            {
                if (EsPdf(extension))
                {
                    PdfFichaService.ExportarIndividual(ruta, ficha);
                }
                else
                {
                    ExcelFichaService.ExportarIndividual(ruta, ficha);
                }
            });

    public static Task<ResultadoExportacion?> ExportarHistorialPreciosAsync(
        FichaTecnica ficha,
        IReadOnlyList<PrecioProducto> precios) =>
        GuardarDocumentoAsync(
            $"historial-precios-{Sanitizar(ficha.Codigo)}",
            (ruta, extension) =>
            {
                if (EsPdf(extension))
                {
                    HistorialPreciosExportService.ExportarPdf(ruta, ficha, precios);
                }
                else
                {
                    HistorialPreciosExportService.ExportarExcel(ruta, ficha, precios);
                }
            });

    public static Task<ResultadoExportacion?> ExportarIngresosAsync(
        string nombreSugerido,
        IReadOnlyList<LineaRegistroIngreso> lineas,
        string periodo) =>
        GuardarDocumentoAsync(
            nombreSugerido,
            (ruta, extension) =>
            {
                if (EsPdf(extension))
                {
                    RegistroIngresosExportService.ExportarPdf(ruta, lineas, periodo);
                }
                else
                {
                    RegistroIngresosExportService.ExportarExcel(ruta, lineas, periodo);
                }
            });

    public static Task<ResultadoExportacion?> ExportarOrdenServicioAsync(
        OrdenServicioDocumento documento,
        bool pdf) =>
        GuardarDocumentoAsync(
            NombreServicio(documento),
            (ruta, _) =>
            {
                if (pdf)
                {
                    OrdenServicioExportService.ExportarPdf(ruta, documento);
                }
                else
                {
                    OrdenServicioExportService.ExportarExcel(ruta, documento);
                }
            },
            soloPdf: pdf,
            soloExcel: !pdf);

    public static Task<ResultadoExportacion?> ExportarOrdenCompraAsync(
        OrdenCompraDocumento documento,
        bool pdf) =>
        GuardarDocumentoAsync(
            NombreOrden(documento),
            (ruta, _) =>
            {
                if (pdf)
                {
                    OrdenCompraExportService.ExportarPdf(ruta, documento);
                }
                else
                {
                    OrdenCompraExportService.ExportarExcel(ruta, documento);
                }
            },
            soloPdf: pdf,
            soloExcel: !pdf);

    public static Task<ResultadoExportacion?> ExportarConsolidadoAsync(IReadOnlyList<FichaTecnica> fichas) =>
        GuardarDocumentoAsync(
            $"consolidado-fichas-{DateTime.Now:yyyyMMdd}",
            (ruta, extension) =>
            {
                if (EsPdf(extension))
                {
                    PdfFichaService.ExportarConsolidado(ruta, fichas);
                }
                else
                {
                    ExcelFichaService.ExportarConsolidado(ruta, fichas);
                }
            });

    public static async Task OfrecerAbrirAsync(XamlRoot? raiz, ResultadoExportacion resultado)
    {
        if (raiz is not null)
        {
            var ubicacion = string.IsNullOrWhiteSpace(resultado.Ruta)
                ? $"Se guardó «{resultado.Archivo.Name}»."
                : $"Se guardó en:\n{resultado.Ruta}";
            var dialogo = new ContentDialog
            {
                Title = "Archivo generado",
                Content = $"{ubicacion}\n\n¿Desea abrirlo ahora?",
                PrimaryButtonText = "Abrir",
                CloseButtonText = "Cerrar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = raiz
            };

            if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        try
        {
            if (await Launcher.LaunchFileAsync(resultado.Archivo))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(resultado.Ruta) && File.Exists(resultado.Ruta))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = resultado.Ruta,
                    UseShellExecute = true
                });
                return;
            }
        }
        catch
        {
        }

        if (raiz is not null)
        {
            var dialogoError = new ContentDialog
            {
                Title = "No se pudo abrir",
                Content = string.IsNullOrWhiteSpace(resultado.Ruta)
                    ? "El archivo se guardó, pero Windows no pudo abrirlo. Ábralo desde la carpeta donde lo guardó."
                    : $"El archivo se guardó, pero Windows no pudo abrirlo. Ábralo desde:\n{resultado.Ruta}",
                CloseButtonText = "Cerrar",
                XamlRoot = raiz
            };
            await dialogoError.ShowAsync();
        }
    }

    private static async Task<ResultadoExportacion?> GuardarDocumentoAsync(
        string nombreSugerido,
        Action<string, string> escribir,
        bool soloPdf = false,
        bool soloExcel = false,
        Window? ventana = null)
    {
        var selector = new FileSavePicker();
        VentanaHelper.AsociarSelector(selector, ventana);
        VentanaHelper.ConfigurarInicio(selector);
        if (soloExcel)
        {
            selector.FileTypeChoices.Add("Excel", [".xlsx"]);
            selector.DefaultFileExtension = ".xlsx";
        }
        else if (soloPdf)
        {
            selector.FileTypeChoices.Add("PDF", [".pdf"]);
            selector.DefaultFileExtension = ".pdf";
        }
        else
        {
            selector.FileTypeChoices.Add("PDF", [".pdf"]);
            selector.FileTypeChoices.Add("Excel", [".xlsx"]);
            selector.DefaultFileExtension = ".pdf";
        }

        selector.SuggestedFileName = nombreSugerido;

        var archivo = await selector.PickSaveFileAsync();
        if (archivo is null)
        {
            return null;
        }

        var extension = ExtensionDe(archivo, soloExcel ? ".xlsx" : ".pdf");
        var temporal = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
        try
        {
            await Task.Run(() => escribir(temporal, extension));
            if (!File.Exists(temporal) || new FileInfo(temporal).Length == 0)
            {
                throw new InvalidOperationException("El documento se generó vacío. Inténtelo de nuevo.");
            }

            await CopiarArchivoAsync(temporal, archivo);
            return new ResultadoExportacion(archivo, extension);
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

    public static async Task CopiarArchivoAsync(string temporal, StorageFile archivo)
    {
        if (!File.Exists(temporal) || new FileInfo(temporal).Length == 0)
        {
            throw new InvalidOperationException("No se encontró el documento generado.");
        }

        var bytes = await File.ReadAllBytesAsync(temporal);
        if (RutaCompleta(archivo.Path, bytes.Length))
        {
            return;
        }

        var aplazado = false;
        try
        {
            CachedFileManager.DeferUpdates(archivo);
            aplazado = true;
        }
        catch
        {
        }

        Exception? errorEscritura = null;
        try
        {
            await FileIO.WriteBytesAsync(archivo, bytes);
        }
        catch (Exception ex)
        {
            errorEscritura = ex;
            try
            {
                using var ras = await archivo.OpenAsync(FileAccessMode.ReadWrite);
                ras.Size = 0;
                using var destino = ras.AsStreamForWrite();
                await destino.WriteAsync(bytes);
                await destino.FlushAsync();
                errorEscritura = null;
            }
            catch (Exception exFlujo)
            {
                errorEscritura = exFlujo;
            }
        }
        finally
        {
            if (aplazado)
            {
                try
                {
                    await CachedFileManager.CompleteUpdatesAsync(archivo);
                }
                catch
                {
                }
            }
        }

        if (RutaCompleta(archivo.Path, bytes.Length) || CopiarPorRuta(temporal, archivo, bytes.Length))
        {
            return;
        }

        if (errorEscritura is not null)
        {
            throw new InvalidOperationException(
                "No se pudo guardar el archivo en la carpeta elegida. Pruebe Descargas u otra carpeta local.",
                errorEscritura);
        }
    }

    private static bool CopiarPorRuta(string temporal, StorageFile archivo, long esperado) =>
        CopiarPorRuta(temporal, archivo.Path, esperado);

    private static bool CopiarPorRuta(string temporal, string? ruta, long esperado)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(temporal))
            {
                return false;
            }

            var carpeta = Path.GetDirectoryName(ruta);
            if (!string.IsNullOrWhiteSpace(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }

            File.Copy(temporal, ruta, overwrite: true);
            return RutaCompleta(ruta, esperado);
        }
        catch
        {
            return false;
        }
    }

    private static bool RutaCompleta(string? ruta, long esperado)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(ruta)
                   && File.Exists(ruta)
                   && esperado > 0
                   && new FileInfo(ruta).Length == esperado;
        }
        catch
        {
            return false;
        }
    }

    private static string ExtensionDe(StorageFile archivo, string preferida)
    {
        foreach (var candidato in new[]
                 {
                     Path.GetExtension(archivo.Path),
                     Path.GetExtension(archivo.Name),
                     archivo.FileType,
                     preferida
                 })
        {
            if (string.Equals(candidato, ".pdf", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidato, ".xlsx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidato, ".zip", StringComparison.OrdinalIgnoreCase))
            {
                return candidato.ToLowerInvariant();
            }
        }

        return preferida.StartsWith('.') ? preferida.ToLowerInvariant() : ".pdf";
    }

    private static bool EsPdf(string extension) =>
        extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    private static string NombreServicio(OrdenServicioDocumento documento)
    {
        var titulo = documento.TituloImpresion.Replace('/', '-');
        var limpio = new string(titulo.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(limpio) ? $"ORDEN DE SERVICIO {DateTime.Now:dd-MM-yyyy}" : limpio;
    }

    private static string NombreOrden(OrdenCompraDocumento documento)
    {
        var titulo = documento.TituloImpresion.Replace('/', '-');
        var limpio = new string(titulo.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(limpio) ? $"ORDEN DE COMPRA {DateTime.Now:dd-MM-yyyy}" : limpio;
    }

    private static string Sanitizar(string valor)
    {
        var limpio = new string(valor.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(limpio) ? "ficha" : limpio;
    }
}
