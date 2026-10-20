using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;

namespace IdermaFichas.Services;

public static class IdermaMarca
{
    public const string Nombre = "Iderma Capilar";
    public const string NombreMayusculas = "IDERMA CAPILAR";
    public const string Giro = "Clínica de trasplante e implante capilar";
    public const string Sistema = "Sistema de fichas técnicas de materiales";
    public const string Pie = "Documento de uso interno · Iderma Capilar · No sustituye la etiqueta del fabricante";
    public const string Navy = "#1A305A";
    public const string Oro = "#C5A059";
    public const string FondoSuave = "#F4EFE4";

    public static string? RutaLogo()
    {
        var candidatos = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "img", "IDERMA LOGO.jpg"),
            Path.Combine(AppDirectory(), "img", "IDERMA LOGO.jpg")
        };

        return candidatos.FirstOrDefault(File.Exists);
    }

    public static void AplicarPropiedades(XLWorkbook libro, string titulo)
    {
        libro.Properties.Author = Nombre;
        libro.Properties.Company = Nombre;
        libro.Properties.Title = titulo;
        libro.Properties.Subject = Sistema;
        libro.Properties.Comments = $"{Nombre} · {Giro}";
    }

    public static int EscribirMembrete(IXLWorksheet hoja, int columnas, string tituloDocumento, string? subtitulo = null)
    {
        columnas = Math.Max(columnas, 4);

        hoja.Range(1, 1, 5, columnas).Style.Fill.BackgroundColor = XLColor.White;
        hoja.Row(1).Height = 18;
        hoja.Row(2).Height = 16;
        hoja.Row(3).Height = 16;
        hoja.Row(4).Height = 14;
        hoja.Row(5).Height = 8;

        var textoDesde = 2;
        InsertarLogo(hoja, ref textoDesde);

        hoja.Range(1, textoDesde, 1, columnas).Merge();
        hoja.Cell(1, textoDesde).Value = NombreMayusculas;
        hoja.Cell(1, textoDesde).Style.Font.Bold = true;
        hoja.Cell(1, textoDesde).Style.Font.FontSize = 18;
        hoja.Cell(1, textoDesde).Style.Font.FontColor = XLColor.FromHtml(Navy);

        hoja.Range(2, textoDesde, 2, columnas).Merge();
        hoja.Cell(2, textoDesde).Value = Giro;
        hoja.Cell(2, textoDesde).Style.Font.FontSize = 11;
        hoja.Cell(2, textoDesde).Style.Font.FontColor = XLColor.FromHtml(Oro);

        hoja.Range(3, textoDesde, 3, columnas).Merge();
        hoja.Cell(3, textoDesde).Value = tituloDocumento;
        hoja.Cell(3, textoDesde).Style.Font.Bold = true;
        hoja.Cell(3, textoDesde).Style.Font.FontSize = 12;
        hoja.Cell(3, textoDesde).Style.Font.FontColor = XLColor.FromHtml(Navy);

        hoja.Range(4, textoDesde, 4, columnas).Merge();
        hoja.Cell(4, textoDesde).Value = string.IsNullOrWhiteSpace(subtitulo)
            ? $"{Sistema}  ·  Emitido el {DateTime.Now:dd/MM/yyyy HH:mm}"
            : $"{subtitulo}  ·  {DateTime.Now:dd/MM/yyyy HH:mm}";
        hoja.Cell(4, textoDesde).Style.Font.FontSize = 9;
        hoja.Cell(4, textoDesde).Style.Font.FontColor = XLColor.FromHtml("#5A6570");

        hoja.Range(5, 1, 5, columnas).Style.Fill.BackgroundColor = XLColor.FromHtml(Oro);

        hoja.PageSetup.Footer.Left.AddText($"{Nombre} · {Giro}");
        hoja.PageSetup.Footer.Right.AddText(Pie);
        hoja.PageSetup.Header.Right.AddText(NombreMayusculas);

        return 7;
    }

    public static void EscribirPieTabla(IXLWorksheet hoja, int fila, int columnas)
    {
        columnas = Math.Max(columnas, 4);
        hoja.Range(fila, 1, fila, columnas).Merge();
        hoja.Cell(fila, 1).Value = Pie;
        hoja.Cell(fila, 1).Style.Font.Italic = true;
        hoja.Cell(fila, 1).Style.Font.FontSize = 8;
        hoja.Cell(fila, 1).Style.Font.FontColor = XLColor.FromHtml("#5A6570");
    }

    private static void InsertarLogo(IXLWorksheet hoja, ref int columnaTexto)
    {
        var ruta = RutaLogo();
        if (ruta is null)
        {
            return;
        }

        using var flujo = new MemoryStream(File.ReadAllBytes(ruta));
        var formato = Path.GetExtension(ruta).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? XLPictureFormat.Png
            : XLPictureFormat.Jpeg;
        hoja.AddPicture(flujo, formato)
            .MoveTo(hoja.Cell(1, 1), 4, 2)
            .WithSize(132, 82);
        columnaTexto = 2;
    }

    private static string AppDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        var directorio = new DirectoryInfo(baseDir);
        while (directorio is not null)
        {
            var img = Path.Combine(directorio.FullName, "img", "IDERMA LOGO.jpg");
            if (File.Exists(img))
            {
                return directorio.FullName;
            }

            directorio = directorio.Parent;
        }

        return baseDir;
    }
}
