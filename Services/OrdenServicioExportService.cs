using System.Globalization;
using ClosedXML.Excel;
using IdermaFichas.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IdermaFichas.Services;

public static class OrdenServicioExportService
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    public static void ExportarExcel(string ruta, OrdenServicioDocumento documento)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Orden de servicio");
        hoja.Cell(1, 1).Value = documento.TituloImpresion;
        hoja.Cell(2, 1).Value = "Ampliación de contratos por meses";
        hoja.Cell(3, 1).Value = "Código";
        hoja.Cell(3, 2).Value = documento.CodigoFormato;
        hoja.Cell(3, 3).Value = "Mes / Año";
        hoja.Cell(3, 4).Value = documento.MesAnio;
        hoja.Cell(3, 5).Value = "Estado";
        hoja.Cell(3, 6).Value = documento.Estado;
        hoja.Cell(4, 1).Value = "Elaborado por";
        hoja.Cell(4, 2).Value = documento.ElaboradoPor;
        hoja.Cell(4, 3).Value = "Revisado por";
        hoja.Cell(4, 4).Value = documento.RevisadoPor;
        hoja.Cell(4, 5).Value = "Cobertura";
        hoja.Cell(4, 6).Value = documento.PeriodoCobertura;
        var headers = new[]
        {
            "#", "Proveedor", "RUC", "Contrato", "Servicio", "Vigencia actual", "Meses",
            "Nueva vigencia", "Mensual", "Sin IGV", "IGV 18%", "Con IGV"
        };
        for (var i = 0; i < headers.Length; i++)
        {
            hoja.Cell(6, i + 1).Value = headers[i];
            hoja.Cell(6, i + 1).Style.Font.Bold = true;
        }

        var fila = 7;
        var n = 1;
        foreach (var linea in documento.Lineas)
        {
            hoja.Cell(fila, 1).Value = n;
            hoja.Cell(fila, 2).Value = linea.Proveedor;
            hoja.Cell(fila, 3).Value = linea.Ruc;
            hoja.Cell(fila, 4).Value = linea.Contrato;
            hoja.Cell(fila, 5).Value = linea.Servicio;
            hoja.Cell(fila, 6).Value = linea.VigenciaActual?.ToString("dd/MM/yyyy", Cultura) ?? "";
            hoja.Cell(fila, 7).Value = linea.Meses;
            hoja.Cell(fila, 8).Value = linea.NuevaVigencia.ToString("dd/MM/yyyy", Cultura);
            hoja.Cell(fila, 9).Value = linea.MontoMensual;
            hoja.Cell(fila, 10).Value = linea.SinIgv;
            hoja.Cell(fila, 11).Value = linea.Igv;
            hoja.Cell(fila, 12).Value = linea.TotalConIgv;
            fila++;
            n++;
        }

        hoja.Cell(fila + 1, 1).Value = "Total con IGV";
        hoja.Cell(fila + 1, 2).Value = documento.Lineas.Sum(l => l.TotalConIgv);
        hoja.Columns().AdjustToContents();
        IdermaMarca.AplicarPropiedades(libro, documento.TituloImpresion);
        libro.SaveAs(ruta);
    }

    public static void ExportarPdf(string ruta, OrdenServicioDocumento documento)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        Document.Create(doc =>
        {
            doc.Page(pagina =>
            {
                pagina.Size(PageSizes.A4.Landscape());
                pagina.Margin(28);
                pagina.DefaultTextStyle(e => e.FontSize(9).FontColor("#1A1A1A"));
                pagina.Header().Column(col =>
                {
                    col.Item().Text(IdermaMarca.NombreMayusculas).FontSize(14).Bold().FontColor(IdermaMarca.Navy);
                    col.Item().Text(documento.TituloImpresion).FontSize(10).FontColor(IdermaMarca.Oro);
                });
                pagina.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text($"Código {documento.CodigoFormato}   ·   {documento.MesAnio}   ·   {documento.FechaElaboracion}   ·   {documento.Estado}");
                    col.Item().Text($"Elaborado por {Texto(documento.ElaboradoPor)}   ·   Revisado por {Texto(documento.RevisadoPor)}   ·   {Texto(documento.PeriodoCobertura)}");
                    col.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(24);
                            c.RelativeColumn(2);
                            c.ConstantColumn(72);
                            c.RelativeColumn(2);
                            c.ConstantColumn(48);
                            c.ConstantColumn(78);
                            c.ConstantColumn(78);
                            c.ConstantColumn(78);
                            c.ConstantColumn(82);
                        });
                        tabla.Header(h =>
                        {
                            foreach (var titulo in new[] { "#", "Proveedor", "RUC", "Servicio", "Meses", "Nueva vigencia", "Sin IGV", "IGV", "Con IGV" })
                            {
                                h.Cell().Background(IdermaMarca.Navy).Padding(4).Text(titulo).FontSize(8).FontColor(Colors.White).Bold();
                            }
                        });
                        var n = 1;
                        foreach (var linea in documento.Lineas)
                        {
                            foreach (var texto in new[]
                                     {
                                         n.ToString(),
                                         linea.Proveedor,
                                         string.IsNullOrWhiteSpace(linea.Ruc) ? "—" : linea.Ruc,
                                         linea.Servicio,
                                         linea.Meses.ToString(Cultura),
                                         linea.NuevaVigencia.ToString("dd/MM/yyyy", Cultura),
                                         linea.SinIgv.ToString("C", Cultura),
                                         linea.Igv.ToString("C", Cultura),
                                         linea.TotalConIgv.ToString("C", Cultura)
                                     })
                            {
                                tabla.Cell().BorderBottom(0.4f).BorderColor("#E4E0D8").Padding(3).Text(texto).FontSize(8);
                            }

                            n++;
                        }
                    });
                    col.Item().AlignRight().Text($"Total con IGV: {documento.Lineas.Sum(l => l.TotalConIgv).ToString("C", Cultura)}").Bold().FontSize(12);
                });
                pagina.Footer().AlignRight().Text(t =>
                {
                    t.Span("Página ").FontSize(8);
                    t.CurrentPageNumber().FontSize(8);
                    t.Span(" de ").FontSize(8);
                    t.TotalPages().FontSize(8);
                });
            });
        }).GeneratePdf(ruta);
    }

    private static string Texto(string valor) => string.IsNullOrWhiteSpace(valor) ? "—" : valor.Trim();
}
