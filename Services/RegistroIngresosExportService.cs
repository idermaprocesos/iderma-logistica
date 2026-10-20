using ClosedXML.Excel;
using IdermaFichas.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IdermaFichas.Services;

public sealed record LineaRegistroIngreso(FichaTecnica Ficha, MovimientoInventario Movimiento);

public static class RegistroIngresosExportService
{
    private static readonly string Navy = IdermaMarca.Navy;
    private static readonly string Oro = IdermaMarca.Oro;
    private static readonly string Fondo = IdermaMarca.FondoSuave;
    private static readonly string Gris = "#5A6570";

    public static void ExportarExcel(string ruta, IReadOnlyList<LineaRegistroIngreso> lineas, string periodo)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Movimientos");
        string[] titulos =
        [
            "Área", "Código", "Producto", "Fecha", "Tipo", "Lote", "Caducidad", "Cantidad", "Unidad",
            "Existencia resultante", "Documento", "Número", "Nota", "Recibo"
        ];

        var inicio = IdermaMarca.EscribirMembrete(
            hoja,
            titulos.Length,
            "Registro de movimientos",
            $"{lineas.Count} movimiento(s) · {periodo}");

        for (var i = 0; i < titulos.Length; i++)
        {
            hoja.Cell(inicio, i + 1).Value = titulos[i];
        }

        var encabezado = hoja.Range(inicio, 1, inicio, titulos.Length);
        encabezado.Style.Font.Bold = true;
        encabezado.Style.Font.FontColor = XLColor.White;
        encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml(Navy);

        for (var i = 0; i < lineas.Count; i++)
        {
            var linea = lineas[i];
            var f = linea.Ficha;
            var m = linea.Movimiento;
            var fila = inicio + 1 + i;
            hoja.Cell(fila, 1).Value = f.AreaTitulo;
            hoja.Cell(fila, 2).Value = f.Codigo;
            hoja.Cell(fila, 3).Value = f.Nombre;
            hoja.Cell(fila, 4).Value = m.Fecha.ToLocalTime().DateTime;
            hoja.Cell(fila, 4).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            hoja.Cell(fila, 5).Value = m.EsSalida ? "Salida" : "Ingreso";
            hoja.Cell(fila, 6).Value = m.Lote;
            hoja.Cell(fila, 7).Value = m.FechaCaducidad?.ToString("dd/MM/yyyy") ?? string.Empty;
            hoja.Cell(fila, 8).Value = m.EsSalida ? -m.Cantidad : m.Cantidad;
            hoja.Cell(fila, 9).Value = f.UnidadMedida;
            hoja.Cell(fila, 10).Value = m.ExistenciaResultante;
            hoja.Cell(fila, 11).Value = m.TipoDocumento;
            hoja.Cell(fila, 12).Value = m.NumeroDocumento;
            hoja.Cell(fila, 13).Value = m.Nota;
            var rutaRecibo = ReciboMovimientoService.RutaEfectiva(m);
            hoja.Cell(fila, 14).Value = string.IsNullOrWhiteSpace(rutaRecibo)
                ? "Sin recibo"
                : ReciboMovimientoService.NombreExportacion(f, m);
        }

        IdermaMarca.EscribirPieTabla(hoja, inicio + 1 + lineas.Count + 1, titulos.Length);
        hoja.SheetView.FreezeRows(inicio);
        hoja.Columns().AdjustToContents();
        IdermaMarca.AplicarPropiedades(libro, $"Registro de movimientos · {IdermaMarca.Nombre}");
        libro.SaveAs(ruta);
    }

    public static void ExportarPdf(string ruta, IReadOnlyList<LineaRegistroIngreso> lineas, string periodo)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        Document
            .Create(contenedor =>
            {
                contenedor.Page(pagina =>
                {
                    pagina.Size(PageSizes.A4.Landscape());
                    pagina.MarginHorizontal(28);
                    pagina.MarginTop(24);
                    pagina.MarginBottom(20);
                    pagina.DefaultTextStyle(x => x.FontFamily("Calibri", "Segoe UI", "Arial").FontSize(8).FontColor(Navy));
                    pagina.Header().Element(Encabezado);
                    pagina.Footer().Element(Pie);
                    pagina.Content().Column(col =>
                    {
                        col.Spacing(10);
                        col.Item().Text("REGISTRO DE MOVIMIENTOS").FontSize(13).Bold().FontColor(Navy);
                        col.Item().Text($"{lineas.Count} movimiento(s) · {periodo}")
                            .FontSize(9).FontColor(Gris);
                        col.Item().Element(c => Tabla(c, lineas));
                        foreach (var linea in lineas.Where(l => ReciboMovimientoService.RutaEfectiva(l.Movimiento) is not null))
                        {
                            col.Item().PageBreak();
                            col.Item().Element(c => PaginaRecibo(c, linea));
                        }
                    });
                });
            })
            .WithMetadata(new DocumentMetadata
            {
                Title = "Registro de movimientos",
                Author = IdermaMarca.Nombre,
                Creator = IdermaMarca.Sistema,
                Subject = periodo
            })
            .GeneratePdf(ruta);
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
                    texto.Item().Text("Historial de ingresos y salidas").FontSize(8).FontColor(Gris);
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

    private static void Tabla(IContainer contenedor, IReadOnlyList<LineaRegistroIngreso> lineas)
    {
        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(1.2f);
                cols.ConstantColumn(48);
                cols.RelativeColumn(1.8f);
                cols.ConstantColumn(70);
                cols.ConstantColumn(44);
                cols.ConstantColumn(48);
                cols.ConstantColumn(48);
                cols.RelativeColumn(1.3f);
                cols.RelativeColumn(1.2f);
                cols.RelativeColumn(1.1f);
            });

            tabla.Header(h =>
            {
                void Cabeza(string texto) =>
                    h.Cell().Background(Navy).Padding(4)
                        .Text(texto).FontSize(7.5f).Bold().FontColor(Colors.White);

                Cabeza("Área");
                Cabeza("Código");
                Cabeza("Producto");
                Cabeza("Fecha");
                Cabeza("Tipo");
                Cabeza("Cantidad");
                Cabeza("Quedaron");
                Cabeza("Documento");
                Cabeza("Nota");
                Cabeza("Recibo");
            });

            for (var i = 0; i < lineas.Count; i++)
            {
                var linea = lineas[i];
                var fondo = i % 2 == 0 ? Fondo : "#FFFFFF";
                Celda(tabla, linea.Ficha.AreaTitulo, fondo);
                Celda(tabla, linea.Ficha.Codigo, fondo);
                Celda(tabla, linea.Ficha.Nombre, fondo);
                Celda(tabla, linea.Movimiento.Fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), fondo);
                Celda(tabla, linea.Movimiento.EsSalida ? "Salida" : "Ingreso", fondo);
                Celda(tabla, $"{(linea.Movimiento.EsSalida ? "-" : "+")}{linea.Movimiento.Cantidad:0.##} {linea.Ficha.UnidadMedida}".Trim(), fondo);
                Celda(tabla, $"{linea.Movimiento.ExistenciaResultante:0.##}", fondo);
                Celda(tabla, $"{linea.Movimiento.TipoDocumento} {linea.Movimiento.NumeroDocumento}".Trim(), fondo);
                Celda(tabla, linea.Movimiento.Nota, fondo);
                Celda(tabla, ReciboMovimientoService.RutaEfectiva(linea.Movimiento) is null
                    ? "No"
                    : ReciboMovimientoService.NombreExportacion(linea.Ficha, linea.Movimiento), fondo);
            }
        });
    }

    private static void PaginaRecibo(IContainer contenedor, LineaRegistroIngreso linea)
    {
        var m = linea.Movimiento;
        var f = linea.Ficha;
        var ruta = ReciboMovimientoService.RutaEfectiva(m);
        contenedor.Column(col =>
        {
            col.Spacing(8);
            col.Item().Text($"RECIBO · {m.DocumentoResumen}").FontSize(12).Bold().FontColor(Navy);
            col.Item().Text($"{f.Codigo} · {f.Nombre} · {f.AreaTitulo}").FontSize(9).FontColor(Gris);
            col.Item().Text(
                    $"{m.Fecha.ToLocalTime():dd/MM/yyyy HH:mm}  ·  {(m.EsSalida ? "Salida" : "Ingreso")}  ·  " +
                    $"{(m.EsSalida ? "-" : "+")}{m.Cantidad:0.##} {f.UnidadMedida}".Trim())
                .FontSize(9);
            if (!string.IsNullOrWhiteSpace(m.Nota))
            {
                col.Item().Text($"Nota: {m.Nota}").FontSize(8).FontColor(Gris);
            }

            if (ruta is null)
            {
                return;
            }

            if (ReciboMovimientoService.EsImagen(ruta))
            {
                col.Item().MaxHeight(420).Image(ruta).FitArea();
            }
            else
            {
                col.Item().Text($"El recibo ({Path.GetFileName(ruta)}) se copió a la carpeta de recibos junto a este PDF.")
                    .FontSize(9).Italic().FontColor(Gris);
            }
        });
    }

    public static string? CopiarRecibos(string rutaDocumento, IReadOnlyList<LineaRegistroIngreso> lineas)
    {
        var carpetaDocumento = Path.GetDirectoryName(rutaDocumento);
        if (string.IsNullOrWhiteSpace(carpetaDocumento))
        {
            return null;
        }

        var destino = Path.Combine(carpetaDocumento, Path.GetFileNameWithoutExtension(rutaDocumento) + "-recibos");
        var copiados = 0;
        foreach (var linea in lineas)
        {
            var origen = ReciboMovimientoService.RutaEfectiva(linea.Movimiento);
            if (origen is null)
            {
                continue;
            }

            Directory.CreateDirectory(destino);
            var nombre = ReciboMovimientoService.NombreExportacion(linea.Ficha, linea.Movimiento);
            File.Copy(origen, Path.Combine(destino, nombre), overwrite: true);
            copiados++;
        }

        return copiados == 0 ? null : destino;
    }

    private static void Celda(TableDescriptor tabla, string texto, string fondo) =>
        tabla.Cell().Background(fondo).BorderBottom(0.4f).BorderColor("#D8D2C6").Padding(4)
            .Text(texto).FontSize(7.5f);
}
