using System.Globalization;
using IdermaFichas.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IdermaFichas.Services;

public readonly record struct RangoMerma(DateTimeOffset Desde, DateTimeOffset Hasta, string Titulo)
{
    public bool Contiene(DateTimeOffset fecha)
    {
        var local = fecha.ToLocalTime();
        return local >= Desde && local < Hasta;
    }

    public static RangoMerma De(string periodo, DateTimeOffset? desdeCustom, DateTimeOffset? hastaCustom)
    {
        var hoy = DateTime.Today;
        return periodo switch
        {
            "Ayer" => new RangoMerma(hoy.AddDays(-1), hoy, "Ayer"),
            "Esta semana" => Semana(hoy),
            "Este mes" => new RangoMerma(new DateTime(hoy.Year, hoy.Month, 1), new DateTime(hoy.Year, hoy.Month, 1).AddMonths(1), "Este mes"),
            "Personalizado" => Personalizado(desdeCustom, hastaCustom),
            _ => new RangoMerma(hoy, hoy.AddDays(1), "Hoy")
        };
    }

    private static RangoMerma Semana(DateTime hoy)
    {
        var lunes = hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7));
        return new RangoMerma(lunes, lunes.AddDays(7), "Esta semana");
    }

    private static RangoMerma Personalizado(DateTimeOffset? desdeCustom, DateTimeOffset? hastaCustom)
    {
        var desde = (desdeCustom ?? DateTimeOffset.Now).LocalDateTime.Date;
        var hasta = (hastaCustom ?? desdeCustom ?? DateTimeOffset.Now).LocalDateTime.Date;
        if (hasta < desde)
        {
            (desde, hasta) = (hasta, desde);
        }

        var titulo = desde == hasta
            ? desde.ToString("dd/MM/yyyy")
            : $"{desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}";
        return new RangoMerma(desde, hasta.AddDays(1), titulo);
    }
}

public static class MermaReporte
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    public static void ExportarPdf(string ruta, string periodo, IReadOnlyList<MermaRegistrada> lineas)
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
                    col.Item().Text($"REPORTE DE MERMA · {periodo}").FontSize(11).FontColor(IdermaMarca.Oro);
                });
                pagina.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text($"{lineas.Count} movimiento(s)");
                    col.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(110);
                            c.ConstantColumn(70);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1.2f);
                            c.ConstantColumn(78);
                            c.ConstantColumn(70);
                            c.RelativeColumn(1.6f);
                        });
                        tabla.Header(h =>
                        {
                            foreach (var titulo in new[] { "Fecha", "Código", "Producto", "Lote", "Caducidad", "Cantidad", "Motivo" })
                            {
                                h.Cell().Background(IdermaMarca.Navy).Padding(4).Text(titulo).FontSize(8).FontColor(Colors.White).Bold();
                            }
                        });
                        foreach (var linea in lineas.OrderBy(l => l.Fecha))
                        {
                            var motivo = string.IsNullOrWhiteSpace(linea.Detalle) ? linea.Motivo : $"{linea.Motivo}. {linea.Detalle}";
                            foreach (var texto in new[]
                                     {
                                         linea.Fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                                         linea.Codigo,
                                         linea.Nombre,
                                         string.IsNullOrWhiteSpace(linea.Lote) ? "Sin lote" : linea.Lote,
                                         linea.FechaCaducidad?.ToLocalTime().ToString("dd/MM/yyyy") ?? "Sin caducidad",
                                         $"{linea.Cantidad.ToString("0.##", Cultura)} {linea.Unidad}".Trim(),
                                         motivo
                                     })
                            {
                                tabla.Cell().BorderBottom(0.4f).BorderColor("#E4E0D8").Padding(3).Text(texto).FontSize(8);
                            }
                        }
                    });
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
}
