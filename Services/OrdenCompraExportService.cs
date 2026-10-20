using System.Globalization;
using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using IdermaFichas.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IdermaFichas.Services;

public static class OrdenCompraExportService
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private static readonly string Navy = IdermaMarca.Navy;
    private static readonly string Oro = IdermaMarca.Oro;
    private static readonly string Fondo = IdermaMarca.FondoSuave;
    private static readonly string AzulPrecios = "#1E4E8C";
    private static readonly string Gris = "#5A6570";
    private static readonly string Linea = "#D8D2C6";

    private static readonly string[] Columnas =
    [
        "#", "CÓD.", "NOMBRE DEL INSUMO", "MARCA", "PROVEEDOR", "RUC", "CONS. PROM.",
        "STOCK ACTUAL", "CANT. PEDIR", "UND.", "PRECIO UNIT. (sin IGV)", "IGV 18%",
        "TOTAL (con IGV)", "TIPO PRECIO", "ESTADO"
    ];

    public static void ExportarExcel(string ruta, OrdenCompraDocumento documento)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Orden de compra");
        const int columnas = 15;

        hoja.Style.Font.FontName = "Calibri";
        hoja.Style.Font.FontSize = 10;
        hoja.Row(1).Height = 22;
        hoja.Row(2).Height = 16;
        hoja.Row(3).Height = 6;
        hoja.Row(4).Height = 24;
        hoja.Row(5).Height = 22;
        hoja.Row(6).Height = 22;

        hoja.Range(1, 1, 2, columnas).Style.Fill.BackgroundColor = XLColor.White;
        InsertarLogo(hoja);
        hoja.Range(1, 3, 1, columnas).Merge();
        hoja.Cell(1, 3).Value = IdermaMarca.NombreMayusculas;
        hoja.Cell(1, 3).Style.Font.Bold = true;
        hoja.Cell(1, 3).Style.Font.FontSize = 16;
        hoja.Cell(1, 3).Style.Font.FontColor = XLColor.FromHtml(Navy);
        hoja.Cell(1, 3).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        hoja.Range(2, 3, 2, columnas).Merge();
        hoja.Cell(2, 3).Value = IdermaMarca.Giro;
        hoja.Cell(2, 3).Style.Font.FontSize = 10;
        hoja.Cell(2, 3).Style.Font.FontColor = XLColor.FromHtml(Oro);
        hoja.Cell(2, 3).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        hoja.Range(3, 1, 3, columnas).Style.Fill.BackgroundColor = XLColor.FromHtml(Oro);

        var titulo = hoja.Range(4, 1, 4, columnas);
        titulo.Merge();
        titulo.Style.Fill.BackgroundColor = XLColor.FromHtml(Navy);
        titulo.Style.Font.Bold = true;
        titulo.Style.Font.FontSize = 13;
        titulo.Style.Font.FontColor = XLColor.White;
        titulo.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titulo.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        hoja.Cell(4, 1).Value = documento.TituloImpresion;

        Meta(hoja, 5, 1, 4, "Código", documento.CodigoFormato);
        Meta(hoja, 5, 5, 9, "Mes / Año", documento.MesAnio);
        Meta(hoja, 5, 10, columnas, "Fecha de elaboración", documento.FechaElaboracion);
        Meta(hoja, 6, 1, 4, "Elaborado por", documento.ElaboradoPor);
        Meta(hoja, 6, 5, 8, "Revisado por", documento.RevisadoPor);
        Meta(hoja, 6, 9, 12, "Período de cobertura", documento.PeriodoCobertura);
        Meta(hoja, 6, 13, columnas, "Estado", documento.Estado);

        var meta = hoja.Range(5, 1, 6, columnas);
        meta.Style.Fill.BackgroundColor = XLColor.FromHtml(Fondo);
        meta.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        meta.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        meta.Style.Border.OutsideBorderColor = XLColor.FromHtml(Navy);

        const int filaGrupo = 8;
        const int filaCabeza = 9;
        Grupo(hoja, filaGrupo, 1, 10, "DETALLE DEL INSUMO", Navy);
        Grupo(hoja, filaGrupo, 11, 13, documento.TituloPrecios, AzulPrecios);
        Grupo(hoja, filaGrupo, 13, columnas, "ESTADO", Navy);
        hoja.Row(filaGrupo).Height = 18;

        for (var i = 0; i < Columnas.Length; i++)
        {
            var celda = hoja.Cell(filaCabeza, i + 1);
            celda.Value = Columnas[i];
            celda.Style.Font.Bold = true;
            celda.Style.Font.FontSize = 8;
            celda.Style.Font.FontColor = XLColor.White;
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml(i is >= 10 and <= 12 ? AzulPrecios : Navy);
            celda.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            celda.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            celda.Style.Alignment.WrapText = true;
        }

        hoja.Row(filaCabeza).Height = 32;

        var fila = filaCabeza;
        for (var i = 0; i < documento.Lineas.Count; i++)
        {
            fila++;
            var linea = documento.Lineas[i];
            var fondo = i % 2 == 0 ? XLColor.White : XLColor.FromHtml(Fondo);
            hoja.Cell(fila, 1).Value = i + 1;
            hoja.Cell(fila, 2).Value = linea.Codigo;
            hoja.Cell(fila, 3).Value = linea.Nombre;
            hoja.Cell(fila, 4).Value = linea.Marca;
            hoja.Cell(fila, 5).Value = linea.Proveedor;
            hoja.Cell(fila, 6).Value = string.IsNullOrWhiteSpace(linea.Ruc) ? "—" : linea.Ruc;
            hoja.Cell(fila, 7).Value = linea.ConsumoPromedio;
            hoja.Cell(fila, 8).Value = linea.StockActual;
            hoja.Cell(fila, 9).Value = linea.CantidadPedir;
            hoja.Cell(fila, 10).Value = linea.Unidad;
            if (linea.TienePrecio)
            {
                hoja.Cell(fila, 11).Value = linea.PrecioUnitarioSinIgv;
                hoja.Cell(fila, 12).Value = linea.Igv;
                hoja.Cell(fila, 13).Value = linea.TotalConIgv;
            }
            else
            {
                hoja.Cell(fila, 11).Value = "Sin precio";
                hoja.Cell(fila, 12).Value = "—";
                hoja.Cell(fila, 13).Value = "—";
            }

            hoja.Cell(fila, 14).Value = linea.TipoPrecio;
            hoja.Cell(fila, 15).Value = linea.Estado;

            var rango = hoja.Range(fila, 1, fila, columnas);
            rango.Style.Fill.BackgroundColor = fondo;
            rango.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            hoja.Cell(fila, 3).Style.Alignment.WrapText = true;
            foreach (var col in new[] { 1, 10, 14, 15 })
            {
                hoja.Cell(fila, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            foreach (var col in new[] { 7, 8, 9 })
            {
                hoja.Cell(fila, col).Style.NumberFormat.Format = "#,##0.00";
                hoja.Cell(fila, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }

            if (linea.TienePrecio)
            {
                foreach (var col in new[] { 11, 12, 13 })
                {
                    hoja.Cell(fila, col).Style.NumberFormat.Format = "#,##0.00";
                    hoja.Cell(fila, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }
            }
        }

        var filaDatosFin = Math.Max(fila, filaCabeza);
        foreach (var total in Totales(documento))
        {
            fila++;
            hoja.Range(fila, 1, fila, 10).Merge();
            hoja.Cell(fila, 1).Value = $"TOTAL {MonedaPrecio.Simbolo(total.Moneda)}";
            hoja.Cell(fila, 1).Style.Font.Bold = true;
            hoja.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            hoja.Cell(fila, 12).Value = total.Igv;
            hoja.Cell(fila, 13).Value = total.Total;
            hoja.Cell(fila, 12).Style.NumberFormat.Format = "#,##0.00";
            hoja.Cell(fila, 13).Style.NumberFormat.Format = "#,##0.00";
            hoja.Cell(fila, 12).Style.Font.Bold = true;
            hoja.Cell(fila, 13).Style.Font.Bold = true;
            hoja.Range(fila, 1, fila, columnas).Style.Fill.BackgroundColor = XLColor.FromHtml(Fondo);
        }

        var tabla = hoja.Range(filaGrupo, 1, Math.Max(fila, filaDatosFin), columnas);
        tabla.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tabla.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        tabla.Style.Border.OutsideBorderColor = XLColor.FromHtml(Navy);
        tabla.Style.Border.InsideBorderColor = XLColor.FromHtml(Linea);

        fila += 2;
        hoja.Range(fila, 1, fila, 7).Merge();
        hoja.Cell(fila, 1).Value = string.IsNullOrWhiteSpace(documento.ElaboradoPor)
            ? "Elaborado por: ____________________"
            : $"Elaborado por: {documento.ElaboradoPor}";
        hoja.Range(fila, 8, fila, columnas).Merge();
        hoja.Cell(fila, 8).Value = string.IsNullOrWhiteSpace(documento.RevisadoPor)
            ? "Revisado por: ____________________"
            : $"Revisado por: {documento.RevisadoPor}";

        fila += 2;
        hoja.Range(fila, 1, fila, columnas).Merge();
        hoja.Cell(fila, 1).Value =
            "Precio sin IGV = último precio − 18 %. IGV = 18 % de ese precio, en la moneda registrada (soles o dólares). " +
            "Total con IGV = último precio × cantidad a pedir. " +
            "Consumo promedio: salidas de los últimos 6 meses ÷ 6.";
        hoja.Cell(fila, 1).Style.Font.Italic = true;
        hoja.Cell(fila, 1).Style.Font.FontSize = 8;
        hoja.Cell(fila, 1).Style.Font.FontColor = XLColor.FromHtml(Gris);
        hoja.Cell(fila, 1).Style.Alignment.WrapText = true;
        hoja.Row(fila).Height = 28;

        fila += 2;
        IdermaMarca.EscribirPieTabla(hoja, fila, columnas);

        double[] anchos = [5, 14, 32, 14, 18, 14, 12, 13, 12, 8, 16, 12, 15, 14, 14];
        for (var i = 0; i < anchos.Length; i++)
        {
            hoja.Column(i + 1).Width = anchos[i];
        }

        hoja.SheetView.FreezeRows(filaCabeza);
        hoja.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        hoja.PageSetup.PaperSize = XLPaperSize.A4Paper;
        hoja.PageSetup.FitToPages(1, 0);
        hoja.PageSetup.CenterHorizontally = true;
        hoja.PageSetup.Margins.Top = 0.4;
        hoja.PageSetup.Margins.Bottom = 0.5;
        hoja.PageSetup.Margins.Left = 0.4;
        hoja.PageSetup.Margins.Right = 0.4;
        hoja.PageSetup.Footer.Left.AddText($"{IdermaMarca.Nombre} · {documento.CodigoFormato}");
        hoja.PageSetup.Footer.Right.AddText(IdermaMarca.Pie);
        hoja.PageSetup.Header.Right.AddText(IdermaMarca.NombreMayusculas);

        IdermaMarca.AplicarPropiedades(libro, documento.TituloImpresion);
        libro.SaveAs(ruta);
    }

    public static void ExportarPdf(string ruta, OrdenCompraDocumento documento)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        Document
            .Create(contenedor =>
            {
                contenedor.Page(pagina =>
                {
                    pagina.Size(PageSizes.A4.Landscape());
                    pagina.MarginHorizontal(22);
                    pagina.MarginTop(18);
                    pagina.MarginBottom(16);
                    pagina.DefaultTextStyle(x => x.FontFamily("Calibri", "Segoe UI", "Arial").FontSize(8).FontColor(Navy));
                    pagina.Header().Element(c => Encabezado(c, documento.TituloImpresion));
                    pagina.Footer().Element(c => Pie(c, documento.CodigoFormato));
                    pagina.Content().Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Background(Navy).PaddingVertical(6).AlignCenter()
                            .Text(documento.TituloImpresion)
                            .FontSize(12).Bold().FontColor(Colors.White);
                        col.Item().Element(c => CajaControl(c, documento));
                        col.Item().Element(c => Tabla(c, documento));
                        col.Item().Element(c => TotalesPdf(c, documento));
                        col.Item().Text(
                                "Precio sin IGV = último precio − 18 %. IGV = 18 % de ese precio, en la moneda registrada (soles o dólares). " +
                                "Total con IGV = último precio × cantidad a pedir. " +
                                "Consumo promedio: salidas de los últimos 6 meses ÷ 6.")
                            .FontSize(7).Italic().FontColor(Gris);
                        col.Item().PaddingTop(10).Row(firmas =>
                        {
                            firmas.RelativeItem().Text(Firma("Elaborado por", documento.ElaboradoPor)).FontSize(9);
                            firmas.RelativeItem().Text(Firma("Revisado por", documento.RevisadoPor)).FontSize(9);
                        });
                    });
                });
            })
            .WithMetadata(new DocumentMetadata
            {
                Title = documento.TituloImpresion,
                Author = IdermaMarca.Nombre,
                Creator = IdermaMarca.Sistema,
                Subject = $"{documento.CodigoFormato} · {documento.MesAnio}"
            })
            .GeneratePdf(ruta);
    }

    private static void Encabezado(IContainer contenedor, string titulo)
    {
        contenedor.Column(col =>
        {
            col.Item().Row(fila =>
            {
                var logo = IdermaMarca.RutaLogo();
                if (logo is not null)
                {
                    fila.ConstantItem(78).Height(42).Image(logo).FitArea();
                    fila.ConstantItem(8);
                }

                fila.RelativeItem().Column(texto =>
                {
                    texto.Item().Text(IdermaMarca.NombreMayusculas).FontSize(14).Bold().FontColor(Navy);
                    texto.Item().Text(IdermaMarca.Giro).FontSize(8).FontColor(Oro);
                    texto.Item().Text(titulo).FontSize(8).FontColor(Gris);
                });

                fila.ConstantItem(110).AlignRight().Column(meta =>
                {
                    meta.Item().AlignRight().Text("USO INTERNO").FontSize(8).Bold().FontColor(Oro);
                    meta.Item().AlignRight().Text($"Emisión: {DateTime.Now:dd/MM/yyyy}").FontSize(8).FontColor(Gris);
                    meta.Item().AlignRight().Text(CalculoOrdenCompra.CodigoFormato).FontSize(8).FontColor(Gris);
                });
            });

            col.Item().PaddingTop(6).Height(2).Background(Oro);
            col.Item().Height(1).Background(Navy);
        });
    }

    private static void Pie(IContainer contenedor, string codigo)
    {
        contenedor.Column(col =>
        {
            col.Item().Height(1).Background(Oro);
            col.Item().PaddingTop(4).Row(fila =>
            {
                fila.RelativeItem().Text($"{IdermaMarca.Pie}  ·  {codigo}").FontSize(7).FontColor(Gris);
                fila.ConstantItem(80).AlignRight().Text(texto =>
                {
                    texto.Span("Página ").FontSize(7).FontColor(Gris);
                    texto.CurrentPageNumber().FontSize(7).FontColor(Gris);
                    texto.Span(" de ").FontSize(7).FontColor(Gris);
                    texto.TotalPages().FontSize(7).FontColor(Gris);
                });
            });
        });
    }

    private static void CajaControl(IContainer contenedor, OrdenCompraDocumento documento)
    {
        contenedor.Border(0.8f).BorderColor(Navy).Background(Fondo).Padding(8).Column(col =>
        {
            col.Spacing(6);
            col.Item().Row(fila =>
            {
                Dato(fila, "Código", documento.CodigoFormato);
                Dato(fila, "Mes / Año", Vacio(documento.MesAnio));
                Dato(fila, "Fecha de elaboración", Vacio(documento.FechaElaboracion));
                Dato(fila, "Estado", Vacio(documento.Estado));
            });
            col.Item().Row(fila =>
            {
                Dato(fila, "Elaborado por", Vacio(documento.ElaboradoPor));
                Dato(fila, "Revisado por", Vacio(documento.RevisadoPor));
                Dato(fila, "Período de cobertura", Vacio(documento.PeriodoCobertura));
                Dato(fila, "Ítems", documento.Lineas.Count.ToString(Cultura));
            });
        });
    }

    private static void Dato(RowDescriptor fila, string etiqueta, string valor)
    {
        fila.RelativeItem().Column(col =>
        {
            col.Item().Text(etiqueta.ToUpperInvariant()).FontSize(6.5f).FontColor(Gris);
            col.Item().Text(valor).FontSize(8).Bold().FontColor(Navy);
        });
    }

    private static void Tabla(IContainer contenedor, OrdenCompraDocumento documento)
    {
        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(16);
                cols.ConstantColumn(46);
                cols.RelativeColumn(2.2f);
                cols.RelativeColumn(1.1f);
                cols.RelativeColumn(1.2f);
                cols.ConstantColumn(52);
                cols.ConstantColumn(36);
                cols.ConstantColumn(38);
                cols.ConstantColumn(38);
                cols.ConstantColumn(28);
                cols.ConstantColumn(52);
                cols.ConstantColumn(44);
                cols.ConstantColumn(52);
                cols.ConstantColumn(48);
                cols.ConstantColumn(46);
            });

            tabla.Header(h =>
            {
                GrupoPdf(h, 10, "DETALLE DEL INSUMO", Navy);
                GrupoPdf(h, 3, documento.TituloPrecios, AzulPrecios);
                GrupoPdf(h, 2, "ESTADO", Navy);
                foreach (var (texto, indice) in Columnas.Select((texto, indice) => (texto, indice)))
                {
                    h.Cell().Background(indice is >= 10 and <= 12 ? AzulPrecios : Navy).Padding(3)
                        .AlignCenter().AlignMiddle()
                        .Text(texto).FontSize(5.4f).Bold().FontColor(Colors.White);
                }
            });

            for (var i = 0; i < documento.Lineas.Count; i++)
            {
                var linea = documento.Lineas[i];
                var fondo = i % 2 == 0 ? "#FFFFFF" : Fondo;
                void Celda(string texto, bool derecha = false, bool centro = false)
                {
                    var celda = tabla.Cell().Background(fondo).BorderBottom(0.4f).BorderColor(Linea).Padding(2);
                    if (derecha)
                    {
                        celda.AlignRight().AlignMiddle().Text(texto).FontSize(6.4f).FontColor(Navy);
                        return;
                    }

                    if (centro)
                    {
                        celda.AlignCenter().AlignMiddle().Text(texto).FontSize(6.4f).FontColor(Navy);
                        return;
                    }

                    celda.AlignMiddle().Text(texto).FontSize(6.4f).FontColor(Navy);
                }

                Celda((i + 1).ToString(Cultura), centro: true);
                Celda(linea.Codigo);
                Celda(linea.Nombre);
                Celda(linea.Marca);
                Celda(linea.Proveedor);
                Celda(string.IsNullOrWhiteSpace(linea.Ruc) ? "—" : linea.Ruc);
                Celda(linea.ConsumoPromedio.ToString("0.##", Cultura), derecha: true);
                Celda(linea.StockActual.ToString("0.##", Cultura), derecha: true);
                Celda(linea.CantidadPedir.ToString("0.##", Cultura), derecha: true);
                Celda(linea.Unidad, centro: true);
                if (linea.TienePrecio)
                {
                    Celda(MonedaPrecio.Formato(linea.PrecioUnitarioSinIgv, linea.Moneda), derecha: true);
                    Celda(MonedaPrecio.Formato(linea.Igv, linea.Moneda), derecha: true);
                    Celda(MonedaPrecio.Formato(linea.TotalConIgv, linea.Moneda), derecha: true);
                }
                else
                {
                    Celda("Sin precio", centro: true);
                    Celda("—", centro: true);
                    Celda("—", centro: true);
                }

                Celda(linea.TipoPrecio, centro: true);
                Celda(linea.Estado, centro: true);
            }
        });
    }

    private static void TotalesPdf(IContainer contenedor, OrdenCompraDocumento documento)
    {
        var totales = Totales(documento).ToList();
        if (totales.Count == 0)
        {
            contenedor.AlignRight().Text("Sin precios registrados para totalizar.").FontSize(8).FontColor(Gris);
            return;
        }

        contenedor.AlignRight().Column(col =>
        {
            foreach (var total in totales)
            {
                col.Item().Text(
                        $"Total {MonedaPrecio.Simbolo(total.Moneda)}   IGV {MonedaPrecio.Formato(total.Igv, total.Moneda)}    ·    Con IGV {MonedaPrecio.Formato(total.Total, total.Moneda)}")
                    .FontSize(9).Bold().FontColor(Navy);
            }
        });
    }

    private static void GrupoPdf(TableCellDescriptor celdas, uint span, string texto, string fondo) =>
        celdas.Cell().ColumnSpan(span).Background(fondo).PaddingVertical(3).AlignCenter()
            .Text(texto).FontSize(7).Bold().FontColor(Colors.White);

    private static void Grupo(IXLWorksheet hoja, int fila, int desde, int hasta, string texto, string fondo)
    {
        var rango = hoja.Range(fila, desde, fila, hasta);
        rango.Merge();
        rango.Style.Fill.BackgroundColor = XLColor.FromHtml(fondo);
        rango.Style.Font.Bold = true;
        rango.Style.Font.FontColor = XLColor.White;
        rango.Style.Font.FontSize = 9;
        rango.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        rango.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        hoja.Cell(fila, desde).Value = texto;
    }

    private static void Meta(IXLWorksheet hoja, int fila, int desde, int hasta, string etiqueta, string valor)
    {
        var rango = hoja.Range(fila, desde, fila, hasta);
        rango.Merge();
        rango.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        rango.Style.Alignment.WrapText = false;
        hoja.Cell(fila, desde).Value = string.IsNullOrWhiteSpace(valor)
            ? $"{etiqueta}:"
            : $"{etiqueta}: {valor}";
        hoja.Cell(fila, desde).Style.Font.FontSize = 9;
    }

    private static void InsertarLogo(IXLWorksheet hoja)
    {
        var ruta = IdermaMarca.RutaLogo();
        if (ruta is null)
        {
            return;
        }

        using var flujo = new MemoryStream(File.ReadAllBytes(ruta));
        var formato = Path.GetExtension(ruta).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? XLPictureFormat.Png
            : XLPictureFormat.Jpeg;
        hoja.AddPicture(flujo, formato)
            .MoveTo(hoja.Cell(1, 1), 2, 1)
            .WithSize(96, 34);
    }

    private static IEnumerable<(string Moneda, double Igv, double Total)> Totales(OrdenCompraDocumento documento) =>
        documento.Lineas
            .Where(l => l.TienePrecio)
            .GroupBy(l => MonedaPrecio.Normalizar(l.Moneda))
            .Select(g => (
                g.Key,
                CalculoOrdenCompra.Redondear(g.Sum(l => l.Igv)),
                CalculoOrdenCompra.Redondear(g.Sum(l => l.TotalConIgv))));

    private static string Vacio(string valor) =>
        string.IsNullOrWhiteSpace(valor) ? "—" : valor.Trim();

    private static string Firma(string etiqueta, string nombre) =>
        string.IsNullOrWhiteSpace(nombre)
            ? $"{etiqueta}: ____________________"
            : $"{etiqueta}: {nombre.Trim()}";
}
