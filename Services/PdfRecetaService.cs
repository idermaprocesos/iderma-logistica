using IdermaFichas.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IdermaFichas.Services;

public static class PdfRecetaService
{
    private static readonly string Navy = IdermaMarca.Navy;
    private static readonly string Oro = IdermaMarca.Oro;
    private static readonly string Fondo = IdermaMarca.FondoSuave;
    private static readonly string Gris = "#5A6570";
    private static readonly string Linea = "#D8D2C6";

    public static void Exportar(
        string ruta,
        Doctor doctor,
        ListaReceta lista,
        RecetaEmitida receta,
        IReadOnlyList<RatioMedicamento> ratios)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        try
        {
            var fhir = SmartHealthLinkService.Crear(doctor, lista, receta);
            File.WriteAllText(Path.ChangeExtension(ruta, ".fhir.json"), fhir.FichaSmartHealthCard);
        }
        catch
        {
        }
        Document
            .Create(contenedor =>
            {
                contenedor.Page(pagina =>
                {
                    pagina.Size(PageSizes.A4);
                    pagina.MarginHorizontal(32);
                    pagina.MarginTop(22);
                    pagina.MarginBottom(24);
                    pagina.DefaultTextStyle(x => x.FontFamily("Calibri", "Segoe UI", "Arial").FontSize(8.5f).FontColor(Navy));
                    pagina.Header().Element(Encabezado);
                    pagina.Footer().Element(Pie);
                    pagina.Content().Column(col =>
                    {
                        col.Spacing(6);
                        col.Item().Text("RECETA MÉDICA").FontSize(12).Bold().FontColor(Navy);
                        col.Item().Text($"{receta.Numero}  ·  {receta.Fecha.ToLocalTime():dd/MM/yyyy HH:mm}  ·  Lista: {lista.Nombre}")
                            .FontSize(8).FontColor(Gris);
                        col.Item().Element(c => CajaDoctor(c, doctor));
                        col.Item().Element(c => CajaPaciente(c, receta, lista));
                        col.Item().Text("INDICACIÓN FARMACOLÓGICA").FontSize(9).Bold().FontColor(Navy);
                        col.Item().Element(c => DetalleMedicamentos(c, lista));
                        if (ratios.Count > 0)
                        {
                            col.Item().Text("RATIOS DE USO EN LAS LISTAS DEL MÉDICO · INVENTARIO IDERMA")
                                .FontSize(9).Bold().FontColor(Navy);
                            col.Item().Element(c => TablaRatios(c, ratios));
                        }

                        if (!string.IsNullOrWhiteSpace(receta.Indicaciones) ||
                            !string.IsNullOrWhiteSpace(lista.IndicacionesGenerales))
                        {
                            col.Item().Background(Fondo).Padding(6).Column(notas =>
                            {
                                notas.Item().Text("INDICACIONES").FontSize(7).Bold().FontColor(Oro);
                                notas.Item().Text(string.IsNullOrWhiteSpace(receta.Indicaciones)
                                        ? lista.IndicacionesGenerales
                                        : receta.Indicaciones)
                                    .FontSize(9);
                            });
                        }

                        col.Item().Extend().AlignBottom().Element(c => BloqueFirma(c, doctor));
                    });
                });
            })
            .WithMetadata(new DocumentMetadata
            {
                Title = $"Receta {receta.Numero} · {doctor.Nombre}",
                Author = IdermaMarca.Nombre,
                Creator = IdermaMarca.Sistema,
                Subject = "Receta médica"
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
                    fila.ConstantItem(76).Height(42).Image(logo).FitArea();
                    fila.ConstantItem(8);
                }

                fila.RelativeItem().Column(texto =>
                {
                    texto.Item().Text(IdermaMarca.NombreMayusculas).FontSize(14).Bold().FontColor(Navy);
                    texto.Item().Text(IdermaMarca.Giro).FontSize(8).FontColor(Oro);
                    texto.Item().Text("Personal médico · Recetas").FontSize(7).FontColor(Gris);
                });

                fila.ConstantItem(118).AlignRight().Column(meta =>
                {
                    meta.Item().Text("USO INTERNO").FontSize(8).Bold().FontColor(Oro);
                    meta.Item().Text($"Emisión: {DateTime.Now:dd/MM/yyyy}").FontSize(8).FontColor(Gris);
                    meta.Item().Text($"Hora: {DateTime.Now:HH:mm}").FontSize(8).FontColor(Gris);
                });
            });
            col.Item().PaddingTop(5).Height(1.8f).Background(Oro);
            col.Item().Height(1).Background(Navy);
        });
    }

    private static void Pie(IContainer contenedor)
    {
        contenedor.Column(col =>
        {
            col.Item().Height(1).Background(Oro);
            col.Item().PaddingTop(6).Row(fila =>
            {
                fila.RelativeItem().Text($"{IdermaMarca.Nombre} · {IdermaMarca.Giro} · No sustituye la etiqueta del fabricante")
                    .FontSize(7).FontColor(Gris);
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

    private static void BloqueFirma(IContainer contenedor, Doctor doctor)
    {
        contenedor.PaddingTop(28).AlignCenter().Width(240).Column(izq =>
        {
            izq.Item().Height(88);
            izq.Item().Height(1).Background(Navy);
            izq.Item().PaddingTop(8).AlignCenter().Text("Firma y sello del médico").FontSize(8).FontColor(Gris);
            izq.Item().AlignCenter().Text(doctor.Nombre).FontSize(9).Bold();
            izq.Item().AlignCenter().Text($"CMP {doctor.Colegiatura}  ·  {doctor.Rne}").FontSize(8).FontColor(Gris);
            izq.Item().AlignCenter().Text(doctor.Especialidad).FontSize(8).FontColor(Gris);
        });
    }

    private static void CajaDoctor(IContainer contenedor, Doctor doctor)
    {
        contenedor.Border(0.7f).BorderColor(Navy).Background(Fondo).Padding(6).Column(col =>
        {
            col.Item().Text("MÉDICO TRATANTE").FontSize(7).Bold().FontColor(Oro);
            col.Item().Text(string.Join("  ·  ", new[]
            {
                doctor.Nombre,
                string.IsNullOrWhiteSpace(doctor.Colegiatura) ? "" : $"CMP {doctor.Colegiatura}",
                string.IsNullOrWhiteSpace(doctor.Rne) ? "" : doctor.Rne,
                doctor.Especialidad
            }.Where(s => !string.IsNullOrWhiteSpace(s)))).FontSize(8.5f).Bold();
        });
    }

    private static void CajaPaciente(IContainer contenedor, RecetaEmitida receta, ListaReceta lista)
    {
        contenedor.Border(0.7f).BorderColor(Linea).Padding(6).Column(col =>
        {
            col.Item().Text("DATOS DEL PACIENTE").FontSize(7).Bold().FontColor(Oro);
            col.Item().Row(fila =>
            {
                Dato(fila, "Paciente", receta.PacienteNombre);
                DatoSi(fila, "Documento", receta.PacienteDocumento);
                Dato(fila, "Diagnóstico", string.IsNullOrWhiteSpace(receta.Diagnostico) ? lista.Diagnostico : receta.Diagnostico);
                Dato(fila, "Folio", receta.Numero);
                Dato(fila, "Clínica", IdermaMarca.Nombre);
            });
        });
    }

    private static void DetalleMedicamentos(IContainer contenedor, ListaReceta lista)
    {
        contenedor.Column(col =>
        {
            col.Spacing(0);
            var n = 1;
            foreach (var item in lista.Items)
            {
                var actual = item;
                var numero = n;
                col.Item().PaddingVertical(5).BorderBottom(0.5f).BorderColor(Linea).Column(med =>
                {
                    med.Spacing(1);
                    med.Item().Text($"{numero}.  {actual.Nombre}").FontSize(9).Bold();
                    var linea = LineaPosologia(actual);
                    if (!string.IsNullOrWhiteSpace(linea))
                    {
                        med.Item().Text(linea).FontSize(8);
                    }

                    if (!string.IsNullOrWhiteSpace(actual.Indicaciones))
                    {
                        med.Item().Text($"Uso: {actual.Indicaciones}").FontSize(8).FontColor(Gris);
                    }
                });
                n++;
            }
        });
    }

    private static string LineaPosologia(ItemListaReceta item)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.PosologiaTexto))
        {
            partes.Add(item.PosologiaTexto);
        }

        if (!string.IsNullOrWhiteSpace(item.DespachoTexto))
        {
            partes.Add("Despachar: " + item.DespachoTexto);
        }

        return string.Join("  ·  ", partes);
    }

    private static void Dato(RowDescriptor fila, string etiqueta, string valor)
    {
        fila.RelativeItem().Column(c =>
        {
            c.Item().Text(etiqueta).FontSize(6.5f).FontColor(Gris);
            c.Item().Text(string.IsNullOrWhiteSpace(valor) ? "—" : valor).FontSize(8.5f).Bold();
        });
    }

    private static void DatoSi(RowDescriptor fila, string etiqueta, string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return;
        }

        Dato(fila, etiqueta, valor);
    }

    private static void TablaRatios(IContainer contenedor, IReadOnlyList<RatioMedicamento> ratios)
    {
        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2.2f);
                c.RelativeColumn(1.4f);
                c.RelativeColumn(2.2f);
            });
            EncabezadoTabla(tabla, "Medicamento", "Uso en listas del médico", "Inventario Iderma");
            foreach (var ratio in ratios)
            {
                tabla.Cell().Element(Celda).Text(ratio.Nombre).FontSize(8);
                tabla.Cell().Element(Celda).Text(ratio.RatioTexto).FontSize(8);
                tabla.Cell().Element(Celda).Text(ratio.InventarioTexto).FontSize(8);
            }
        });
    }

    private static void EncabezadoTabla(TableDescriptor tabla, params string[] titulos)
    {
        foreach (var titulo in titulos)
        {
            tabla.Cell().Background(Navy).Padding(3).Text(titulo).FontSize(7).Bold().FontColor("#FFFFFF");
        }
    }

    private static IContainer Celda(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Linea).PaddingVertical(3).PaddingHorizontal(3);
}
