using IdermaFichas.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IdermaFichas.Services;

public static class PdfFichaService
{
    private static readonly string Navy = IdermaMarca.Navy;
    private static readonly string Oro = IdermaMarca.Oro;
    private static readonly string Fondo = IdermaMarca.FondoSuave;
    private static readonly string Gris = "#5A6570";
    private static readonly string Linea = "#D8D2C6";

    public static void ExportarIndividual(string ruta, FichaTecnica ficha)
    {
        AsegurarLicencia();
        Document
            .Create(contenedor => ComponerPaginas(contenedor, [ficha], esConsolidado: false))
            .WithMetadata(Metadatos($"Ficha técnica {ficha.Codigo} · {ficha.Nombre}"))
            .GeneratePdf(ruta);
    }

    public static void ExportarConsolidado(string ruta, IReadOnlyList<FichaTecnica> fichas)
    {
        AsegurarLicencia();
        Document
            .Create(contenedor => ComponerPaginas(contenedor, fichas, esConsolidado: true))
            .WithMetadata(Metadatos($"Consolidado de fichas técnicas · {fichas.Count} materiales"))
            .GeneratePdf(ruta);
    }

    private static void AsegurarLicencia() =>
        QuestPDF.Settings.License = LicenseType.Community;

    private static DocumentMetadata Metadatos(string titulo) => new()
    {
        Title = titulo,
        Author = IdermaMarca.Nombre,
        Creator = IdermaMarca.Sistema,
        Subject = IdermaMarca.Giro
    };

    private static void ComponerPaginas(IDocumentContainer contenedor, IReadOnlyList<FichaTecnica> fichas, bool esConsolidado)
    {
        contenedor.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.MarginHorizontal(36);
            pagina.MarginTop(28);
            pagina.MarginBottom(24);
            pagina.DefaultTextStyle(x => x.FontFamily("Calibri", "Segoe UI", "Arial").FontSize(9).FontColor(Navy));
            pagina.Header().Element(Encabezado);
            pagina.Footer().Element(Pie);

            pagina.Content().Column(col =>
            {
                col.Spacing(12);

                if (esConsolidado)
                {
                    col.Item().Element(c => TituloDocumento(c,
                        "CONSOLIDADO DE FICHAS TÉCNICAS",
                        $"{fichas.Count} material(es) seleccionado(s)"));
                    col.Item().Element(c => CajaControl(c, "CON-FT-" + DateTime.Now.ToString("yyyyMMdd"), "Varias áreas"));
                    col.Item().Element(c => TablaResumen(c, fichas));
                    col.Item().PaddingTop(4).Text("A continuación se detalla cada ficha técnica seleccionada.")
                        .FontSize(8).Italic().FontColor(Gris);

                    foreach (var ficha in fichas)
                    {
                        col.Item().PageBreak();
                        col.Item().Element(c => CuerpoFicha(c, ficha));
                    }
                }
                else
                {
                    var ficha = fichas[0];
                    col.Item().Element(c => TituloDocumento(c,
                        "FICHA TÉCNICA DE MATERIAL",
                        $"{ficha.AreaTitulo}  ·  {ficha.Codigo}"));
                    col.Item().Element(c => CajaControl(c, ficha.Codigo, ficha.AreaTitulo));
                    col.Item().Element(c => CuerpoFicha(c, ficha));
                }
            });
        });
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
                    fila.ConstantItem(92).Height(56).Image(logo).FitArea();
                    fila.ConstantItem(12);
                }

                fila.RelativeItem().Column(texto =>
                {
                    texto.Item().Text(IdermaMarca.NombreMayusculas)
                        .FontSize(16).Bold().FontColor(Navy);
                    texto.Item().Text(IdermaMarca.Giro)
                        .FontSize(9).FontColor(Oro);
                    texto.Item().Text(IdermaMarca.Sistema)
                        .FontSize(8).FontColor(Gris);
                });

                fila.ConstantItem(118).AlignRight().Column(meta =>
                {
                    meta.Item().Text("USO INTERNO").FontSize(8).Bold().FontColor(Oro);
                    meta.Item().Text($"Emisión: {DateTime.Now:dd/MM/yyyy}")
                        .FontSize(8).FontColor(Gris);
                    meta.Item().Text($"Hora: {DateTime.Now:HH:mm}")
                        .FontSize(8).FontColor(Gris);
                });
            });

            col.Item().PaddingTop(8).Height(2.2f).Background(Oro);
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

    private static void TituloDocumento(IContainer contenedor, string titulo, string subtitulo)
    {
        contenedor.Column(col =>
        {
            col.Item().Text(titulo).FontSize(13).Bold().FontColor(Navy);
            col.Item().Text(subtitulo).FontSize(9).FontColor(Gris);
        });
    }

    private static void CajaControl(IContainer contenedor, string folio, string area)
    {
        contenedor.Border(0.8f).BorderColor(Navy).Background(Fondo).Padding(8).Row(fila =>
        {
            DatoControl(fila, "Folio", folio);
            DatoControl(fila, "Área", area);
            DatoControl(fila, "Clasificación", "Uso interno");
            DatoControl(fila, "Emisor", IdermaMarca.Nombre);
        });
    }

    private static void DatoControl(RowDescriptor fila, string etiqueta, string valor)
    {
        fila.RelativeItem().Column(col =>
        {
            col.Item().Text(etiqueta.ToUpperInvariant()).FontSize(7).FontColor(Gris);
            col.Item().Text(valor).FontSize(9).Bold().FontColor(Navy);
        });
    }

    private static void TablaResumen(IContainer contenedor, IReadOnlyList<FichaTecnica> fichas)
    {
        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(58);
                cols.RelativeColumn(3);
                cols.ConstantColumn(58);
                cols.RelativeColumn(2);
                cols.ConstantColumn(48);
                cols.ConstantColumn(62);
                cols.RelativeColumn(2);
            });

            tabla.Header(h =>
            {
                void Cabeza(string texto) =>
                    h.Cell().Background(Navy).Padding(5)
                        .Text(texto).FontSize(7.5f).Bold().FontColor(Colors.White);

                Cabeza("Código");
                Cabeza("Material");
                Cabeza("Área");
                Cabeza("Categoría");
                Cabeza("Exist.");
                Cabeza("Caducidad");
                Cabeza("Estado");
            });

            var alterna = false;
            foreach (var f in fichas)
            {
                var fondo = alterna ? Fondo : "#FFFFFF";
                void Celda(string texto, bool negrita = false)
                {
                    var celda = tabla.Cell().Background(fondo).BorderBottom(0.4f).BorderColor(Linea).Padding(4);
                    var t = celda.Text(texto).FontSize(8);
                    if (negrita)
                    {
                        t.Bold();
                    }
                }

                Celda(f.Codigo, negrita: true);
                Celda(f.Nombre);
                Celda(f.AreaTitulo);
                Celda(Valor(f.Categoria));
                Celda(f.Existencia.ToString("0.##"));
                Celda(f.FechaCaducidadTexto);
                Celda(f.EstadoCaducidadTexto);
                alterna = !alterna;
            }
        });
    }

    private static void CuerpoFicha(IContainer contenedor, FichaTecnica f)
    {
        contenedor.Column(col =>
        {
            col.Spacing(8);
            col.Item().Background(Navy).PaddingHorizontal(8).PaddingVertical(5)
                .Text($"{f.Codigo}   ·   {f.Nombre}")
                .FontColor(Colors.White).SemiBold().FontSize(10);

            col.Item().Element(c => Seccion(c, "1. Identificación",
                ("Código", f.Codigo),
                ("Nombre", f.Nombre),
                ("Categoría", f.Categoria),
                ("Unidad de medida", f.UnidadMedida),
                ("Área", f.AreaTitulo),
                ("Estado físico", f.EstadoFisico),
                ("Descripción", f.Descripcion)));

            col.Item().Element(c => Seccion(c, "2. Características técnicas",
                ("Marca", f.Marca),
                ("Modelo", f.Modelo),
                ("Normas / certificaciones", f.NormasCertificaciones),
                ("Vida útil", f.VidaUtil),
                ("Existencia", f.Existencia.ToString("0.##")),
                ("Stock mínimo", f.StockMinimo.ToString("0.##"))));

            col.Item().Element(c => Seccion(c, "3. Procedencia",
                ("Fabricante", f.Fabricante),
                ("País de origen", f.PaisOrigen),
                ("Planta / ciudad", f.PlantaOrigen),
                ("Tipo de origen", f.TipoOrigen),
                ("Proveedor", f.Proveedor),
                ("Factura", f.NumeroFactura),
                ("Fecha de adquisición", Fecha(f.FechaAdquisicion)),
                ("Registro sanitario", f.RegistroSanitario)));

            if (f.Partidas.Count > 0)
            {
                col.Item().Element(c => Seccion(c, "4. Lotes y caducidades",
                    f.Partidas.Select(p => (
                        string.IsNullOrWhiteSpace(p.Lote) ? "(sin lote)" : p.Lote,
                        $"{p.Cantidad.ToString("0.##")} · {p.FechaCaducidadTexto} · {p.EstadoCaducidadTexto}"
                    )).ToArray()));
            }

            if (AreasOperativas.Plantilla(f.Area) == AreaOperativa.Clinica)
            {
                col.Item().Element(c => Seccion(c, "5. Particularidades de clínica",
                    ("Estéril", SiNo(f.Esteril)),
                    ("Refrigeración", SiNo(f.RequiereRefrigeracion)),
                    ("Caducidad de referencia", Fecha(f.FechaCaducidad)),
                    ("Reutilizable", SiNo(f.Reutilizable))));
            }
            else if (AreasOperativas.Plantilla(f.Area) == AreaOperativa.Oficina)
            {
                col.Item().Element(c => Seccion(c, "5. Particularidades de oficina",
                    ("Ubicación", f.Ubicacion),
                    ("Número de serie", f.NumeroSerie),
                    ("Responsable", f.Responsable),
                    ("Garantía (meses)", f.GarantiaMeses.ToString("0.##")),
                    ("Voltaje", f.Voltaje),
                    ("Mantenimiento", SiNo(f.RequiereMantenimiento)),
                    ("Periodicidad", f.PeriodicidadMantenimiento),
                    ("Compatibilidad", f.Compatibilidad)));
            }
            else
            {
                col.Item().Element(c => Seccion(c, "5. Particularidades de limpieza",
                    ("Principio activo", f.PrincipioActivo),
                    ("Concentración", f.Concentracion),
                    ("pH", f.Ph),
                    ("Tiempo de contacto", f.TiempoContacto),
                    ("Dilución", f.Dilucion),
                    ("Peligrosidad", f.Peligrosidad),
                    ("Hoja de seguridad", f.HojaSeguridad),
                    ("EPP requerido", f.EppRequerido),
                    ("Superficies de uso", f.SuperficiesUso),
                    ("Materiales compatibles", f.MaterialesCompatibles),
                    ("Residuo generado", f.ResiduoGenerado)));
            }

            if (f.Caracteristicas.Count > 0)
            {
                col.Item().Element(c => Seccion(c, "6. Características adicionales",
                    f.Caracteristicas.Select(x => (x.Nombre, x.Valor)).ToArray()));
                col.Item().Element(c => Seccion(c, "7. Observaciones",
                    ("Notas internas", f.Observaciones)));
            }
            else
            {
                col.Item().Element(c => Seccion(c, "6. Observaciones",
                    ("Notas internas", f.Observaciones)));
            }
        });
    }

    private static void Seccion(IContainer contenedor, string titulo, params (string Etiqueta, string Valor)[] campos)
    {
        contenedor.Column(col =>
        {
            col.Item().Background(Navy).PaddingHorizontal(7).PaddingVertical(4)
                .Text(titulo).FontSize(8.5f).Bold().FontColor(Colors.White);

            foreach (var (etiqueta, valor) in campos)
            {
                col.Item().BorderBottom(0.4f).BorderColor(Linea).Row(fila =>
                {
                    fila.RelativeItem(2).Background(Fondo).Padding(4)
                        .Text(etiqueta).FontSize(8).SemiBold();
                    fila.RelativeItem(5).Padding(4)
                        .Text(Valor(valor)).FontSize(8);
                });
            }
        });
    }

    private static string Valor(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? "—" : texto.Trim();

    private static string SiNo(bool valor) => valor ? "Sí" : "No";

    private static string Fecha(DateTimeOffset? fecha) =>
        fecha?.ToString("dd/MM/yyyy") ?? "—";
}
