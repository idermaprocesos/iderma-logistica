using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class ExcelEtiquetaService
{
    public static readonly string[] Encabezados = ["CÓDIGO", "ÁREA", "PRODUCTO", "CANTIDAD"];

    public static void CrearPlantilla(string ruta)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Etiquetas");

        for (var i = 0; i < Encabezados.Length; i++)
        {
            var celda = hoja.Cell(1, i + 1);
            celda.Value = Encabezados[i];
            celda.Style.Font.Bold = true;
            celda.Style.Font.FontColor = XLColor.FromHtml(IdermaMarca.Navy);
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
            celda.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            celda.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        var ejemplos = new (string Codigo, string Area, string Producto, int Cantidad)[]
        {
            ("CDB_002", "CDB", "MONALISA (HARD TYPE) FUCSIA", 8),
            ("CDB_004", "CDB", "MONALISA (SOFT TYPE) VERDE", 0),
            ("CDB_007", "CDB", "BIODERMA CICABIO CREME 40 ML", 7)
        };

        for (var i = 0; i < ejemplos.Length; i++)
        {
            var fila = i + 2;
            hoja.Cell(fila, 1).Value = ejemplos[i].Codigo;
            hoja.Cell(fila, 2).Value = ejemplos[i].Area;
            hoja.Cell(fila, 3).Value = ejemplos[i].Producto;
            hoja.Cell(fila, 4).Value = ejemplos[i].Cantidad;

            hoja.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            hoja.Cell(fila, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            hoja.Cell(fila, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            hoja.Cell(fila, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var rango = hoja.Range(1, 1, 1 + ejemplos.Length, Encabezados.Length);
        rango.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        rango.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        rango.Style.Border.OutsideBorderColor = XLColor.Black;
        rango.Style.Border.InsideBorderColor = XLColor.Black;

        hoja.Column(1).Width = 16;
        hoja.Column(2).Width = 12;
        hoja.Column(3).Width = 42;
        hoja.Column(4).Width = 14;
        hoja.Row(1).Height = 18;
        hoja.SheetView.FreezeRows(1);

        IdermaMarca.AplicarPropiedades(libro, "Plantilla de etiquetas · Iderma Capilar");
        libro.SaveAs(ruta);
    }

    public static IReadOnlyList<FilaEtiqueta> Leer(string ruta)
    {
        using var libro = new XLWorkbook(ruta);
        var hoja = libro.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("El archivo no tiene hojas.");

        var encabezado = hoja.Row(1);
        for (var i = 0; i < Encabezados.Length; i++)
        {
            var actual = Normalizar(encabezado.Cell(i + 1).GetString());
            var esperado = Normalizar(Encabezados[i]);
            if (actual != esperado)
            {
                throw new InvalidOperationException(
                    "El Excel no sigue la estructura obligatoria. " +
                    "La primera fila debe ser exactamente: CÓDIGO | ÁREA | PRODUCTO | CANTIDAD. " +
                    "Descargue la plantilla e inténtelo de nuevo.");
            }
        }

        var filas = new List<FilaEtiqueta>();
        foreach (var fila in hoja.RowsUsed().Where(f => f.RowNumber() > 1))
        {
            var codigo = fila.Cell(1).GetString().Trim();
            var area = fila.Cell(2).GetString().Trim();
            var producto = fila.Cell(3).GetString().Trim();
            if (string.IsNullOrWhiteSpace(codigo) && string.IsNullOrWhiteSpace(producto))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(codigo))
            {
                throw new InvalidOperationException($"La fila {fila.RowNumber()} no tiene CÓDIGO.");
            }

            if (!TryCantidad(fila.Cell(4), out var cantidad))
            {
                throw new InvalidOperationException(
                    $"La fila {fila.RowNumber()} ({codigo}) no tiene CANTIDAD válida. " +
                    "Ese número es cuántas etiquetas se imprimen de ese producto. Use 0, 1, 2, 8, etc.");
            }

            filas.Add(new FilaEtiqueta(codigo, area, producto, cantidad));
        }

        if (filas.Count == 0)
        {
            throw new InvalidOperationException("El archivo no contiene productos debajo de los encabezados.");
        }

        return filas;
    }

    public static IReadOnlyList<(string Codigo, string Nombre)> LeerCodigos(string ruta)
    {
        using var libro = new XLWorkbook(ruta);
        var hoja = libro.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("El archivo no tiene hojas.");

        var encabezado = hoja.Row(1);
        var ultima = Math.Max(encabezado.LastCellUsed()?.Address.ColumnNumber ?? 1, 1);
        var columnaCodigo = 0;
        var columnaNombre = 0;
        var hayEncabezado = false;
        for (var columna = 1; columna <= ultima; columna++)
        {
            var texto = Normalizar(encabezado.Cell(columna).GetString());
            if (texto.Length == 0)
            {
                continue;
            }

            var esNombre = texto is "PRODUCTO" or "NOMBRE" or "DESCRIPCION"
                || texto.StartsWith("NOMBRE", StringComparison.Ordinal)
                || texto.StartsWith("PRODUCTO", StringComparison.Ordinal);
            hayEncabezado = hayEncabezado || texto is "CODIGO" or "AREA" or "PRODUCTO" or "CANTIDAD" or "NOMBRE" or "DESCRIPCION"
                || texto.StartsWith("CODIGO", StringComparison.Ordinal)
                || esNombre;
            if (columnaCodigo == 0 && (texto == "CODIGO" || texto.StartsWith("CODIGO", StringComparison.Ordinal)))
            {
                columnaCodigo = columna;
            }

            if (columnaNombre == 0 && esNombre)
            {
                columnaNombre = columna;
            }
        }

        if (hayEncabezado && columnaCodigo == 0)
        {
            throw new InvalidOperationException(
                "No se encontró la columna CÓDIGO. Ponga ese encabezado en la primera fila.");
        }

        var inicio = columnaCodigo == 0 ? 1 : 2;
        if (columnaCodigo == 0)
        {
            columnaCodigo = 1;
            if (columnaNombre == 0 && ultima >= 2)
            {
                columnaNombre = 2;
            }
        }

        var filas = new List<(string Codigo, string Nombre)>();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fila in hoja.RowsUsed().Where(f => f.RowNumber() >= inicio))
        {
            var codigo = TextoCelda(fila.Cell(columnaCodigo));
            if (codigo.Length == 0 || !vistos.Add(codigo))
            {
                continue;
            }

            var nombre = columnaNombre == 0 ? string.Empty : TextoCelda(fila.Cell(columnaNombre));
            filas.Add((codigo, nombre));
        }

        if (filas.Count == 0)
        {
            throw new InvalidOperationException("La columna CÓDIGO no tiene códigos para exportar.");
        }

        return filas;
    }

    private static string TextoCelda(IXLCell celda)
    {
        var texto = celda.GetString().Trim();
        if (texto.Length == 0)
        {
            texto = celda.GetFormattedString().Trim();
        }

        return texto;
    }

    private static bool TryCantidad(IXLCell celda, out int cantidad)
    {
        cantidad = 0;
        if (celda.Value.IsBlank && celda.CachedValue.IsBlank)
        {
            return false;
        }

        if (celda.CachedValue.TryConvert(out double cacheado, CultureInfo.CurrentCulture)
            && TryEntero(cacheado, out cantidad))
        {
            return true;
        }

        if (celda.Value.TryConvert(out double valor, CultureInfo.CurrentCulture)
            && TryEntero(valor, out cantidad))
        {
            return true;
        }

        if (celda.TryGetValue(out double numero) && TryEntero(numero, out cantidad))
        {
            return true;
        }

        var texto = celda.GetFormattedString().Trim();
        if (string.IsNullOrWhiteSpace(texto))
        {
            texto = celda.GetString().Trim();
        }

        texto = texto.Replace(" ", string.Empty);
        if (int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out cantidad) && cantidad >= 0)
        {
            return true;
        }

        if (int.TryParse(texto, NumberStyles.Integer, CultureInfo.CurrentCulture, out cantidad) && cantidad >= 0)
        {
            return true;
        }

        if (double.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalInv)
            && TryEntero(decimalInv, out cantidad))
        {
            return true;
        }

        return double.TryParse(texto, NumberStyles.Number, CultureInfo.CurrentCulture, out var decimalLocal)
            && TryEntero(decimalLocal, out cantidad);
    }

    private static bool TryEntero(double numero, out int cantidad)
    {
        cantidad = 0;
        if (numero < 0 || Math.Abs(numero - Math.Round(numero)) >= 0.001)
        {
            return false;
        }

        cantidad = (int)Math.Round(numero);
        return true;
    }

    private static string Normalizar(string valor)
    {
        var formD = valor.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sinAcento = new string(formD
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return sinAcento.Replace(" ", string.Empty);
    }
}
