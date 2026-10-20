using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class GraficoHistorialPrecios
{
    public static byte[] GenerarPng(IReadOnlyList<PrecioProducto> precios)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return GenerarPngNucleo(precios);
        }

        byte[]? png = null;
        Exception? error = null;
        var hilo = new Thread(() =>
        {
            try
            {
                png = GenerarPngNucleo(precios);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        hilo.SetApartmentState(ApartmentState.STA);
        hilo.IsBackground = true;
        hilo.Start();
        hilo.Join();
        if (error is not null)
        {
            throw error;
        }

        return png ?? [];
    }

    private static byte[] GenerarPngNucleo(IReadOnlyList<PrecioProducto> precios)
    {
        var series = precios
            .OrderBy(p => p.Fecha)
            .ThenBy(p => p.Id)
            .GroupBy(p => MonedaPrecio.Normalizar(p.Moneda))
            .Select(g => g.ToList())
            .Where(g => g.Count > 0)
            .ToList();

        const int escala = 3;
        const int ancho = 1000;
        const int altoSerie = 340;
        var alto = Math.Max(altoSerie, Math.Max(1, series.Count) * altoSerie);
        using var imagen = new Bitmap(ancho * escala, alto * escala);
        using (var g = Graphics.FromImage(imagen))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PageUnit = GraphicsUnit.Pixel;
            g.ScaleTransform(escala, escala);
            g.Clear(Color.White);

            if (series.Count == 0)
            {
                using var fuente = Fuente(13);
                g.DrawString("Sin precios para graficar", fuente, Brushes.Gray, 24, 24);
            }
            else
            {
                for (var i = 0; i < series.Count; i++)
                {
                    DibujarSerie(g, series[i], new Rectangle(0, i * altoSerie, ancho, altoSerie));
                }
            }
        }

        return APng(imagen);
    }

    private static void DibujarSerie(Graphics g, List<PrecioProducto> serie, Rectangle area)
    {
        var moneda = MonedaPrecio.Normalizar(serie[0].Moneda);
        using var fuenteTitulo = Fuente(13, FontStyle.Bold);
        using var fuenteEje = Fuente(11);
        using var fuentePrecio = Fuente(11, FontStyle.Bold);
        using var navy = new SolidBrush(Color.FromArgb(26, 48, 90));
        using var gris = new SolidBrush(Color.FromArgb(90, 101, 112));
        using var plumaGrilla = new Pen(Color.FromArgb(230, 226, 216), 1);
        using var plumaEje = new Pen(Color.FromArgb(26, 48, 90), 1.2f);

        var min = serie.Min(p => p.Monto);
        var max = serie.Max(p => p.Monto);
        if (Math.Abs(max - min) < 0.0001)
        {
            min = Math.Max(0, min - 1);
            max += 1;
        }

        var extra = (max - min) * 0.18;
        min = Math.Max(0, min - extra);
        max += extra;

        var anchoEje = 0f;
        for (var i = 0; i <= 4; i++)
        {
            var valor = min + (max - min) * i / 4;
            var etiqueta = $"{MonedaPrecio.Simbolo(moneda)} {valor:N2}";
            anchoEje = Math.Max(anchoEje, g.MeasureString(etiqueta, fuenteEje).Width);
        }

        var margenIzq = (int)Math.Ceiling(anchoEje) + 16;
        var margenDer = 18;
        var margenSup = 36;
        var margenInf = 36;
        var plot = new Rectangle(
            area.X + margenIzq,
            area.Y + margenSup,
            Math.Max(40, area.Width - margenIzq - margenDer),
            Math.Max(40, area.Height - margenSup - margenInf));

        g.DrawString($"Evolución · {MonedaPrecio.Etiqueta(moneda)}", fuenteTitulo, navy, area.X + 12, area.Y + 8);

        for (var i = 0; i <= 4; i++)
        {
            var y = plot.Bottom - (float)i / 4 * plot.Height;
            g.DrawLine(plumaGrilla, plot.Left, y, plot.Right, y);
            var valor = min + (max - min) * i / 4;
            var etiqueta = $"{MonedaPrecio.Simbolo(moneda)} {valor:N2}";
            var tam = g.MeasureString(etiqueta, fuenteEje);
            g.DrawString(etiqueta, fuenteEje, gris, plot.Left - tam.Width - 6, y - tam.Height / 2);
        }

        g.DrawRectangle(plumaEje, plot);

        PointF Punto(int indice)
        {
            var x = serie.Count == 1
                ? plot.Left + plot.Width / 2f
                : plot.Left + (float)indice / (serie.Count - 1) * plot.Width;
            var y = plot.Bottom - (float)((serie[indice].Monto - min) / (max - min) * plot.Height);
            return new PointF(x, y);
        }

        for (var i = 0; i < serie.Count - 1; i++)
        {
            var a = Punto(i);
            var b = Punto(i + 1);
            var color = serie[i + 1].Monto > serie[i].Monto
                ? Color.FromArgb(196, 43, 28)
                : serie[i + 1].Monto < serie[i].Monto
                    ? Color.FromArgb(46, 125, 79)
                    : Color.FromArgb(26, 48, 90);
            using var pluma = new Pen(color, 2.4f)
            {
                LineJoin = LineJoin.Round,
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(pluma, a, b);
        }

        using var relleno = new SolidBrush(Color.FromArgb(26, 48, 90));
        using var oro = new SolidBrush(Color.FromArgb(197, 160, 89));
        using var blanco = new SolidBrush(Color.White);
        var pasoFecha = Math.Max(1, (int)Math.Ceiling(serie.Count / 8.0));
        for (var i = 0; i < serie.Count; i++)
        {
            var p = Punto(i);
            g.FillEllipse(blanco, p.X - 5.5f, p.Y - 5.5f, 11, 11);
            g.FillEllipse(oro, p.X - 4.2f, p.Y - 4.2f, 8.4f, 8.4f);
            g.FillEllipse(relleno, p.X - 2f, p.Y - 2f, 4, 4);

            var valor = MonedaPrecio.Formato(serie[i].Monto, serie[i].Moneda);
            var tamValor = g.MeasureString(valor, fuentePrecio);
            var xValor = Math.Clamp(p.X - tamValor.Width / 2, plot.Left + 2, plot.Right - tamValor.Width - 2);
            var yValor = p.Y - tamValor.Height - 4;
            if (yValor < plot.Top + 2)
            {
                yValor = p.Y + 8;
            }

            g.FillRectangle(blanco, xValor - 1, yValor, tamValor.Width + 1, tamValor.Height);
            g.DrawString(valor, fuentePrecio, navy, xValor, yValor);

            if (i % pasoFecha == 0 || i == serie.Count - 1)
            {
                var fecha = serie[i].Fecha.ToString("dd/MM/yy");
                var tam = g.MeasureString(fecha, fuenteEje);
                var xTexto = Math.Clamp(p.X - tam.Width / 2, plot.Left, plot.Right - tam.Width);
                g.DrawString(fecha, fuenteEje, gris, xTexto, plot.Bottom + 4);
            }
        }
    }

    private static Font Fuente(float pixeles, FontStyle estilo = FontStyle.Regular) =>
        new("Segoe UI", pixeles, estilo, GraphicsUnit.Pixel);

    private static byte[] APng(Bitmap imagen)
    {
        using var memoria = new MemoryStream();
        imagen.Save(memoria, ImageFormat.Png);
        return memoria.ToArray();
    }
}
