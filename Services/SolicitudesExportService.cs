using System.Globalization;
using ClosedXML.Excel;
using IdermaFichas.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IdermaFichas.Services;

public static class SolicitudesExportService
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    public static void ExportarExcel(string ruta, IReadOnlyList<SolicitudClinica> solicitudes)
    {
        var filas = new List<string[]>();
        foreach (var s in solicitudes)
        {
            if (s.Items.Count == 0)
            {
                filas.Add(FilaClinica(s, null));
                continue;
            }

            foreach (var linea in s.Items)
            {
                filas.Add(FilaClinica(s, linea));
            }
        }

        GuardarExcel(
            ruta,
            "Clínica",
            ["Folio", "Fecha", "Persona", "Urgente", "Estado", "Código", "Área", "Producto", "Pedidas", "Respondidas", "Fecha respuesta", "Nota"],
            filas);
    }

    public static void ExportarPdf(string ruta, IReadOnlyList<SolicitudClinica> solicitudes)
    {
        var filas = new List<string[]>();
        foreach (var s in solicitudes)
        {
            var lineas = s.Items.Count == 0 ? [null] : s.Items.Cast<LineaSolicitudClinica?>().ToList();
            foreach (var linea in lineas)
            {
                var producto = linea is null
                    ? "Sin productos"
                    : Unir(linea.Codigo, linea.Area, linea.Nombre);
                var pedidas = linea?.Cantidad.ToString(Cultura) ?? "";
                var respondidas = linea?.CantidadRespuesta?.ToString(Cultura) ?? "";
                filas.Add(
                [
                    s.Folio,
                    s.FechaTexto,
                    s.Enfermera + (s.Urgente ? " · Urgente" : ""),
                    s.EstadoTexto,
                    producto,
                    pedidas,
                    respondidas,
                    NotaClinica(s)
                ]);
            }
        }

        GuardarPdf(
            ruta,
            "Solicitudes de clínica",
            solicitudes.Count,
            filas.Count,
            [58, 72, 90, 62, 0, 36, 42, 0],
            ["Folio", "Fecha", "Persona", "Estado", "Producto", "Ped.", "Resp.", "Nota"],
            filas);
    }

    public static void ExportarExcel(string ruta, IReadOnlyList<SolicitudIncidencia> solicitudes)
    {
        var filas = solicitudes.Select(s => new[]
        {
            s.Folio,
            s.FechaTexto,
            s.Solicitante,
            s.Area,
            s.TipoTexto,
            s.Urgente ? "Sí" : "No",
            s.EstadoTexto,
            s.Nombre,
            s.Cantidad.ToString("0.##", Cultura),
            s.Unidad,
            s.PrecioReposicion > 0 ? s.PrecioReposicion.ToString("0.00", Cultura) : "",
            s.Enlace,
            s.DetalleTexto,
            s.OrdenId,
            s.EstadoOrden,
            s.FechaRespuesta?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "",
            s.Motivo
        }).ToList();

        GuardarExcel(
            ruta,
            "Incidencias",
            ["Folio", "Fecha", "Solicitante", "Área", "Tipo", "Urgente", "Estado", "Nombre", "Cantidad", "Unidad", "Precio reposición", "Enlace", "Detalle", "Orden", "Estado orden", "Fecha respuesta", "Motivo"],
            filas);
    }

    public static void ExportarPdf(string ruta, IReadOnlyList<SolicitudIncidencia> solicitudes)
    {
        var filas = solicitudes.Select(s => new[]
        {
            s.Folio,
            s.FechaTexto,
            Unir(s.Solicitante, s.Area, s.TipoTexto) + (s.Urgente ? " · Urgente" : ""),
            s.EstadoTexto,
            Unir(s.Nombre, $"{s.Cantidad.ToString("0.##", Cultura)} {s.Unidad}".Trim(), s.PrecioReposicion > 0 ? s.PrecioReposicion.ToString("C", Cultura) : ""),
            Unir(s.DetalleTexto, s.Enlace),
            Unir(s.EstadoOrden, s.OrdenId),
            NotaIncidencia(s)
        }).ToList();

        GuardarPdf(
            ruta,
            "Reportes de incidencias",
            solicitudes.Count,
            filas.Count,
            [58, 72, 110, 58, 0, 0, 70, 90],
            ["Folio", "Fecha", "Quién", "Estado", "Ítem", "Detalle", "Orden", "Respuesta"],
            filas);
    }

    public static void ExportarExcel(string ruta, IReadOnlyList<AvisoAutorizacion> avisos)
    {
        var filas = avisos.Select(a => new[]
        {
            a.Folio,
            a.FechaTexto,
            a.Solicitante,
            a.EstadoTexto,
            a.Resumen,
            a.Detalle,
            a.Respuesta
        }).ToList();

        GuardarExcel(
            ruta,
            "Autorizaciones",
            ["Folio", "Fecha", "Solicitante", "Estado", "Resumen", "Productos", "Respuesta"],
            filas);
    }

    public static void ExportarPdf(string ruta, IReadOnlyList<AvisoAutorizacion> avisos)
    {
        var filas = avisos.Select(a => new[]
        {
            a.Folio,
            a.FechaTexto,
            a.Solicitante,
            a.EstadoTexto,
            a.Resumen,
            a.Detalle,
            a.Respuesta
        }).ToList();

        GuardarPdf(
            ruta,
            "Autorizaciones de base de datos",
            avisos.Count,
            filas.Count,
            [58, 72, 80, 62, 0, 0, 120],
            ["Folio", "Fecha", "Solicitante", "Estado", "Resumen", "Productos", "Respuesta"],
            filas);
    }

    private static string[] FilaClinica(SolicitudClinica s, LineaSolicitudClinica? linea) =>
    [
        s.Folio,
        s.FechaTexto,
        s.Enfermera,
        s.Urgente ? "Sí" : "No",
        s.EstadoTexto,
        linea?.Codigo ?? "",
        linea?.Area ?? "",
        linea?.Nombre ?? "",
        linea?.Cantidad.ToString(Cultura) ?? "",
        linea?.CantidadRespuesta?.ToString(Cultura) ?? "",
        s.FechaRespuesta?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "",
        NotaClinica(s)
    ];

    private static string NotaClinica(SolicitudClinica s) =>
        s.EstaPendiente ? "" : s.ResumenRespuesta;

    private static string NotaIncidencia(SolicitudIncidencia s)
    {
        if (s.EstaPendiente)
        {
            return "";
        }

        var fecha = s.FechaRespuesta?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "";
        return Unir(s.EstadoTexto, fecha, s.Motivo);
    }

    private static string Unir(params string?[] partes) =>
        string.Join(" · ", partes.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    private static void GuardarExcel(string ruta, string hojaNombre, string[] titulos, IReadOnlyList<string[]> filas)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet(hojaNombre);
        for (var i = 0; i < titulos.Length; i++)
        {
            hoja.Cell(1, i + 1).Value = titulos[i];
        }

        var encabezado = hoja.Range(1, 1, 1, titulos.Length);
        encabezado.Style.Font.Bold = true;
        encabezado.Style.Font.FontSize = 9;
        encabezado.Style.Font.FontColor = XLColor.White;
        encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F766E");
        encabezado.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        for (var i = 0; i < filas.Count; i++)
        {
            for (var c = 0; c < titulos.Length; c++)
            {
                var celda = hoja.Cell(i + 2, c + 1);
                celda.Value = c < filas[i].Length ? filas[i][c] : "";
                celda.Style.Font.FontSize = 9;
                celda.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                celda.Style.Alignment.WrapText = true;
            }
        }

        hoja.SheetView.FreezeRows(1);
        hoja.RangeUsed()?.SetAutoFilter();
        hoja.Rows().Height = 16;
        hoja.Row(1).Height = 18;
        hoja.Columns().AdjustToContents(8, 42);
        libro.SaveAs(ruta);
    }

    private static void GuardarPdf(
        string ruta,
        string titulo,
        int registros,
        int lineas,
        float[] anchos,
        string[] titulos,
        IReadOnlyList<string[]> filas)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(PageSizes.A4.Landscape());
                pagina.Margin(16);
                pagina.DefaultTextStyle(x => x.FontSize(8).FontColor("#1A1A1A"));
                pagina.Header().Column(col =>
                {
                    col.Item().Row(fila =>
                    {
                        fila.RelativeItem().Text($"Notificaciones · {titulo}").SemiBold().FontSize(12).FontColor(IdermaMarca.Navy);
                        fila.ConstantItem(220).AlignRight().Text($"{DateTime.Now:dd/MM/yyyy HH:mm} · {registros} registros · {lineas} líneas")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken2);
                    });
                    col.Item().PaddingTop(4).LineHorizontal(0.6f).LineColor(IdermaMarca.Oro);
                });

                pagina.Content().PaddingTop(6).Table(tabla =>
                {
                    tabla.ColumnsDefinition(c =>
                    {
                        foreach (var ancho in anchos)
                        {
                            if (ancho <= 0)
                            {
                                c.RelativeColumn();
                            }
                            else
                            {
                                c.ConstantColumn(ancho);
                            }
                        }
                    });

                    tabla.Header(h =>
                    {
                        foreach (var texto in titulos)
                        {
                            h.Cell().Background(IdermaMarca.Navy).Padding(2).Text(texto).FontSize(7).FontColor(Colors.White).SemiBold();
                        }
                    });

                    if (filas.Count == 0)
                    {
                        tabla.Cell().ColumnSpan((uint)titulos.Length).Padding(4).Text("No hay registros.");
                        return;
                    }

                    for (var i = 0; i < filas.Count; i++)
                    {
                        var fondo = i % 2 == 0 ? "#FFFFFF" : "#F6F4F0";
                        foreach (var texto in filas[i])
                        {
                            tabla.Cell()
                                .Background(fondo)
                                .BorderBottom(0.3f)
                                .BorderColor("#E4E0D8")
                                .Padding(2)
                                .Text(string.IsNullOrWhiteSpace(texto) ? "—" : texto)
                                .FontSize(7);
                        }
                    }
                });

                pagina.Footer().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Darken2));
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf(ruta);
    }
}
