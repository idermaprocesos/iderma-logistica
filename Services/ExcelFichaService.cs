using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class ExcelFichaService
{
    public static readonly string[] Encabezados =
    [
        "Área", "Código", "Nombre", "Categoría", "Descripción",
        "Estado físico", "Unidad", "Marca", "Modelo",
        "Normas", "Vida útil", "Existencia",
        "Stock mínimo", "Fabricante", "País", "Planta", "Tipo origen",
        "Proveedor", "Canal contacto 1", "Dato contacto 1", "Canal contacto 2", "Dato contacto 2",
        "Lote", "Fecha adquisición", "Factura",
        "Registro sanitario", "Estéril", "Refrigeración",
        "Fecha caducidad",
        "Reutilizable", "Ubicación", "Número de serie", "Garantía meses", "Mantenimiento",
        "Periodicidad", "Voltaje", "Compatibilidad", "Responsable", "Principio activo",
        "Concentración", "pH", "Superficies", "Tiempo contacto", "Dilución",
        "Peligrosidad", "SDS", "EPP", "Materiales compatibles", "Residuo",
        "Observaciones"
    ];

    public static void CrearPlantilla(string ruta, IReadOnlyList<FichaTecnica>? productos = null)
    {
        var fichas = (productos ?? [])
            .OrderBy(f => f.AreaTitulo, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(f => f.Codigo, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(f => f.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Fichas");
        var subtitulo = fichas.Count == 0
            ? null
            : $"{fichas.Count} producto(s) del inventario";
        var fila = IdermaMarca.EscribirMembrete(
            hoja,
            Encabezados.Length,
            "Plantilla de captura de fichas técnicas",
            subtitulo);

        for (var i = 0; i < Encabezados.Length; i++)
        {
            hoja.Cell(fila, i + 1).Value = Encabezados[i];
        }

        EstiloEncabezado(hoja.Range(fila, 1, fila, Encabezados.Length));

        if (fichas.Count == 0)
        {
            var ejemplo = fila + 1;
            Ejemplo(hoja, ejemplo, "Área", "Insumos Médicos");
            Ejemplo(hoja, ejemplo, "Código", "CDE_010");
            Ejemplo(hoja, ejemplo, "Nombre", "Gasas estériles 10 × 10");
            Ejemplo(hoja, ejemplo, "Categoría", "Material de curación");
            Ejemplo(hoja, ejemplo, "Existencia", 40);
            Ejemplo(hoja, ejemplo, "Stock mínimo", 10);
            Ejemplo(hoja, ejemplo, "País", "México");
            Ejemplo(hoja, ejemplo, "Canal contacto 1", "WhatsApp");
            Ejemplo(hoja, ejemplo, "Dato contacto 1", "999888777");
            Ejemplo(hoja, ejemplo, "Canal contacto 2", "Web");
            Ejemplo(hoja, ejemplo, "Dato contacto 2", "https://proveedor.example");
            Ejemplo(hoja, ejemplo, "Fecha caducidad", DateTime.Today.AddMonths(8));
            IdermaMarca.EscribirPieTabla(hoja, ejemplo + 2, Encabezados.Length);
            EscribirHojaPartidas(libro, [], "Use esta hoja o varias filas con el mismo código en Fichas para distintos vencimientos.");
        }
        else
        {
            for (var i = 0; i < fichas.Count; i++)
            {
                EscribirFilaDetalle(hoja.Row(fila + 1 + i), fichas[i]);
            }

            IdermaMarca.EscribirPieTabla(hoja, fila + 1 + fichas.Count + 1, Encabezados.Length);
            EscribirHojaPartidas(libro, fichas);
        }

        hoja.SheetView.FreezeRows(fila);
        for (var i = 0; i < Encabezados.Length; i++)
        {
            hoja.Column(i + 1).Width = Math.Clamp(Encabezados[i].Length + 4, 12, 28);
        }

        IdermaMarca.AplicarPropiedades(libro, "Plantilla de fichas técnicas · Iderma Capilar");
        libro.SaveAs(ruta);
    }

    public static List<FichaTecnica> Leer(string ruta, Guid? areaPredeterminada = null)
    {
        using var libro = new XLWorkbook(ruta);
        var hoja = libro.Worksheets.First();
        var primera = EncontrarEncabezado(hoja);
        var mapa = CrearMapa(primera);
        var fichas = new List<FichaTecnica>();

        foreach (var fila in hoja.RowsUsed().Where(f => f.RowNumber() > primera.RowNumber()))
        {
            var nombre = Texto(fila, mapa, "nombre");
            if (string.IsNullOrWhiteSpace(nombre))
            {
                continue;
            }

            var areaTexto = Texto(fila, mapa, "area", "área");
            var area = ParsearArea(areaTexto) ?? areaPredeterminada;

            var ficha = new FichaTecnica
            {
                Area = area,
                Codigo = TextoIdentificacion.Codigo(Texto(fila, mapa, "codigo", "código")),
                Nombre = TextoIdentificacion.Nombre(nombre),
                Categoria = Texto(fila, mapa, "categoria", "categoría"),
                Descripcion = Texto(fila, mapa, "descripcion", "descripción"),
                EstadoFisico = Texto(fila, mapa, "estado fisico", "estado físico"),
                Color = Texto(fila, mapa, "color", "color / apariencia"),
                Dimensiones = Texto(fila, mapa, "dimensiones"),
                PesoVolumen = Texto(fila, mapa, "peso / volumen", "peso", "volumen"),
                MaterialFabricacion = Texto(fila, mapa, "material", "material de fabricacion"),
                UnidadMedida = Texto(fila, mapa, "unidad", "unidad de medida"),
                Marca = Texto(fila, mapa, "marca"),
                Modelo = Texto(fila, mapa, "modelo"),
                NormasCertificaciones = Texto(fila, mapa, "normas", "normas / certificaciones"),
                VidaUtil = Texto(fila, mapa, "vida util", "vida útil"),
                Existencia = Numero(fila, mapa, "existencia"),
                StockMinimo = Numero(fila, mapa, "stock minimo", "stock mínimo"),
                Fabricante = Texto(fila, mapa, "fabricante"),
                PaisOrigen = Texto(fila, mapa, "pais", "país", "pais de origen"),
                PlantaOrigen = Texto(fila, mapa, "planta", "planta / ciudad de origen"),
                TipoOrigen = Texto(fila, mapa, "tipo origen", "tipo de origen"),
                Proveedor = Texto(fila, mapa, "proveedor", "proveedor / distribuidor"),
                ContactoProveedor = Texto(fila, mapa, "contacto", "contacto del proveedor"),
                ContactoCanal1 = CanalContacto(fila, mapa, "canal contacto 1", "canal 1"),
                ContactoDato1 = Texto(fila, mapa, "dato contacto 1", "dato 1"),
                ContactoCanal2 = CanalContacto(fila, mapa, "canal contacto 2", "canal 2"),
                ContactoDato2 = Texto(fila, mapa, "dato contacto 2", "dato 2"),
                Lote = Texto(fila, mapa, "lote", "lote / partida"),
                FechaAdquisicion = Fecha(fila, mapa, "fecha adquisicion", "fecha de adquisición"),
                NumeroFactura = Texto(fila, mapa, "factura", "numero de factura"),
                RegistroSanitario = Texto(fila, mapa, "registro sanitario"),
                Esteril = Booleano(fila, mapa, "esteril", "estéril"),
                RequiereRefrigeracion = Booleano(fila, mapa, "refrigeracion", "refrigeración"),
                FechaCaducidad = Fecha(fila, mapa, "fecha caducidad", "caducidad", "vencimiento"),
                Reutilizable = Booleano(fila, mapa, "reutilizable"),
                Ubicacion = Texto(fila, mapa, "ubicacion", "ubicación"),
                NumeroSerie = Texto(fila, mapa, "numero de serie", "número de serie"),
                GarantiaMeses = Numero(fila, mapa, "garantia meses", "garantía meses"),
                RequiereMantenimiento = Booleano(fila, mapa, "mantenimiento"),
                PeriodicidadMantenimiento = Texto(fila, mapa, "periodicidad"),
                Voltaje = Texto(fila, mapa, "voltaje"),
                Compatibilidad = Texto(fila, mapa, "compatibilidad"),
                Responsable = Texto(fila, mapa, "responsable"),
                PrincipioActivo = Texto(fila, mapa, "principio activo"),
                Concentracion = Texto(fila, mapa, "concentracion", "concentración"),
                Ph = Texto(fila, mapa, "ph", "pH"),
                SuperficiesUso = Texto(fila, mapa, "superficies"),
                TiempoContacto = Texto(fila, mapa, "tiempo contacto"),
                Dilucion = Texto(fila, mapa, "dilucion", "dilución"),
                Peligrosidad = Texto(fila, mapa, "peligrosidad"),
                HojaSeguridad = Texto(fila, mapa, "sds", "hoja de seguridad"),
                EppRequerido = Texto(fila, mapa, "epp"),
                MaterialesCompatibles = Texto(fila, mapa, "materiales compatibles"),
                ResiduoGenerado = Texto(fila, mapa, "residuo"),
                Observaciones = Texto(fila, mapa, "observaciones"),
                Caracteristicas = AlmacenCaracteristicas.ParsearLinea(
                    Texto(fila, mapa, "caracteristicas extra", "características extra", "caracteristicas"))
            };
            AplicarContactosProveedor(ficha);
            fichas.Add(ficha);
        }

        var agrupadas = AgruparPorCodigo(fichas);
        AplicarHojaPartidas(libro, agrupadas, areaPredeterminada);
        return agrupadas;
    }

    private static IXLRow EncontrarEncabezado(IXLWorksheet hoja)
    {
        foreach (var fila in hoja.RowsUsed())
        {
            var mapa = CrearMapa(fila);
            if (mapa.ContainsKey("nombre") && (mapa.ContainsKey("codigo") || mapa.ContainsKey("area")))
            {
                return fila;
            }
        }

        return hoja.FirstRowUsed()
            ?? throw new InvalidOperationException("El archivo de Excel está vacío.");
    }

    private static Dictionary<string, int> CrearMapa(IXLRow encabezado)
    {
        var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var celda in encabezado.CellsUsed())
        {
            var clave = Normalizar(celda.GetString());
            if (!string.IsNullOrWhiteSpace(clave) && !mapa.ContainsKey(clave))
            {
                mapa[clave] = celda.Address.ColumnNumber;
            }
        }

        return mapa;
    }

    private static string Texto(IXLRow fila, Dictionary<string, int> mapa, params string[] claves)
    {
        var celda = Celda(fila, mapa, claves);
        return celda is null ? string.Empty : celda.GetString().Trim();
    }

    private static double Numero(IXLRow fila, Dictionary<string, int> mapa, params string[] claves)
    {
        var celda = Celda(fila, mapa, claves);
        if (celda is null || celda.IsEmpty())
        {
            return 0;
        }

        if (celda.TryGetValue(out double numero))
        {
            return numero;
        }

        return double.TryParse(celda.GetString(), NumberStyles.Any, CultureInfo.CurrentCulture, out var parseado)
            || double.TryParse(celda.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out parseado)
            ? parseado
            : 0;
    }

    private static bool Booleano(IXLRow fila, Dictionary<string, int> mapa, params string[] claves)
    {
        var texto = Texto(fila, mapa, claves).ToLowerInvariant();
        return texto is "si" or "sí" or "true" or "1" or "x" or "yes";
    }

    private static DateTimeOffset? Fecha(IXLRow fila, Dictionary<string, int> mapa, params string[] claves)
    {
        var celda = Celda(fila, mapa, claves);
        if (celda is null || celda.IsEmpty())
        {
            return null;
        }

        if (celda.TryGetValue(out DateTime fecha))
        {
            return new DateTimeOffset(DateTime.SpecifyKind(fecha.Date, DateTimeKind.Local));
        }

        var texto = celda.GetString().Trim();
        var formatos = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy" };
        if (DateTime.TryParseExact(texto, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parseada)
            || DateTime.TryParse(texto, CultureInfo.CurrentCulture, DateTimeStyles.None, out parseada))
        {
            return new DateTimeOffset(DateTime.SpecifyKind(parseada.Date, DateTimeKind.Local));
        }

        return null;
    }

    private static IXLCell? Celda(IXLRow fila, Dictionary<string, int> mapa, params string[] claves)
    {
        foreach (var clave in claves)
        {
            if (mapa.TryGetValue(Normalizar(clave), out var columna))
            {
                return fila.Cell(columna);
            }
        }

        return null;
    }

    private static Guid? ParsearArea(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        return AreasOperativas.ResolverId(valor);
    }

    private static string Normalizar(string valor)
    {
        var form = valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(form.Length);
        foreach (var c in form)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c) || c is '/' or '%')
            {
                builder.Append(c);
            }
            else if (char.IsWhiteSpace(c) || c is '-' or '_')
            {
                builder.Append(' ');
            }
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static void ExportarIndividual(string ruta, FichaTecnica ficha)
    {
        using var libro = new XLWorkbook();
        EscribirFichaFormato(libro.AddWorksheet(NombreHoja(ficha.Codigo)), ficha);
        IdermaMarca.AplicarPropiedades(libro, $"Ficha técnica {ficha.Codigo} · {IdermaMarca.Nombre}");
        libro.SaveAs(ruta);
    }

    public static void ExportarConsolidado(string ruta, IReadOnlyList<FichaTecnica> fichas)
    {
        using var libro = new XLWorkbook();
        EscribirResumen(libro.AddWorksheet("Consolidado"), fichas);
        EscribirDetalle(libro.AddWorksheet("Detalle"), fichas);
        EscribirHojaPartidas(libro, fichas);

        foreach (var ficha in fichas.Take(20))
        {
            EscribirFichaFormato(libro.AddWorksheet(NombreHoja(ficha.Codigo)), ficha);
        }

        IdermaMarca.AplicarPropiedades(libro, $"Consolidado de fichas técnicas · {IdermaMarca.Nombre}");
        libro.SaveAs(ruta);
    }

    private static void EscribirResumen(IXLWorksheet hoja, IReadOnlyList<FichaTecnica> fichas)
    {
        string[] titulos =
        [
            "Área", "Código", "Nombre", "Categoría", "Marca", "Modelo",
            "Existencia", "Stock mínimo", "Caducidad", "Estado",
            "Fabricante", "País", "Proveedor", "Lote"
        ];

        var inicio = IdermaMarca.EscribirMembrete(
            hoja,
            titulos.Length,
            "Consolidado de fichas técnicas",
            $"{fichas.Count} material(es) seleccionado(s)");

        for (var i = 0; i < titulos.Length; i++)
        {
            hoja.Cell(inicio, i + 1).Value = titulos[i];
        }

        EstiloEncabezado(hoja.Range(inicio, 1, inicio, titulos.Length));

        for (var i = 0; i < fichas.Count; i++)
        {
            var f = fichas[i];
            var fila = inicio + 1 + i;
            hoja.Cell(fila, 1).Value = f.AreaTitulo;
            hoja.Cell(fila, 2).Value = f.Codigo;
            hoja.Cell(fila, 3).Value = f.Nombre;
            hoja.Cell(fila, 4).Value = f.Categoria;
            hoja.Cell(fila, 5).Value = f.Marca;
            hoja.Cell(fila, 6).Value = f.Modelo;
            hoja.Cell(fila, 7).Value = f.Existencia;
            hoja.Cell(fila, 8).Value = f.StockMinimo;
            hoja.Cell(fila, 9).Value = f.FechaCaducidad?.ToString("dd/MM/yyyy") ?? string.Empty;
            hoja.Cell(fila, 10).Value = f.EstadoCaducidadTexto;
            hoja.Cell(fila, 11).Value = f.Fabricante;
            hoja.Cell(fila, 12).Value = f.PaisOrigen;
            hoja.Cell(fila, 13).Value = f.Proveedor;
            hoja.Cell(fila, 14).Value = f.Lote;
        }

        IdermaMarca.EscribirPieTabla(hoja, inicio + 1 + fichas.Count + 1, titulos.Length);
        hoja.SheetView.FreezeRows(inicio);
        hoja.Columns().AdjustToContents();
    }

    private static void EscribirDetalle(IXLWorksheet hoja, IReadOnlyList<FichaTecnica> fichas)
    {
        var inicio = IdermaMarca.EscribirMembrete(
            hoja,
            Encabezados.Length,
            "Detalle de fichas técnicas",
            $"{fichas.Count} material(es) · {IdermaMarca.Nombre}");

        for (var i = 0; i < Encabezados.Length; i++)
        {
            hoja.Cell(inicio, i + 1).Value = Encabezados[i];
        }

        EstiloEncabezado(hoja.Range(inicio, 1, inicio, Encabezados.Length));

        for (var i = 0; i < fichas.Count; i++)
        {
            EscribirFilaDetalle(hoja.Row(inicio + 1 + i), fichas[i]);
        }

        IdermaMarca.EscribirPieTabla(hoja, inicio + 1 + fichas.Count + 1, Encabezados.Length);
        hoja.SheetView.FreezeRows(inicio);
        hoja.Columns().AdjustToContents();
    }

    private static void EscribirFilaDetalle(IXLRow fila, FichaTecnica f)
    {
        var contactos = ContactosParaExcel(f);
        object?[] valores =
        [
            f.AreaTitulo, f.Codigo, f.Nombre, f.Categoria, f.Descripcion,
            f.EstadoFisico, f.UnidadMedida, f.Marca, f.Modelo,
            f.NormasCertificaciones, f.VidaUtil, f.Existencia,
            f.StockMinimo, f.Fabricante, f.PaisOrigen, f.PlantaOrigen, f.TipoOrigen,
            f.Proveedor, contactos.Canal1, contactos.Dato1, contactos.Canal2, contactos.Dato2,
            f.Lote, FechaTexto(f.FechaAdquisicion), f.NumeroFactura,
            f.RegistroSanitario, SiNo(f.Esteril), SiNo(f.RequiereRefrigeracion),
            FechaTexto(f.FechaCaducidad),
            SiNo(f.Reutilizable), f.Ubicacion, f.NumeroSerie, f.GarantiaMeses, SiNo(f.RequiereMantenimiento),
            f.PeriodicidadMantenimiento, f.Voltaje, f.Compatibilidad, f.Responsable, f.PrincipioActivo,
            f.Concentracion, f.Ph, f.SuperficiesUso, f.TiempoContacto, f.Dilucion,
            f.Peligrosidad, f.HojaSeguridad, f.EppRequerido, f.MaterialesCompatibles, f.ResiduoGenerado,
            f.Observaciones
        ];

        for (var i = 0; i < valores.Length; i++)
        {
            fila.Cell(i + 1).Value = valores[i]?.ToString() ?? string.Empty;
        }
    }

    private static void EscribirFichaFormato(IXLWorksheet hoja, FichaTecnica f)
    {
        hoja.Column(1).Width = 34;
        hoja.Column(2).Width = 56;
        hoja.Column(3).Width = 34;
        hoja.Column(4).Width = 28;

        var fila = IdermaMarca.EscribirMembrete(
            hoja,
            4,
            "Ficha técnica de material",
            $"{f.AreaTitulo}  ·  {f.Codigo}  ·  {f.Nombre}");
        fila = Bloque(hoja, fila, "Identificación",
            ("Código", f.Codigo),
            ("Nombre", f.Nombre),
            ("Categoría", f.Categoria),
            ("Unidad", f.UnidadMedida),
            ("Área", f.AreaTitulo),
            ("Estado físico", f.EstadoFisico),
            ("Descripción", f.Descripcion));

        fila = Bloque(hoja, fila, "Características técnicas",
            ("Marca", f.Marca),
            ("Modelo", f.Modelo),
            ("Normas", f.NormasCertificaciones),
            ("Vida útil", f.VidaUtil),
            ("Existencia", f.Existencia.ToString("0.##")),
            ("Stock mínimo", f.StockMinimo.ToString("0.##")));

        fila = Bloque(hoja, fila, "Procedencia",
            ("Fabricante", f.Fabricante),
            ("País", f.PaisOrigen),
            ("Planta", f.PlantaOrigen),
            ("Tipo de origen", f.TipoOrigen),
            ("Proveedor", f.Proveedor),
            ("Factura", f.NumeroFactura),
            ("Adquisición", FechaTexto(f.FechaAdquisicion)),
            ("Registro sanitario", f.RegistroSanitario));

        if (f.Partidas.Count > 0)
        {
            fila = Bloque(hoja, fila, "Lotes y caducidades",
                f.Partidas.Select(p => (
                    string.IsNullOrWhiteSpace(p.Lote) ? "(sin lote)" : p.Lote,
                    $"{p.Cantidad.ToString("0.##")} · {p.FechaCaducidadTexto} · {p.EstadoCaducidadTexto}"
                )).ToArray());
        }

        if (AreasOperativas.Plantilla(f.Area) == AreaOperativa.Clinica)
        {
            fila = Bloque(hoja, fila, "Particularidades de clínica",
                ("Estéril", SiNo(f.Esteril)),
                ("Refrigeración", SiNo(f.RequiereRefrigeracion)),
                ("Caducidad de referencia", FechaTexto(f.FechaCaducidad)),
                ("Reutilizable", SiNo(f.Reutilizable)));
        }
        else if (AreasOperativas.Plantilla(f.Area) == AreaOperativa.Oficina)
        {
            fila = Bloque(hoja, fila, "Particularidades de oficina",
                ("Ubicación", f.Ubicacion),
                ("Número de serie", f.NumeroSerie),
                ("Responsable", f.Responsable),
                ("Garantía (meses)", f.GarantiaMeses.ToString("0.##")),
                ("Voltaje", f.Voltaje),
                ("Mantenimiento", SiNo(f.RequiereMantenimiento)),
                ("Periodicidad", f.PeriodicidadMantenimiento),
                ("Compatibilidad", f.Compatibilidad));
        }
        else
        {
            fila = Bloque(hoja, fila, "Particularidades de limpieza",
                ("Principio activo", f.PrincipioActivo),
                ("Concentración", f.Concentracion),
                ("pH", f.Ph),
                ("Tiempo de contacto", f.TiempoContacto),
                ("Dilución", f.Dilucion),
                ("Peligrosidad", f.Peligrosidad),
                ("SDS", f.HojaSeguridad),
                ("EPP", f.EppRequerido),
                ("Superficies", f.SuperficiesUso),
                ("Materiales compatibles", f.MaterialesCompatibles),
                ("Residuo", f.ResiduoGenerado));
        }

        if (f.Caracteristicas.Count > 0)
        {
            fila = Bloque(hoja, fila, "Características adicionales",
                f.Caracteristicas.Select(c => (c.Nombre, c.Valor)).ToArray());
        }

        fila = Bloque(hoja, fila, "Observaciones", ("Notas", f.Observaciones));
        IdermaMarca.EscribirPieTabla(hoja, fila, 4);
        hoja.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        hoja.PageSetup.FitToPages(1, 0);
        hoja.PageSetup.PaperSize = XLPaperSize.A4Paper;
    }

    private static int Bloque(IXLWorksheet hoja, int fila, string titulo, params (string Etiqueta, string Valor)[] campos)
    {
        hoja.Range(fila, 1, fila, 4).Merge();
        hoja.Cell(fila, 1).Value = titulo;
        hoja.Cell(fila, 1).Style.Font.Bold = true;
        hoja.Cell(fila, 1).Style.Font.FontColor = XLColor.White;
        hoja.Cell(fila, 1).Style.Fill.BackgroundColor = XLColor.FromHtml(IdermaMarca.Navy);
        fila++;

        foreach (var (etiqueta, valor) in campos)
        {
            hoja.Cell(fila, 1).Value = etiqueta;
            hoja.Cell(fila, 1).Style.Font.Bold = true;
            hoja.Cell(fila, 1).Style.Fill.BackgroundColor = XLColor.FromHtml(IdermaMarca.FondoSuave);
            hoja.Range(fila, 2, fila, 4).Merge();
            hoja.Cell(fila, 2).Value = valor;
            hoja.Cell(fila, 2).Style.Alignment.WrapText = true;
            if (!string.IsNullOrWhiteSpace(valor) && valor.Length > 80)
            {
                hoja.Row(fila).Height = 36;
            }

            fila++;
        }

        return fila + 1;
    }

    private static List<FichaTecnica> AgruparPorCodigo(List<FichaTecnica> fichas)
    {
        var resultado = new List<FichaTecnica>();
        foreach (var ficha in fichas.Where(f => string.IsNullOrWhiteSpace(f.Codigo)))
        {
            ficha.Partidas = [PartidaDesdeFila(ficha)];
            PartidasInventario.Normalizar(ficha);
            resultado.Add(ficha);
        }

        foreach (var grupo in fichas
                     .Where(f => !string.IsNullOrWhiteSpace(f.Codigo))
                     .GroupBy(f => $"{f.Area?.ToString("N") ?? "none"}|{f.Codigo.Trim()}", StringComparer.OrdinalIgnoreCase))
        {
            var lista = grupo.ToList();
            var principal = lista[0];
            principal.Partidas = lista.Select(PartidaDesdeFila).ToList();
            PartidasInventario.Normalizar(principal);
            resultado.Add(principal);
        }

        return resultado;
    }

    private static PartidaInventario PartidaDesdeFila(FichaTecnica ficha) => new()
    {
        Lote = ficha.Lote?.Trim() ?? string.Empty,
        FechaCaducidad = ficha.FechaCaducidad,
        Cantidad = Math.Max(0, ficha.Existencia)
    };

    private static void AplicarHojaPartidas(XLWorkbook libro, List<FichaTecnica> fichas, Guid? areaPredeterminada)
    {
        var hoja = libro.Worksheets.FirstOrDefault(h =>
            string.Equals(h.Name, "Partidas", StringComparison.OrdinalIgnoreCase)
            || string.Equals(h.Name, "Lotes", StringComparison.OrdinalIgnoreCase));
        if (hoja is null)
        {
            return;
        }

        var primera = EncontrarEncabezado(hoja);
        var mapa = CrearMapa(primera);
        if (!mapa.ContainsKey("codigo") && !mapa.ContainsKey("código"))
        {
            return;
        }

        foreach (var fila in hoja.RowsUsed().Where(f => f.RowNumber() > primera.RowNumber()))
        {
            var codigo = Texto(fila, mapa, "codigo", "código");
            if (string.IsNullOrWhiteSpace(codigo))
            {
                continue;
            }

            var area = ParsearArea(Texto(fila, mapa, "area", "área")) ?? areaPredeterminada;
            var destino = fichas.FirstOrDefault(f =>
                f.Area == area
                && string.Equals(f.Codigo.Trim(), codigo.Trim(), StringComparison.OrdinalIgnoreCase));
            if (destino is null)
            {
                continue;
            }

            PartidasInventario.Upsert(
                destino,
                Texto(fila, mapa, "lote", "lote / partida"),
                Fecha(fila, mapa, "fecha caducidad", "caducidad", "vencimiento"),
                Numero(fila, mapa, "cantidad", "existencia"),
                reemplazarCantidad: true);
        }

        foreach (var ficha in fichas)
        {
            PartidasInventario.Normalizar(ficha);
        }
    }

    private static void EscribirHojaPartidas(XLWorkbook libro, IReadOnlyList<FichaTecnica> fichas, string? nota = null)
    {
        string[] titulos = ["Área", "Código", "Nombre", "Lote", "Caducidad", "Cantidad", "Estado"];
        var hoja = libro.Worksheets.FirstOrDefault(h => string.Equals(h.Name, "Partidas", StringComparison.OrdinalIgnoreCase))
                   ?? libro.AddWorksheet("Partidas");
        var inicio = IdermaMarca.EscribirMembrete(
            hoja,
            titulos.Length,
            "Lotes y caducidades",
            nota ?? $"{fichas.Sum(f => f.Partidas.Count)} partida(s)");

        for (var i = 0; i < titulos.Length; i++)
        {
            hoja.Cell(inicio, i + 1).Value = titulos[i];
        }

        EstiloEncabezado(hoja.Range(inicio, 1, inicio, titulos.Length));
        var fila = inicio + 1;
        foreach (var ficha in fichas)
        {
            foreach (var partida in ficha.Partidas)
            {
                hoja.Cell(fila, 1).Value = ficha.AreaTitulo;
                hoja.Cell(fila, 2).Value = ficha.Codigo;
                hoja.Cell(fila, 3).Value = ficha.Nombre;
                hoja.Cell(fila, 4).Value = partida.Lote;
                hoja.Cell(fila, 5).Value = partida.FechaCaducidad?.ToString("dd/MM/yyyy") ?? string.Empty;
                hoja.Cell(fila, 6).Value = partida.Cantidad;
                hoja.Cell(fila, 7).Value = partida.EstadoCaducidadTexto;
                fila++;
            }
        }

        if (fila == inicio + 1)
        {
            hoja.Cell(fila, 1).Value = "Insumos Médicos";
            hoja.Cell(fila, 2).Value = "CDE_010";
            hoja.Cell(fila, 3).Value = "Gasas estériles 10 × 10";
            hoja.Cell(fila, 4).Value = "GAS-A";
            hoja.Cell(fila, 5).Value = DateTime.Today.AddMonths(4);
            hoja.Cell(fila, 6).Value = 15;
            fila++;
            hoja.Cell(fila, 1).Value = "Insumos Médicos";
            hoja.Cell(fila, 2).Value = "CDE_010";
            hoja.Cell(fila, 3).Value = "Gasas estériles 10 × 10";
            hoja.Cell(fila, 4).Value = "GAS-B";
            hoja.Cell(fila, 5).Value = DateTime.Today.AddMonths(10);
            hoja.Cell(fila, 6).Value = 25;
            fila++;
        }

        IdermaMarca.EscribirPieTabla(hoja, fila + 1, titulos.Length);
        hoja.SheetView.FreezeRows(inicio);
        hoja.Columns().AdjustToContents();
    }

    private static void Ejemplo(IXLWorksheet hoja, int fila, string encabezado, XLCellValue valor)
    {
        var columna = Array.IndexOf(Encabezados, encabezado);
        if (columna >= 0)
        {
            hoja.Cell(fila, columna + 1).Value = valor;
        }
    }

    private static void AplicarContactosProveedor(FichaTecnica ficha)
    {
        var hayCanal = !string.IsNullOrWhiteSpace(ficha.ContactoCanal1) || !string.IsNullOrWhiteSpace(ficha.ContactoCanal2);
        var hayDato = !string.IsNullOrWhiteSpace(ficha.ContactoDato1) || !string.IsNullOrWhiteSpace(ficha.ContactoDato2);
        if (!hayCanal && !hayDato)
        {
            ficha.ContactoCanal1 = CanalDatoProveedor.WhatsApp;
            ficha.ContactoCanal2 = CanalDatoProveedor.Web;
            return;
        }

        if (string.IsNullOrWhiteSpace(ficha.ContactoCanal1))
        {
            ficha.ContactoCanal1 = CanalDatoProveedor.WhatsApp;
        }

        if (string.IsNullOrWhiteSpace(ficha.ContactoCanal2))
        {
            ficha.ContactoCanal2 = CanalDatoProveedor.Web;
        }

        ficha.ContactosProveedorDefinidos = true;
        ficha.SincronizarCamposProveedorLegados();
    }

    private static string CanalContacto(IXLRow fila, Dictionary<string, int> mapa, params string[] nombres)
    {
        var texto = Texto(fila, mapa, nombres);
        return string.IsNullOrWhiteSpace(texto) ? string.Empty : CanalDatoProveedor.Normalizar(texto);
    }

    private static (string Canal1, string Dato1, string Canal2, string Dato2) ContactosParaExcel(FichaTecnica ficha)
    {
        var origen = ficha.ContactosProveedorDefinidos ? ficha : ficha.Clonar();
        if (!origen.ContactosProveedorDefinidos)
        {
            origen.PrepararContactosProveedor();
        }

        return (
            CanalSiHayDato(origen.ContactoCanal1, origen.ContactoDato1),
            origen.ContactoDato1,
            CanalSiHayDato(origen.ContactoCanal2, origen.ContactoDato2),
            origen.ContactoDato2);
    }

    private static string CanalSiHayDato(string canal, string dato) =>
        string.IsNullOrWhiteSpace(dato) ? string.Empty : CanalDatoProveedor.De(canal).Titulo;

    private static void EstiloEncabezado(IXLRange rango)
    {
        rango.Style.Font.Bold = true;
        rango.Style.Fill.BackgroundColor = XLColor.FromHtml(IdermaMarca.Navy);
        rango.Style.Font.FontColor = XLColor.White;
    }

    private static string NombreHoja(string codigo)
    {
        var limpio = new string(codigo.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (string.IsNullOrWhiteSpace(limpio))
        {
            limpio = "Ficha";
        }

        return limpio.Length <= 31 ? limpio : limpio[..31];
    }

    private static string FechaTexto(DateTimeOffset? fecha) =>
        fecha?.ToString("dd/MM/yyyy") ?? string.Empty;

    private static string SiNo(bool valor) => valor ? "Sí" : "No";
}
