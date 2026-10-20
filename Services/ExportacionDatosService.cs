using System.IO.Compression;
using System.Text.Json;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public sealed class ExportacionOpciones
{
    public bool Fichas { get; set; } = true;
    public bool Caracteristicas { get; set; } = true;
    public bool Areas { get; set; } = true;
    public bool Desplegables { get; set; } = true;
    public bool RecursosHumanos { get; set; } = true;
    public bool Registro { get; set; } = true;
    public bool Recibos { get; set; } = true;
    public bool Fotos { get; set; } = true;
    public bool Preferencias { get; set; } = true;

    public bool HaySeleccion =>
        Fichas || Caracteristicas || Areas || Desplegables || RecursosHumanos
        || Registro || Recibos || Fotos || Preferencias;
}

public static class ExportacionDatosService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void CrearZip(string rutaZip, ExportacionOpciones opciones)
    {
        if (!opciones.HaySeleccion)
        {
            throw new InvalidOperationException("Elija al menos un tipo de información para exportar.");
        }

        App.Instance.Repositorio.Guardar();
        App.Instance.Rh.Guardar();
        App.Instance.PersonalMedico.Guardar();
        App.Instance.Preferencias.Guardar();

        var temporal = Path.Combine(Path.GetTempPath(), $"iderma-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporal);
        try
        {
            if (opciones.Fichas)
            {
                CopiarSiExiste(App.Instance.Repositorio.RutaArchivo, Path.Combine(temporal, "fichas-tecnicas.json"));
                CopiarSiExiste(App.Instance.HistorialPrecios.RutaArchivo, Path.Combine(temporal, "historial-precios.json"));
                CopiarSiExiste(AlmacenHistorialProveedores.RutaArchivo, Path.Combine(temporal, "historial-proveedores.json"));
                CopiarSiExiste(App.Instance.Proveedores.RutaArchivo, Path.Combine(temporal, "proveedores.json"));
            }

            if (opciones.Caracteristicas)
            {
                CopiarSiExiste(AlmacenCaracteristicas.RutaArchivo, Path.Combine(temporal, "fichas-caracteristicas.json"));
            }

            if (opciones.Areas)
            {
                CopiarSiExiste(AreasOperativas.RutaArchivo, Path.Combine(temporal, "areas-operativas.json"));
            }

            if (opciones.Desplegables)
            {
                CopiarSiExiste(Catalogos.RutaArchivo, Path.Combine(temporal, "desplegables.json"));
            }

            if (opciones.RecursosHumanos)
            {
                CopiarSiExiste(App.Instance.Rh.RutaArchivo, Path.Combine(temporal, "recursos-humanos.json"));
                CopiarSiExiste(App.Instance.PersonalMedico.RutaArchivo, Path.Combine(temporal, "personal-medico.json"));
            }

            if (opciones.Registro)
            {
                CopiarSiExiste(App.Instance.Registro.RutaArchivo, Path.Combine(temporal, "registro-ingresos.json"));
                CopiarSiExiste(App.Instance.OrdenesCompra.RutaArchivo, Path.Combine(temporal, "ordenes-compra.json"));
                CopiarSiExiste(App.Instance.Mermas.RutaArchivo, Path.Combine(temporal, "merma.json"));
                CopiarSiExiste(App.Instance.ControlMantenimiento.RutaArchivo, Path.Combine(temporal, "control-mantenimiento.json"));
            }

            if (opciones.Preferencias)
            {
                CopiarSiExiste(App.Instance.Preferencias.RutaArchivo, Path.Combine(temporal, "preferencias.json"));
            }

            if (opciones.Fotos && Directory.Exists(ImagenFichaService.Carpeta))
            {
                CopiarDirectorio(ImagenFichaService.Carpeta, Path.Combine(temporal, "imagenes-fichas"));
            }

            if (opciones.Recibos && Directory.Exists(ReciboMovimientoService.Carpeta))
            {
                CopiarDirectorio(ReciboMovimientoService.Carpeta, Path.Combine(temporal, "recibos-movimientos"));
            }

            File.WriteAllText(
                Path.Combine(temporal, "contenido.json"),
                JsonSerializer.Serialize(new
                {
                    fecha = DateTimeOffset.Now,
                    fichas = opciones.Fichas,
                    caracteristicas = opciones.Caracteristicas,
                    areas = opciones.Areas,
                    desplegables = opciones.Desplegables,
                    recursosHumanos = opciones.RecursosHumanos,
                    registro = opciones.Registro,
                    recibos = opciones.Recibos,
                    fotos = opciones.Fotos,
                    preferencias = opciones.Preferencias
                }, JsonOptions));

            if (File.Exists(rutaZip))
            {
                File.Delete(rutaZip);
            }

            ZipFile.CreateFromDirectory(temporal, rutaZip, CompressionLevel.Fastest, includeBaseDirectory: false);
        }
        finally
        {
            try
            {
                Directory.Delete(temporal, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static void CopiarSiExiste(string origen, string destino)
    {
        if (File.Exists(origen))
        {
            File.Copy(origen, destino, overwrite: true);
        }
    }

    private static void CopiarDirectorio(string origen, string destino)
    {
        Directory.CreateDirectory(destino);
        foreach (var archivo in Directory.EnumerateFiles(origen))
        {
            File.Copy(archivo, Path.Combine(destino, Path.GetFileName(archivo)), overwrite: true);
        }
    }
}
