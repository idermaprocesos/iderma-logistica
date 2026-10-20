using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class ExcelMedicacionComunService
{
    public static readonly string[] Encabezados =
    [
        "Nombre del medicamento",
        "Dosis (cantidad)",
        "Unidad de la dosis",
        "Vía",
        "Frecuencia",
        "Horario",
        "Duración del tratamiento",
        "Cantidad a despachar",
        "Presentación",
        "Cómo usarlo"
    ];

    public static void CrearPlantilla(string ruta)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Medicación común");
        var fila = IdermaMarca.EscribirMembrete(hoja, Encabezados.Length, "Plantilla de medicación común");

        for (var i = 0; i < Encabezados.Length; i++)
        {
            hoja.Cell(fila, i + 1).Value = Encabezados[i];
        }

        var encabezado = hoja.Range(fila, 1, fila, Encabezados.Length);
        encabezado.Style.Font.Bold = true;
        encabezado.Style.Font.FontColor = XLColor.White;
        encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml(IdermaMarca.Navy);

        var ejemplo = fila + 1;
        hoja.Cell(ejemplo, 1).Value = "Cefalexina 500 mg";
        hoja.Cell(ejemplo, 2).Value = "1";
        hoja.Cell(ejemplo, 3).Value = "cápsula";
        hoja.Cell(ejemplo, 4).Value = "Oral";
        hoja.Cell(ejemplo, 5).Value = "cada 8 horas";
        hoja.Cell(ejemplo, 6).Value = "Con los alimentos";
        hoja.Cell(ejemplo, 7).Value = "7 días";
        hoja.Cell(ejemplo, 8).Value = "21";
        hoja.Cell(ejemplo, 9).Value = "cápsulas";
        hoja.Cell(ejemplo, 10).Value = "Después de los alimentos.";

        IdermaMarca.EscribirPieTabla(hoja, ejemplo + 2, Encabezados.Length);
        hoja.SheetView.FreezeRows(fila);
        for (var i = 0; i < Encabezados.Length; i++)
        {
            hoja.Column(i + 1).Width = Math.Clamp(Encabezados[i].Length + 3, 14, 36);
        }

        IdermaMarca.AplicarPropiedades(libro, "Medicación común · Iderma Capilar");
        libro.SaveAs(ruta);
    }

    public static List<MedicacionComun> Leer(string ruta)
    {
        using var libro = new XLWorkbook(ruta);
        var hoja = libro.Worksheets.First();
        var primera = EncontrarEncabezado(hoja);
        var mapa = CrearMapa(primera);
        var filas = new List<MedicacionComun>();

        foreach (var fila in hoja.RowsUsed().Where(f => f.RowNumber() > primera.RowNumber()))
        {
            var nombre = Texto(fila, mapa, "nombredelmedicamento", "nombre");
            if (string.IsNullOrWhiteSpace(nombre))
            {
                continue;
            }

            filas.Add(new MedicacionComun
            {
                Nombre = nombre,
                Dosis = Texto(fila, mapa, "dosiscantidad", "dosis"),
                DosisUnidad = Texto(fila, mapa, "unidaddeladosis", "unidaddosis"),
                ViaAdministracion = Texto(fila, mapa, "via"),
                Frecuencia = Texto(fila, mapa, "frecuencia"),
                Horario = Texto(fila, mapa, "horario"),
                Duracion = Texto(fila, mapa, "duraciondeltratamiento", "duracion"),
                Cantidad = Texto(fila, mapa, "cantidadadespachar", "cantidad"),
                Unidad = Texto(fila, mapa, "presentacion"),
                Indicaciones = Texto(fila, mapa, "comousarlo", "indicaciones")
            });
        }

        return filas;
    }

    private static IXLRow EncontrarEncabezado(IXLWorksheet hoja)
    {
        foreach (var fila in hoja.RowsUsed())
        {
            var mapa = CrearMapa(fila);
            if (mapa.ContainsKey("nombredelmedicamento") || mapa.ContainsKey("nombre"))
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
        foreach (var clave in claves)
        {
            if (mapa.TryGetValue(clave, out var col))
            {
                return fila.Cell(col).GetFormattedString().Trim();
            }
        }

        return "";
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

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
