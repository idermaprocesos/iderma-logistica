using IdermaFichas.Helpers;
using IdermaFichas.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IdermaFichas.Services;

public static class PdfEtiquetaService
{
    private const string Navy = IdermaMarca.Navy;
    private const string Oro = IdermaMarca.Oro;
    private const string Gris = "#5A6570";

    public static void ExportarEtiquetas(
        string ruta,
        IReadOnlyList<FilaEtiqueta> filas,
        OpcionesEtiqueta opciones,
        IProgress<(int Porcentaje, string Mensaje)>? progreso = null)
    {
        progreso?.Report((4, "Preparando las etiquetas…"));
        AsegurarLicencia();
        var etiquetas = Expandir(filas);
        if (etiquetas.Count == 0)
        {
            throw new InvalidOperationException("No hay etiquetas que generar. Todas las filas tienen cantidad 0.");
        }

        var tamano = TamanoHoja(opciones.Hoja);
        var columnas = Math.Clamp(opciones.Columnas, 1, 8);
        var filasHoja = Math.Clamp(opciones.Filas, 1, 16);
        var simbolo = CalcularSimbolo(opciones, tamano, columnas, filasHoja);
        progreso?.Report((8, $"Generando {etiquetas.Select(e => e.Codigo).Distinct().Count()} código(s)…"));
        SimboloEtiquetaService.Precargar(
            etiquetas.Select(e => e.Codigo),
            opciones.Tipo,
            simbolo,
            new Progress<(int Hechos, int Total)>(p =>
            {
                var porcentaje = 8 + (int)Math.Round(p.Hechos * 52.0 / Math.Max(1, p.Total));
                progreso?.Report((porcentaje, $"Generando códigos {p.Hechos} de {p.Total}…"));
            }));

        progreso?.Report((65, $"Componiendo el PDF ({etiquetas.Count} etiquetas)…"));
        Document
            .Create(contenedor =>
            {
                contenedor.Page(pagina =>
                {
                    pagina.Size(tamano);
                    pagina.Margin(8, Unit.Millimetre);
                    pagina.DefaultTextStyle(x => x.FontFamily("Calibri", "Segoe UI", "Arial").FontSize(8).FontColor(Navy));
                    pagina.Content().Element(c => Cuadricula(c, etiquetas, opciones, tamano, columnas, filasHoja, simbolo));
                    pagina.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Iderma Capilar · Generador de etiquetas · ").FontColor(Gris).FontSize(7);
                        t.CurrentPageNumber().FontColor(Gris).FontSize(7);
                        t.Span(" / ").FontColor(Gris).FontSize(7);
                        t.TotalPages().FontColor(Gris).FontSize(7);
                    });
                });
            })
            .WithMetadata(Metadatos($"Etiquetas {SimboloEtiquetaService.Titulo(opciones.Tipo)}"))
            .GeneratePdf(ruta);
        progreso?.Report((100, "Documento listo."));
    }

    public static void ExportarTablaNombreQr(
        string ruta,
        IReadOnlyList<ProductoQrLista> productos,
        OrdenTablaQr orden,
        IProgress<(int Porcentaje, string Mensaje)>? progreso = null)
    {
        if (productos.Count == 0)
        {
            throw new InvalidOperationException("No hay productos para la tabla.");
        }

        progreso?.Report((4, "Preparando la tabla…"));
        AsegurarLicencia();
        const float qrMm = 14;
        progreso?.Report((8, $"Generando {productos.Count} código(s) QR…"));
        SimboloEtiquetaService.Precargar(
            productos.Select(p => p.Codigo),
            TipoSimboloEtiqueta.Qr,
            qrMm,
            new Progress<(int Hechos, int Total)>(p =>
            {
                var porcentaje = 8 + (int)Math.Round(p.Hechos * 52.0 / Math.Max(1, p.Total));
                progreso?.Report((porcentaje, $"Generando códigos {p.Hechos} de {p.Total}…"));
            }));

        var tituloOrden = orden == OrdenTablaQr.Alfabetico ? "orden alfabético" : "orden del código";
        progreso?.Report((65, "Componiendo la tabla…"));
        Document
            .Create(contenedor =>
            {
                contenedor.Page(pagina =>
                {
                    pagina.Size(PageSizes.A4);
                    pagina.Margin(10, Unit.Millimetre);
                    pagina.DefaultTextStyle(x => x.FontFamily("Calibri", "Segoe UI", "Arial").FontSize(9).FontColor(Navy));
                    pagina.Header().Element(c => EncabezadoTablaQr(c, productos.Count, tituloOrden));
                    pagina.Content().Element(c => TablaNombreQr(c, productos, qrMm));
                    pagina.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Iderma Capilar · Tabla de productos y QR · ").FontColor(Gris).FontSize(7);
                        t.CurrentPageNumber().FontColor(Gris).FontSize(7);
                        t.Span(" / ").FontColor(Gris).FontSize(7);
                        t.TotalPages().FontColor(Gris).FontSize(7);
                    });
                });
            })
            .WithMetadata(Metadatos("Tabla de productos y códigos QR"))
            .GeneratePdf(ruta);
        progreso?.Report((100, "Documento listo."));
    }

    public static void ExportarPruebaImpresion(string ruta)
    {
        AsegurarLicencia();
        Document
            .Create(contenedor =>
            {
                contenedor.Page(pagina =>
                {
                    pagina.Size(PageSizes.A4);
                    pagina.Margin(6, Unit.Millimetre);
                    pagina.DefaultTextStyle(x => x.FontFamily("Calibri", "Segoe UI", "Arial").FontSize(7).FontColor(Navy));
                    pagina.Content().Element(CuerpoPruebaUnicaCara);
                    pagina.Footer().AlignCenter().Text(
                            "Una cara A4 · Imprima al 100 % sin ajustar a la página · Mida 20, 50, 70 y 100 mm · Iderma Capilar · Área logística")
                        .FontSize(6).FontColor(Gris);
                });
            })
            .WithMetadata(Metadatos("Prueba de impresión y calibración de impresora"))
            .GeneratePdf(ruta);
    }

    private static float CalcularSimbolo(OpcionesEtiqueta opciones, PageSize tamano, int columnas, int filas)
    {
        var huecoMm = 2.5f;
        var paginaAncho = tamano.Width / 2.83465f;
        var paginaAlto = tamano.Height / 2.83465f;
        var utilAncho = paginaAncho - 16;
        var utilAlto = Math.Max(40, paginaAlto - 16 - 14);
        var celdaAncho = (utilAncho - huecoMm * (columnas - 1)) / columnas;
        var celdaAlto = (utilAlto - huecoMm * (filas - 1)) / filas;
        var simbolo = Math.Clamp(opciones.TamanoMm, 10, 70);
        float maxSimbolo;
        if (opciones.Disposicion == DisposicionEtiqueta.Horizontal)
        {
            var maxAlto = Math.Max(10, celdaAlto - 3);
            var maxAncho = Math.Max(10, celdaAncho * 0.52f - 3);
            maxSimbolo = Math.Min(maxAlto, maxAncho);
        }
        else
        {
            maxSimbolo = Math.Max(10, Math.Min(celdaAncho, celdaAlto) - 10);
        }

        return simbolo > maxSimbolo ? maxSimbolo : simbolo;
    }

    private static void Cuadricula(
        IContainer contenedor,
        IReadOnlyList<FilaEtiqueta> etiquetas,
        OpcionesEtiqueta opciones,
        PageSize tamano,
        int columnas,
        int filas,
        float simbolo)
    {
        var huecoMm = 2.5f;
        var paginaAncho = tamano.Width / 2.83465f;
        var paginaAlto = tamano.Height / 2.83465f;
        var utilAncho = paginaAncho - 16;
        var utilAlto = Math.Max(40, paginaAlto - 16 - 14);
        var celdaAncho = (utilAncho - huecoMm * (columnas - 1)) / columnas;
        var celdaAlto = (utilAlto - huecoMm * (filas - 1)) / filas;

        contenedor.Column(hoja =>
        {
            var porPagina = columnas * filas;
            for (var i = 0; i < etiquetas.Count; i++)
            {
                if (i > 0 && i % porPagina == 0)
                {
                    hoja.Item().PageBreak();
                }

                if (i % porPagina == 0)
                {
                    var restantes = etiquetas.Count - i;
                    var enEsta = Math.Min(porPagina, restantes);
                    hoja.Item().Element(caja =>
                        PintarPagina(caja, etiquetas, i, enEsta, columnas, filas, celdaAncho, celdaAlto, huecoMm, opciones, simbolo));
                }
            }
        });
    }

    private static void PintarPagina(
        IContainer contenedor,
        IReadOnlyList<FilaEtiqueta> etiquetas,
        int inicio,
        int cantidad,
        int columnas,
        int filas,
        float celdaAncho,
        float celdaAlto,
        float huecoMm,
        OpcionesEtiqueta opciones,
        float simboloMm)
    {
        contenedor.Layers(capas =>
        {
            capas.PrimaryLayer().Element(caja =>
                PintarEtiquetas(caja, etiquetas, inicio, cantidad, columnas, filas, celdaAlto, huecoMm, opciones, simboloMm, celdaAncho));
            if (opciones.GuiaCorte)
            {
                capas.Layer().Element(caja =>
                    PintarGuiasCorte(caja, columnas, filas, celdaAncho, celdaAlto, huecoMm));
            }
        });
    }

    private static void PintarEtiquetas(
        IContainer contenedor,
        IReadOnlyList<FilaEtiqueta> etiquetas,
        int inicio,
        int cantidad,
        int columnas,
        int filas,
        float celdaAlto,
        float huecoMm,
        OpcionesEtiqueta opciones,
        float simboloMm,
        float celdaAncho)
    {
        contenedor.Column(filasCol =>
        {
            filasCol.Spacing(huecoMm, Unit.Millimetre);
            for (var f = 0; f < filas; f++)
            {
                filasCol.Item().Row(fila =>
                {
                    fila.Spacing(huecoMm, Unit.Millimetre);
                    for (var c = 0; c < columnas; c++)
                    {
                        var indice = inicio + f * columnas + c;
                        fila.RelativeItem().Height(celdaAlto, Unit.Millimetre).Element(celda =>
                        {
                            if (indice < inicio + cantidad)
                            {
                                Etiqueta(celda, etiquetas[indice], opciones, simboloMm, celdaAncho, columnas);
                            }
                        });
                    }
                });
            }
        });
    }

    private static void PintarGuiasCorte(
        IContainer contenedor,
        int columnas,
        int filas,
        float celdaAncho,
        float celdaAlto,
        float huecoMm)
    {
        var ancho = columnas * celdaAncho + (columnas - 1) * huecoMm;
        var alto = filas * celdaAlto + (filas - 1) * huecoMm;
        contenedor
            .Width(ancho, Unit.Millimetre)
            .Height(alto, Unit.Millimetre)
            .Svg(size =>
            {
                static string Inv(float valor) => valor.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

                var svg = new System.Text.StringBuilder();
                svg.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{Inv(size.Width)}' height='{Inv(size.Height)}'>");
                const string estilo = "stroke='#C8C8C8' stroke-width='0.32' stroke-dasharray='1.8 1.6' fill='none' stroke-linecap='round'";

                float X(float mm) => mm / ancho * size.Width;
                float Y(float mm) => mm / alto * size.Height;

                svg.Append($"<rect x='0.25' y='0.25' width='{Inv(size.Width - 0.5f)}' height='{Inv(size.Height - 0.5f)}' {estilo}/>");
                for (var c = 1; c < columnas; c++)
                {
                    var x = X(c * celdaAncho + (c - 1) * huecoMm + huecoMm / 2f);
                    svg.Append($"<line x1='{Inv(x)}' y1='0' x2='{Inv(x)}' y2='{Inv(size.Height)}' {estilo}/>");
                }

                for (var f = 1; f < filas; f++)
                {
                    var y = Y(f * celdaAlto + (f - 1) * huecoMm + huecoMm / 2f);
                    svg.Append($"<line x1='0' y1='{Inv(y)}' x2='{Inv(size.Width)}' y2='{Inv(y)}' {estilo}/>");
                }

                svg.Append("</svg>");
                return svg.ToString();
            });
    }

    private static void Etiqueta(
        IContainer contenedor,
        FilaEtiqueta fila,
        OpcionesEtiqueta opciones,
        float simboloMm,
        float celdaAnchoMm,
        int columnas)
    {
        byte[] imagen;
        try
        {
            imagen = SimboloEtiquetaService.Generar(fila.Codigo, opciones.Tipo, simboloMm);
        }
        catch
        {
            imagen = SimboloEtiquetaService.Generar(fila.Codigo, TipoSimboloEtiqueta.Code128, simboloMm);
        }

        var anchoTexto = Math.Max(12, celdaAnchoMm - 2);
        var tamanoCodigo = columnas >= 5 ? 6f : columnas >= 3 ? 7f : 8f;
        var tamanoProducto = columnas >= 5 ? 4.2f : columnas >= 3 ? 4.8f : 5.5f;
        if (opciones.Disposicion == DisposicionEtiqueta.Horizontal)
        {
            EtiquetaHorizontal(contenedor, fila, opciones.Tipo, imagen, simboloMm, celdaAnchoMm, tamanoCodigo, tamanoProducto);
            return;
        }

        contenedor.Padding(1).Column(col =>
        {
            col.Spacing(0);
            col.Item().AlignCenter().Element(caja => PintarSimbolo(caja, imagen, opciones.Tipo, simboloMm, anchoTexto));
            col.Item().AlignCenter()
                .Text(fila.Codigo).Bold().FontSize(tamanoCodigo).LineHeight(0.95f).FontColor(Colors.Black);
            if (!string.IsNullOrWhiteSpace(fila.Producto))
            {
                col.Item().PaddingTop(0.45f, Unit.Millimetre).MaxWidth(anchoTexto, Unit.Millimetre)
                    .AlignCenter()
                    .Text(fila.Producto).Bold().FontSize(tamanoProducto).LineHeight(0.9f).FontColor(Colors.Black);
            }
        });
    }

    private static void EtiquetaHorizontal(
        IContainer contenedor,
        FilaEtiqueta fila,
        TipoSimboloEtiqueta tipo,
        byte[] imagen,
        float simboloMm,
        float celdaAnchoMm,
        float tamanoCodigo,
        float tamanoProducto)
    {
        var anchoSimbolo = SimboloEtiquetaService.EsBidimensional(tipo)
            ? simboloMm
            : Math.Min(simboloMm * 1.7f, Math.Max(12, celdaAnchoMm * 0.46f));
        var anchoTexto = Math.Max(10, celdaAnchoMm - anchoSimbolo - 4);

        contenedor.Padding(1).Row(filaEtiqueta =>
        {
            filaEtiqueta.Spacing(1.4f, Unit.Millimetre);
            filaEtiqueta.AutoItem().AlignMiddle().Element(caja =>
                PintarSimbolo(caja, imagen, tipo, simboloMm, anchoSimbolo));
            filaEtiqueta.RelativeItem().AlignMiddle().Column(texto =>
            {
                texto.Item().AlignLeft()
                    .Text(fila.Codigo).Bold().FontSize(tamanoCodigo).LineHeight(0.95f).FontColor(Colors.Black);
                if (!string.IsNullOrWhiteSpace(fila.Producto))
                {
                    texto.Item().PaddingTop(0.45f, Unit.Millimetre).MaxWidth(anchoTexto, Unit.Millimetre)
                        .AlignLeft()
                        .Text(fila.Producto).Bold().FontSize(tamanoProducto).LineHeight(0.9f).FontColor(Colors.Black);
                }
            });
        });
    }

    private static void PintarSimbolo(
        IContainer caja,
        byte[] imagen,
        TipoSimboloEtiqueta tipo,
        float simboloMm,
        float anchoMm)
    {
        if (SimboloEtiquetaService.EsBidimensional(tipo))
        {
            caja.Width(simboloMm, Unit.Millimetre).Height(simboloMm, Unit.Millimetre).Image(imagen).FitArea();
            return;
        }

        caja.MaxWidth(anchoMm, Unit.Millimetre).Height(simboloMm, Unit.Millimetre).Image(imagen).FitArea();
    }

    private static void CuerpoPruebaUnicaCara(IContainer contenedor)
    {
        contenedor.Column(col =>
        {
            col.Spacing(2.5f);
            col.Item().Element(EncabezadoCompacto);
            col.Item().Element(TituloSeccion("1. Color sólido, medios tonos y nivel de tinta"));
            col.Item().Element(SeccionColoresTinta);
            col.Item().Element(TituloSeccion("2. Grosor de trazo, escala milimétrica y geometría"));
            col.Item().Element(SeccionGrosoresYEscala);
            col.Item().Element(TituloSeccion("3. QR reales de 10 a 70 mm · el de 10 mm debe leerse con el teléfono"));
            col.Item().Element(c => FilaQr(c, 10, 12, 15, 18, 20, 25));
            col.Item().Element(c => FilaQr(c, 30, 35, 40));
            col.Item().Element(c => FilaQr(c, 50, 60, 70));
            col.Item().Element(TituloSeccion("4. Code 128, Code 39, EAN-13, Data Matrix, PDF417 y Aztec"));
            col.Item().Element(SeccionOtrosEstandares);
            col.Item().Element(TituloSeccion("5. Nitidez de texto, trama y lectura"));
            col.Item().Element(SeccionTextoYTrama);
        });
    }

    private static Action<IContainer> TituloSeccion(string texto) =>
        contenedor => contenedor.AlignCenter().Text(texto).Bold().FontSize(7.5f);

    private static void EncabezadoCompacto(IContainer contenedor)
    {
        contenedor.Column(col =>
        {
            col.Item().Row(fila =>
            {
                var logo = IdermaMarca.RutaLogo();
                if (logo is not null)
                {
                    fila.ConstantItem(36).Height(20).Image(logo).FitArea();
                }

                fila.RelativeItem().Column(texto =>
                {
                    texto.Item().AlignCenter().Text("PRUEBA DE IMPRESIÓN Y CALIBRACIÓN · UNA CARA A4")
                        .Bold().FontSize(10);
                    texto.Item().AlignCenter().Text(
                            $"Iderma Capilar · Área logística · {DateTime.Now:dd/MM/yyyy HH:mm} · Use regla milimétrica")
                        .FontSize(6.5f).FontColor(Gris);
                });
            });
            col.Item().PaddingTop(1).Height(2).Background(Oro);
        });
    }

    private static void SeccionColoresTinta(IContainer contenedor)
    {
        contenedor.Column(col =>
        {
            col.Item().Row(fila =>
            {
                foreach (var (nombre, color) in new (string, string)[]
                {
                    ("Cian", "#00A3E0"),
                    ("Magenta", "#C4007A"),
                    ("Amarillo", "#FFD100"),
                    ("Negro", "#111111"),
                    ("Rojo", "#E10600"),
                    ("Verde", "#009639"),
                    ("Azul", "#0033A0"),
                    ("Naranja", "#FF6A00")
                })
                {
                    fila.RelativeItem().Padding(0.8f).Column(caja =>
                    {
                        caja.Item().Height(12).Border(0.4f).BorderColor("#333333").Background(color);
                        caja.Item().AlignCenter().Text(nombre).FontSize(5);
                    });
                }
            });
            col.Item().Row(fila =>
            {
                foreach (var (nombre, color) in new (string, string)[]
                {
                    ("Navy", Navy),
                    ("Oro", Oro),
                    ("Rosa", "#E85D8C"),
                    ("Cian 50%", "#7FD1EF"),
                    ("Mag. 50%", "#E180BD"),
                    ("Amar. 50%", "#FFE888"),
                    ("Gris 50%", "#808080"),
                    ("Blanco", "#FFFFFF")
                })
                {
                    fila.RelativeItem().Padding(0.8f).Column(caja =>
                    {
                        caja.Item().Height(12).Border(0.4f).BorderColor("#333333").Background(color);
                        caja.Item().AlignCenter().Text(nombre).FontSize(5);
                    });
                }
            });
            col.Item().PaddingTop(1).Row(fila =>
            {
                for (var i = 0; i <= 10; i++)
                {
                    var nivel = i * 10;
                    var gris = 255 - (int)Math.Round(255 * (nivel / 100.0));
                    var hex = $"#{gris:X2}{gris:X2}{gris:X2}";
                    fila.RelativeItem().Padding(0.4f).Column(caja =>
                    {
                        caja.Item().Height(9).Border(0.3f).BorderColor("#888888").Background(hex);
                        caja.Item().AlignCenter().Text($"{nivel}%").FontSize(5);
                    });
                }
            });
        });
    }

    private static void SeccionGrosoresYEscala(IContainer contenedor)
    {
        contenedor.Column(col =>
        {
            col.Item().Row(fila =>
            {
                void Linea(IContainer caja, float grosor)
                {
                    caja.Row(linea =>
                    {
                        linea.ConstantItem(28).AlignMiddle().Text($"{grosor:0.00}").FontSize(6);
                        linea.RelativeItem().MinHeight(10).AlignMiddle().Column(trazo =>
                            trazo.Item().Height(grosor, Unit.Millimetre).Background(Navy));
                    });
                }

                fila.RelativeItem().Column(izq =>
                {
                    foreach (var grosor in new[] { 0.10f, 0.25f, 0.50f, 0.75f })
                    {
                        izq.Item().Element(c => Linea(c, grosor));
                    }
                });
                fila.RelativeItem().Column(der =>
                {
                    foreach (var grosor in new[] { 1.00f, 1.50f, 2.00f, 3.00f })
                    {
                        der.Item().Element(c => Linea(c, grosor));
                    }
                });
            });

            col.Item().PaddingTop(2).AlignCenter().Column(regla =>
            {
                regla.Item().AlignCenter().Text("Regla 100 mm  ·  0          25          50          75          100")
                    .FontSize(6).FontColor(Gris);
                regla.Item().AlignCenter().Width(100, Unit.Millimetre).Height(3.2f).Background(Navy);
                regla.Item().AlignCenter().Width(100, Unit.Millimetre).Row(ticks =>
                {
                    for (var i = 0; i < 10; i++)
                    {
                        ticks.RelativeItem().Height(3).BorderLeft(0.6f).BorderColor(Navy);
                    }
                });
            });

            col.Item().AlignCenter().Row(geo =>
            {
                geo.AutoItem().PaddingRight(8).Width(10, Unit.Millimetre).Height(10, Unit.Millimetre)
                    .Border(0.8f).BorderColor(Navy)
                    .AlignCenter().AlignMiddle().Text("10").FontSize(5);
                geo.AutoItem().PaddingRight(8).Width(20, Unit.Millimetre).Height(20, Unit.Millimetre)
                    .Border(0.8f).BorderColor(Navy)
                    .AlignCenter().AlignMiddle().Text("20 mm").FontSize(6);
                geo.AutoItem().PaddingRight(8).Width(8, Unit.Millimetre).Height(8, Unit.Millimetre)
                    .Border(0.7f).BorderColor(Oro)
                    .AlignCenter().AlignMiddle().Text("+").FontSize(7).Bold();
                geo.AutoItem().AlignMiddle().Text("Cuadros 10 y 20 mm · cruz de registro · imprima al 100 %")
                    .FontSize(6).FontColor(Gris);
            });
        });
    }

    private static void FilaQr(IContainer contenedor, params float[] medidas)
    {
        contenedor.AlignCenter().Row(fila =>
        {
            foreach (var mm in medidas)
            {
                var png = SimboloEtiquetaService.Generar("IDERMA-CAL", TipoSimboloEtiqueta.Qr, mm);
                fila.AutoItem().PaddingHorizontal(2).Width(mm, Unit.Millimetre).Column(caja =>
                {
                    caja.Item().AlignCenter().Width(mm, Unit.Millimetre).Height(mm, Unit.Millimetre).Image(png).FitArea();
                    caja.Item().AlignCenter().Text($"{mm:0} mm").FontSize(5.5f);
                });
            }
        });
    }

    private static void SeccionOtrosEstandares(IContainer contenedor)
    {
        contenedor.Row(fila =>
        {
            void Muestra(TipoSimboloEtiqueta tipo, string valor, float mm)
            {
                var png = SimboloEtiquetaService.Generar(valor, tipo, mm);
                fila.RelativeItem().Padding(2).Column(caja =>
                {
                    caja.Item().AlignCenter().Height(mm, Unit.Millimetre).Image(png).FitArea();
                    caja.Item().AlignCenter().Text(SimboloEtiquetaService.Titulo(tipo)).FontSize(5);
                    caja.Item().AlignCenter().Text($"{mm:0} mm").FontSize(5).FontColor(Gris);
                });
            }

            Muestra(TipoSimboloEtiqueta.Code128, "CDB002", 9);
            Muestra(TipoSimboloEtiqueta.Code39, "CDB002", 9);
            Muestra(TipoSimboloEtiqueta.Ean13, "750123456789", 9);
            Muestra(TipoSimboloEtiqueta.DataMatrix, "IDERMA-CAL", 11);
            Muestra(TipoSimboloEtiqueta.Pdf417, "IDERMA-CAL", 9);
            Muestra(TipoSimboloEtiqueta.Aztec, "IDERMA-CAL", 11);
        });
    }

    private static void SeccionTextoYTrama(IContainer contenedor)
    {
        contenedor.Column(col =>
        {
            col.Item().AlignCenter().Text("5 pt · 7 pt · 9 pt  Iderma Capilar · el QR 10 mm y el Code 128 deben escanearse")
                .FontSize(6.5f).FontColor(Gris);
            col.Item().Row(fila =>
            {
                for (var i = 0; i < 48; i++)
                {
                    fila.RelativeItem().Height(3.5f).Background(i % 2 == 0 ? Colors.Black : Colors.White);
                }
            });
        });
    }

    private static void EncabezadoTablaQr(IContainer contenedor, int total, string tituloOrden)
    {
        contenedor.PaddingBottom(3).Column(col =>
        {
            col.Item().Text("Tabla de productos y códigos QR").Bold().FontSize(14);
            col.Item().Text($"{total} producto(s) · {tituloOrden} · {DateTime.Now:dd/MM/yyyy HH:mm}")
                .FontSize(8).FontColor(Gris);
        });
    }

    private static void TablaNombreQr(IContainer contenedor, IReadOnlyList<ProductoQrLista> productos, float qrMm)
    {
        const int columnas = 3;
        const float altoNombre = 16f;
        const float separacionFilas = 5f;
        const float separacionColumnas = 6f;

        contenedor.Column(columna =>
        {
            columna.Spacing(separacionFilas, Unit.Millimetre);
            for (var i = 0; i < productos.Count; i += columnas)
            {
                var inicio = i;
                var fondo = i / columnas % 2 == 0 ? "#F7F8FA" : "#FFFFFF";
                columna.Item().ShowEntire().Row(fila =>
                {
                    fila.Spacing(separacionColumnas, Unit.Millimetre);
                    for (var c = 0; c < columnas; c++)
                    {
                        var indice = inicio + c;
                        if (indice < productos.Count)
                        {
                            fila.RelativeItem().Element(caja =>
                                PintarTarjeta(caja, productos[indice], qrMm, altoNombre, fondo));
                        }
                        else
                        {
                            fila.RelativeItem();
                        }
                    }
                });
            }
        });
    }

    private static void PintarTarjeta(IContainer contenedor, ProductoQrLista producto, float qrMm, float altoNombre, string fondo)
    {
        var png = SimboloEtiquetaService.Generar(producto.Codigo, TipoSimboloEtiqueta.Qr, qrMm);
        var nombre = TextoIdentificacion.Nombre(producto.Nombre);
        var codigo = string.Equals(nombre, producto.Codigo, StringComparison.OrdinalIgnoreCase)
            ? " "
            : producto.Codigo;

        contenedor.Background(fondo).Border(0.4f).BorderColor("#D5D8DE")
            .PaddingHorizontal(5, Unit.Millimetre)
            .PaddingTop(3, Unit.Millimetre)
            .PaddingBottom(5, Unit.Millimetre)
            .Column(col =>
        {
            col.Item().Height(altoNombre, Unit.Millimetre).AlignTop().AlignCenter().Column(texto =>
            {
                texto.Spacing(1);
                texto.Item().AlignCenter().Text(nombre).Bold().FontSize(8).LineHeight(1.05f).FontColor(Colors.Black);
                texto.Item().AlignCenter().Text(codigo).FontSize(6.5f).FontColor(Gris);
            });
            col.Item().PaddingTop(3, Unit.Millimetre).AlignCenter()
                .Width(qrMm, Unit.Millimetre)
                .Height(qrMm, Unit.Millimetre)
                .Image(png)
                .FitArea();
        });
    }

    private static List<FilaEtiqueta> Expandir(IReadOnlyList<FilaEtiqueta> filas)
    {
        var resultado = new List<FilaEtiqueta>();
        foreach (var fila in filas)
        {
            for (var i = 0; i < fila.Cantidad; i++)
            {
                resultado.Add(fila);
            }
        }

        return resultado;
    }

    private static PageSize TamanoHoja(TamanoHojaEtiqueta hoja) => hoja switch
    {
        TamanoHojaEtiqueta.Carta => PageSizes.Letter,
        TamanoHojaEtiqueta.Oficio => PageSizes.Legal,
        TamanoHojaEtiqueta.A5 => PageSizes.A5,
        _ => PageSizes.A4
    };

    private static void AsegurarLicencia() =>
        QuestPDF.Settings.License = LicenseType.Community;

    private static DocumentMetadata Metadatos(string titulo) => new()
    {
        Title = titulo,
        Author = IdermaMarca.Nombre,
        Creator = IdermaMarca.Sistema,
        Subject = "Área logística · Generador de etiquetas"
    };
}
