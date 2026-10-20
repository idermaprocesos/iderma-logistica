using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using IdermaFichas.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IdermaFichas.Services;

public static class HistorialPreciosExportService
{
    private static readonly string Navy = IdermaMarca.Navy;
    private static readonly string Oro = IdermaMarca.Oro;
    private static readonly string Fondo = IdermaMarca.FondoSuave;
    private static readonly string Gris = "#5A6570";

    public static void ExportarExcel(string ruta, FichaTecnica ficha, IReadOnlyList<PrecioProducto> precios)
    {
        var cronologico = Ordenar(precios);
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Historial de precios");
        string[] titulos = ["Fecha", "Precio", "Monto", "Moneda", "Variación %", "Nota"];
        var inicio = IdermaMarca.EscribirMembrete(
            hoja,
            8,
            "Reporte de historial de precios",
            $"{ficha.Codigo} · {ficha.Nombre} · {ficha.AreaTitulo}");

        var resumen = ResumenVista(cronologico);
        var filaResumen = inicio;
        hoja.Cell(filaResumen, 1).Value = "Precio actual";
        hoja.Cell(filaResumen, 2).Value = resumen.PrecioActual;
        hoja.Cell(filaResumen, 3).Value = "vs. anterior";
        hoja.Cell(filaResumen, 4).Value = resumen.VariacionAnterior;
        hoja.Cell(filaResumen + 1, 1).Value = "Precio inicial";
        hoja.Cell(filaResumen + 1, 2).Value = resumen.PrecioInicial;
        hoja.Cell(filaResumen + 1, 3).Value = "vs. inicial";
        hoja.Cell(filaResumen + 1, 4).Value = resumen.VariacionInicial;
        hoja.Range(filaResumen, 1, filaResumen + 1, 1).Style.Font.Bold = true;
        hoja.Range(filaResumen, 2, filaResumen + 1, 2).Style.Font.Bold = true;
        hoja.Range(filaResumen, 2, filaResumen + 1, 2).Style.Font.FontSize = 14;
        PintarCeldaVariacion(hoja.Cell(filaResumen, 4), resumen.VariacionAnterior);
        PintarCeldaVariacion(hoja.Cell(filaResumen + 1, 4), resumen.VariacionInicial);
        inicio = filaResumen + 3;

        for (var i = 0; i < titulos.Length; i++)
        {
            hoja.Cell(inicio, i + 1).Value = titulos[i];
        }

        var encabezado = hoja.Range(inicio, 1, inicio, titulos.Length);
        encabezado.Style.Font.Bold = true;
        encabezado.Style.Font.FontColor = XLColor.White;
        encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml(Navy);

        for (var i = 0; i < cronologico.Count; i++)
        {
            var precio = cronologico[i];
            var fila = inicio + 1 + i;
            hoja.Cell(fila, 1).Value = precio.Fecha.ToLocalTime().DateTime;
            hoja.Cell(fila, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            hoja.Cell(fila, 2).Value = MonedaPrecio.Formato(precio.Monto, precio.Moneda);
            hoja.Cell(fila, 3).Value = precio.Monto;
            hoja.Cell(fila, 3).Style.NumberFormat.Format = "0.00";
            hoja.Cell(fila, 4).Value = MonedaPrecio.Etiqueta(precio.Moneda);
            hoja.Cell(fila, 5).Value = TextoVariacion(cronologico, i);
            hoja.Cell(fila, 6).Value = precio.Nota;
        }

        var filaGrafico = inicio + 1 + Math.Max(cronologico.Count, 1) + 2;
        hoja.Cell(filaGrafico, 1).Value = "Estadística lineal (subidas en rojo, bajadas en verde)";
        hoja.Cell(filaGrafico, 1).Style.Font.Bold = true;
        hoja.Cell(filaGrafico, 1).Style.Font.FontColor = XLColor.FromHtml(Navy);

        var png = GraficoHistorialPrecios.GenerarPng(cronologico);
        var altoGrafico = Math.Max(1, cronologico.Select(p => MonedaPrecio.Normalizar(p.Moneda)).Distinct().Count()) * 280;
        using var flujo = new MemoryStream(png);
        hoja.AddPicture(flujo, XLPictureFormat.Png)
            .MoveTo(hoja.Cell(filaGrafico + 1, 1))
            .WithSize(720, altoGrafico);

        IdermaMarca.EscribirPieTabla(hoja, filaGrafico + 14, 8);
        hoja.SheetView.FreezeRows(inicio);
        hoja.Columns(1, 6).AdjustToContents();
        IdermaMarca.AplicarPropiedades(libro, $"Historial de precios · {ficha.Codigo}");
        libro.SaveAs(ruta);
    }

    public static void ExportarPdf(string ruta, FichaTecnica ficha, IReadOnlyList<PrecioProducto> precios)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var cronologico = Ordenar(precios);
        var png = GraficoHistorialPrecios.GenerarPng(cronologico);
        Document
            .Create(contenedor =>
            {
                contenedor.Page(pagina =>
                {
                    pagina.Size(PageSizes.A4);
                    pagina.MarginHorizontal(32);
                    pagina.MarginTop(24);
                    pagina.MarginBottom(20);
                    pagina.DefaultTextStyle(x => x.FontFamily("Calibri", "Segoe UI", "Arial").FontSize(9).FontColor(Navy));
                    pagina.Header().Element(Encabezado);
                    pagina.Footer().Element(Pie);
                    pagina.Content().Column(col =>
                    {
                        col.Spacing(10);
                        col.Item().Text("REPORTE DE HISTORIAL DE PRECIOS").FontSize(13).Bold();
                        col.Item().Text($"{ficha.Codigo} · {ficha.Nombre} · {ficha.AreaTitulo}")
                            .FontSize(9).FontColor(Gris);
                        col.Item().Text(Resumen(cronologico)).FontSize(9).FontColor(Gris);
                        col.Item().Element(c => CajaResumen(c, cronologico));
                        col.Item().Text("Estadística lineal de subidas y bajadas")
                            .FontSize(10).Bold();
                        col.Item().Border(0.6f).BorderColor(Oro).Padding(6).MaxHeight(280)
                            .Image(png).FitArea().WithCompressionQuality(ImageCompressionQuality.Best);
                        col.Item().Element(c => Tabla(c, cronologico));
                    });
                });
            })
            .WithMetadata(new DocumentMetadata
            {
                Title = $"Historial de precios · {ficha.Codigo}",
                Author = IdermaMarca.Nombre,
                Creator = IdermaMarca.Sistema,
                Subject = ficha.Nombre
            })
            .GeneratePdf(ruta);
    }

    private static List<PrecioProducto> Ordenar(IReadOnlyList<PrecioProducto> precios) =>
        precios.OrderBy(p => p.Fecha).ThenBy(p => p.Id).ToList();

    private sealed record DatosResumenPrecios(
        string PrecioActual,
        string VariacionAnterior,
        string PrecioInicial,
        string VariacionInicial);

    private static DatosResumenPrecios ResumenVista(IReadOnlyList<PrecioProducto> cronologico)
    {
        if (cronologico.Count == 0)
        {
            return new("Sin precio", "—", "Sin precio", "—");
        }

        var ultimo = cronologico[^1];
        var moneda = MonedaPrecio.Normalizar(ultimo.Moneda);
        var serie = cronologico.Where(p => MonedaPrecio.Normalizar(p.Moneda) == moneda).ToList();
        var inicial = serie[0];
        var anterior = serie.Count > 1 ? serie[^2] : null;
        return new(
            MonedaPrecio.Formato(ultimo.Monto, ultimo.Moneda),
            Comparar(ultimo, anterior, serie.Count == 1 ? "Primer registro" : "Sin % comparable"),
            MonedaPrecio.Formato(inicial.Monto, inicial.Moneda),
            Comparar(ultimo, inicial, "Sin cambio desde el inicio"));
    }

    public static string TextoVariacion(IReadOnlyList<PrecioProducto> cronologico, int indice)
    {
        var actual = cronologico[indice];
        var anterior = cronologico
            .Take(indice)
            .LastOrDefault(p => MonedaPrecio.Normalizar(p.Moneda) == MonedaPrecio.Normalizar(actual.Moneda));
        return Comparar(actual, anterior, "—");
    }

    private static string Comparar(PrecioProducto actual, PrecioProducto? referencia, string sinDato)
    {
        if (referencia is null || referencia.Monto <= 0 || referencia.Id == actual.Id)
        {
            return sinDato;
        }

        var porcentaje = (actual.Monto - referencia.Monto) / referencia.Monto * 100;
        var cultura = System.Globalization.CultureInfo.GetCultureInfo("es-PE");
        if (porcentaje < -0.05)
        {
            return $"↓ {Math.Abs(porcentaje).ToString("N1", cultura)} %";
        }

        if (porcentaje > 0.05)
        {
            return $"↑ {porcentaje.ToString("N1", cultura)} %";
        }

        return "0 %";
    }

    private static void PintarCeldaVariacion(IXLCell celda, string texto)
    {
        celda.Style.Font.Bold = true;
        if (texto.StartsWith("↓", StringComparison.Ordinal))
        {
            celda.Style.Font.FontColor = XLColor.FromHtml("#2E7D4F");
        }
        else if (texto.StartsWith("↑", StringComparison.Ordinal))
        {
            celda.Style.Font.FontColor = XLColor.FromHtml("#C42B1C");
        }
    }

    private static void CajaResumen(IContainer contenedor, IReadOnlyList<PrecioProducto> cronologico)
    {
        var resumen = ResumenVista(cronologico);
        contenedor.Border(0.6f).BorderColor(Oro).Background(Fondo).Padding(8).Table(tabla =>
        {
            tabla.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(1.1f);
                cols.RelativeColumn(1.4f);
                cols.RelativeColumn(1.1f);
                cols.RelativeColumn(1.2f);
            });

            FilaResumen(tabla, "Precio actual", resumen.PrecioActual, "vs. anterior", resumen.VariacionAnterior);
            FilaResumen(tabla, "Precio inicial", resumen.PrecioInicial, "vs. inicial", resumen.VariacionInicial);
        });
    }

    private static void FilaResumen(TableDescriptor tabla, string etiqueta, string valor, string etiquetaVar, string variacion)
    {
        tabla.Cell().Padding(3).Text(etiqueta).FontSize(8).FontColor(Gris);
        tabla.Cell().Padding(3).Text(valor).FontSize(12).Bold();
        tabla.Cell().Padding(3).AlignMiddle().Text(etiquetaVar).FontSize(8).FontColor(Gris);
        tabla.Cell().Padding(3).AlignMiddle().Text(texto =>
        {
            var color = variacion.StartsWith("↓", StringComparison.Ordinal)
                ? "#2E7D4F"
                : variacion.StartsWith("↑", StringComparison.Ordinal)
                    ? "#C42B1C"
                    : Gris;
            texto.Span(variacion).FontSize(12).Bold().FontColor(color);
        });
    }

    private static string Resumen(IReadOnlyList<PrecioProducto> cronologico)
    {
        if (cronologico.Count == 0)
        {
            return "Sin registros de precio.";
        }

        var ultimo = cronologico[^1];
        var serie = cronologico.Where(p => MonedaPrecio.Normalizar(p.Moneda) == MonedaPrecio.Normalizar(ultimo.Moneda)).ToList();
        var min = serie.Min(p => p.Monto);
        var max = serie.Max(p => p.Monto);
        return $"{cronologico.Count} registro(s) · actual {MonedaPrecio.Formato(ultimo.Monto, ultimo.Moneda)} · " +
               $"mín. {MonedaPrecio.Formato(min, ultimo.Moneda)} · máx. {MonedaPrecio.Formato(max, ultimo.Moneda)}";
    }

    private static void Encabezado(IContainer contenedor)
    {
        contenedor.Column(col =>
        {
            col.Item().Row(fila =>
            {
                var logo = IdermaMarca.RutaLogo();
                if (logo is not null)
                {
                    fila.ConstantItem(80).Height(48).Image(logo).FitArea();
                    fila.ConstantItem(10);
                }

                fila.RelativeItem().Column(texto =>
                {
                    texto.Item().Text(IdermaMarca.NombreMayusculas).FontSize(14).Bold().FontColor(Navy);
                    texto.Item().Text(IdermaMarca.Giro).FontSize(8).FontColor(Oro);
                    texto.Item().Text("Historial de precios por producto").FontSize(8).FontColor(Gris);
                });
            });
            col.Item().PaddingTop(6).Height(2).Background(Oro);
            col.Item().Height(1).Background(Navy);
        });
    }

    private static void Pie(IContainer contenedor)
    {
        contenedor.Column(col =>
        {
            col.Item().Height(1).Background(Oro);
            col.Item().PaddingTop(4).Row(fila =>
            {
                fila.RelativeItem().Text(IdermaMarca.Pie).FontSize(7).FontColor(Gris);
                fila.ConstantItem(90).AlignRight().Text(texto =>
                {
                    texto.Span("Página ").FontSize(7).FontColor(Gris);
                    texto.CurrentPageNumber().FontSize(7).FontColor(Gris);
                    texto.Span(" de ").FontSize(7).FontColor(Gris);
                    texto.TotalPages().FontSize(7).FontColor(Gris);
                });
            });
        });
    }

    private static void Tabla(IContainer contenedor, IReadOnlyList<PrecioProducto> cronologico)
    {
        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(78);
                cols.ConstantColumn(92);
                cols.RelativeColumn(1.1f);
                cols.ConstantColumn(72);
                cols.RelativeColumn(1.4f);
            });

            tabla.Header(h =>
            {
                Cabeza(h, "Fecha");
                Cabeza(h, "Precio");
                Cabeza(h, "Moneda");
                Cabeza(h, "Variación");
                Cabeza(h, "Nota");
            });

            for (var i = 0; i < cronologico.Count; i++)
            {
                var precio = cronologico[i];
                var fondo = i % 2 == 0 ? Fondo : "#FFFFFF";
                Celda(tabla, precio.Fecha.ToString("dd/MM/yyyy"), fondo);
                Celda(tabla, MonedaPrecio.Formato(precio.Monto, precio.Moneda), fondo);
                Celda(tabla, MonedaPrecio.Etiqueta(precio.Moneda), fondo);
                Celda(tabla, TextoVariacion(cronologico, i), fondo);
                Celda(tabla, string.IsNullOrWhiteSpace(precio.Nota) ? "—" : precio.Nota, fondo);
            }
        });
    }

    private static void Cabeza(TableCellDescriptor header, string texto) =>
        header.Cell().Background(Navy).PaddingVertical(6).PaddingHorizontal(5)
            .AlignMiddle().Text(texto).FontSize(8).Bold().FontColor(Colors.White);

    private static void Celda(TableDescriptor tabla, string texto, string fondo) =>
        tabla.Cell().Background(fondo).BorderBottom(0.5f).BorderColor("#D8D2C6")
            .PaddingVertical(5).PaddingHorizontal(5).AlignMiddle()
            .Text(texto).FontSize(8).FontColor(Navy);
}
