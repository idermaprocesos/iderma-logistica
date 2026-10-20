using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using IdermaFichas.Models;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;
using ZXing.Rendering;

namespace IdermaFichas.Services;

public static class SimboloEtiquetaService
{
    public static string Titulo(TipoSimboloEtiqueta tipo) => tipo switch
    {
        TipoSimboloEtiqueta.Qr => "Código QR",
        TipoSimboloEtiqueta.Code128 => "Código de barras (Code 128)",
        TipoSimboloEtiqueta.Code39 => "Code 39",
        TipoSimboloEtiqueta.Ean13 => "EAN-13",
        TipoSimboloEtiqueta.DataMatrix => "Data Matrix",
        TipoSimboloEtiqueta.Pdf417 => "PDF417",
        TipoSimboloEtiqueta.Aztec => "Aztec",
        _ => "Símbolo"
    };

    public static bool EsBidimensional(TipoSimboloEtiqueta tipo) => tipo is
        TipoSimboloEtiqueta.Qr or
        TipoSimboloEtiqueta.DataMatrix or
        TipoSimboloEtiqueta.Aztec;

    private static readonly ConcurrentDictionary<string, byte[]> Cache = new(StringComparer.Ordinal);

    public static void Precargar(
        IEnumerable<string> contenidos,
        TipoSimboloEtiqueta tipo,
        float tamanoMm,
        IProgress<(int Hechos, int Total)>? progreso = null)
    {
        var lista = contenidos
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (lista.Count == 0)
        {
            return;
        }

        var hechos = 0;
        Parallel.ForEach(lista, contenido =>
        {
            Generar(contenido, tipo, tamanoMm);
            var actual = Interlocked.Increment(ref hechos);
            progreso?.Report((actual, lista.Count));
        });
    }

    public static byte[] Generar(string contenido, TipoSimboloEtiqueta tipo, float tamanoMm)
    {
        var clave = $"{tipo}|{tamanoMm:0.#}|{contenido.Trim()}";
        return Cache.GetOrAdd(clave, _ => Crear(contenido, tipo, tamanoMm));
    }

    private static byte[] Crear(string contenido, TipoSimboloEtiqueta tipo, float tamanoMm)
    {
        var px = Math.Max(48, (int)Math.Round(tamanoMm * 8));
        var formato = Formato(tipo);
        var texto = PrepararContenido(contenido, tipo);

        var opciones = tipo == TipoSimboloEtiqueta.Qr
            ? new QrCodeEncodingOptions
            {
                Width = px,
                Height = px,
                Margin = 0,
                CharacterSet = "UTF-8",
                ErrorCorrection = ErrorCorrectionLevel.M
            }
            : new EncodingOptions
            {
                Width = EsBidimensional(tipo) ? px : Math.Max(px * 3, 180),
                Height = tipo == TipoSimboloEtiqueta.Pdf417 ? Math.Max(px / 2, 48) : px,
                Margin = 0,
                PureBarcode = true
            };

        opciones.Hints[EncodeHintType.CHARACTER_SET] = "UTF-8";

        var escritor = new BarcodeWriterPixelData
        {
            Format = formato,
            Options = opciones
        };

        try
        {
            return APng(escritor.Write(texto));
        }
        catch (Exception) when (tipo == TipoSimboloEtiqueta.Ean13)
        {
            return Crear(contenido, TipoSimboloEtiqueta.Code128, tamanoMm);
        }
    }

    private static BarcodeFormat Formato(TipoSimboloEtiqueta tipo) => tipo switch
    {
        TipoSimboloEtiqueta.Qr => BarcodeFormat.QR_CODE,
        TipoSimboloEtiqueta.Code128 => BarcodeFormat.CODE_128,
        TipoSimboloEtiqueta.Code39 => BarcodeFormat.CODE_39,
        TipoSimboloEtiqueta.Ean13 => BarcodeFormat.EAN_13,
        TipoSimboloEtiqueta.DataMatrix => BarcodeFormat.DATA_MATRIX,
        TipoSimboloEtiqueta.Pdf417 => BarcodeFormat.PDF_417,
        TipoSimboloEtiqueta.Aztec => BarcodeFormat.AZTEC,
        _ => BarcodeFormat.QR_CODE
    };

    private static string PrepararContenido(string contenido, TipoSimboloEtiqueta tipo)
    {
        var texto = string.IsNullOrWhiteSpace(contenido) ? "IDERMA" : contenido.Trim();
        if (tipo != TipoSimboloEtiqueta.Ean13)
        {
            return texto;
        }

        var digitos = new string(texto.Where(char.IsDigit).ToArray());
        if (digitos.Length is 12 or 13)
        {
            return digitos.Length == 13 ? digitos[..12] : digitos;
        }

        throw new ArgumentException("EAN-13 requiere 12 o 13 dígitos.");
    }

    private static byte[] APng(PixelData datos)
    {
        // ZXing centra el símbolo en un lienzo más grande. Ese marco blanco,
        // al escalarse con el PDF, achica el código: un QR de 12 mm salía a unos 10 mm.
        var origen = RecortarAlSimbolo(datos);
        using var bitmap = new Bitmap(origen.Width, origen.Height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, origen.Width, origen.Height);
        var bits = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(origen.Pixels, 0, bits.Scan0, origen.Pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(bits);
        }

        using var memoria = new MemoryStream();
        bitmap.Save(memoria, ImageFormat.Png);
        return memoria.ToArray();
    }

    private static Lienzo RecortarAlSimbolo(PixelData datos)
    {
        var x0 = datos.Width;
        var y0 = datos.Height;
        var x1 = -1;
        var y1 = -1;
        for (var y = 0; y < datos.Height; y++)
        {
            for (var x = 0; x < datos.Width; x++)
            {
                var i = (y * datos.Width + x) * 4;
                if (datos.Pixels[i] >= 250 && datos.Pixels[i + 1] >= 250 && datos.Pixels[i + 2] >= 250)
                {
                    continue;
                }

                if (x < x0)
                {
                    x0 = x;
                }

                if (y < y0)
                {
                    y0 = y;
                }

                if (x > x1)
                {
                    x1 = x;
                }

                if (y > y1)
                {
                    y1 = y;
                }
            }
        }

        if (x1 < x0 || y1 < y0)
        {
            return new Lienzo(datos.Width, datos.Height, datos.Pixels);
        }

        var ancho = x1 - x0 + 1;
        var alto = y1 - y0 + 1;
        var escala = Math.Max(1, (int)Math.Ceiling(320.0 / Math.Max(ancho, alto)));
        var destinoAncho = ancho * escala;
        var destinoAlto = alto * escala;
        var pixels = new byte[destinoAncho * destinoAlto * 4];
        for (var y = 0; y < alto; y++)
        {
            for (var x = 0; x < ancho; x++)
            {
                var origen = ((y0 + y) * datos.Width + (x0 + x)) * 4;
                for (var dy = 0; dy < escala; dy++)
                {
                    var fila = ((y * escala + dy) * destinoAncho + x * escala) * 4;
                    for (var dx = 0; dx < escala; dx++)
                    {
                        var destino = fila + dx * 4;
                        pixels[destino] = datos.Pixels[origen];
                        pixels[destino + 1] = datos.Pixels[origen + 1];
                        pixels[destino + 2] = datos.Pixels[origen + 2];
                        pixels[destino + 3] = datos.Pixels[origen + 3];
                    }
                }
            }
        }

        return new Lienzo(destinoAncho, destinoAlto, pixels);
    }

    private readonly record struct Lienzo(int Width, int Height, byte[] Pixels);
}
