using System.Text.Json;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public sealed class BackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string CarpetaRaiz { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "copias");

    public CopiaSeguridadInfo Crear(string origen, bool forzarImagenes = false)
    {
        lock (App.Instance.Repositorio.SyncRoot)
        {
            Directory.CreateDirectory(CarpetaRaiz);
            var marca = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var destino = Path.Combine(CarpetaRaiz, marca);
            Directory.CreateDirectory(destino);

            var preferencias = App.Instance.Preferencias.Actual;
            CopiarSiExiste(App.Instance.Repositorio.RutaArchivo, Path.Combine(destino, "fichas-tecnicas.json"));
            CopiarSiExiste(AlmacenCaracteristicas.RutaArchivo, Path.Combine(destino, "fichas-caracteristicas.json"));
            CopiarSiExiste(AreasOperativas.RutaArchivo, Path.Combine(destino, "areas-operativas.json"));
            CopiarSiExiste(Catalogos.RutaArchivo, Path.Combine(destino, "desplegables.json"));
            CopiarSiExiste(App.Instance.Rh.RutaArchivo, Path.Combine(destino, "recursos-humanos.json"));
            CopiarSiExiste(App.Instance.PersonalMedico.RutaArchivo, Path.Combine(destino, "personal-medico.json"));
            CopiarSiExiste(App.Instance.Registro.RutaArchivo, Path.Combine(destino, "registro-ingresos.json"));
            CopiarSiExiste(App.Instance.HistorialPrecios.RutaArchivo, Path.Combine(destino, "historial-precios.json"));
            CopiarSiExiste(App.Instance.OrdenesCompra.RutaArchivo, Path.Combine(destino, "ordenes-compra.json"));
            CopiarSiExiste(App.Instance.Proveedores.RutaArchivo, Path.Combine(destino, "proveedores.json"));
            CopiarSiExiste(App.Instance.Mermas.RutaArchivo, Path.Combine(destino, "merma.json"));
            CopiarSiExiste(App.Instance.ControlMantenimiento.RutaArchivo, Path.Combine(destino, "control-mantenimiento.json"));

            var incluyeImagenes = forzarImagenes || preferencias.CopiasIncluyenImagenes;
            if (incluyeImagenes && Directory.Exists(ImagenFichaService.Carpeta))
            {
                CopiarDirectorio(ImagenFichaService.Carpeta, Path.Combine(destino, "imagenes-fichas"));
            }

            var incluyeRecibos = Directory.Exists(ReciboMovimientoService.Carpeta) &&
                                 Directory.EnumerateFiles(ReciboMovimientoService.Carpeta).Any();
            if (incluyeRecibos)
            {
                CopiarDirectorio(ReciboMovimientoService.Carpeta, Path.Combine(destino, "recibos-movimientos"));
            }

            var manifiesto = new ManifiestoCopia
            {
                Fecha = DateTimeOffset.Now,
                Origen = origen,
                Fichas = App.Instance.Repositorio.Todas.Count,
                Areas = AreasOperativas.Todas.Count,
                Movimientos = App.Instance.Registro.Cantidad,
                Recibos = incluyeRecibos
                    ? Directory.EnumerateFiles(ReciboMovimientoService.Carpeta).Count()
                    : 0,
                IncluyeImagenes = incluyeImagenes,
                IncluyeRecibos = incluyeRecibos
            };
            File.WriteAllText(Path.Combine(destino, "manifiesto.json"), JsonSerializer.Serialize(manifiesto, JsonOptions));

            preferencias.UltimaCopiaUtc = DateTimeOffset.UtcNow;
            App.Instance.Preferencias.Guardar();
            RecortarExcedentes(preferencias.MaximoCopias);

            return new CopiaSeguridadInfo
            {
                Carpeta = destino,
                Fecha = manifiesto.Fecha,
                Origen = manifiesto.Origen,
                Fichas = manifiesto.Fichas,
                Areas = manifiesto.Areas,
                Movimientos = manifiesto.Movimientos,
                Recibos = manifiesto.Recibos,
                IncluyeImagenes = manifiesto.IncluyeImagenes,
                IncluyeRecibos = manifiesto.IncluyeRecibos
            };
        }
    }

    public bool DebeCrearAutomatica()
    {
        var preferencias = App.Instance.Preferencias.Actual;
        if (!preferencias.CopiasActivas)
        {
            return false;
        }

        if (preferencias.UltimaCopiaUtc is not DateTimeOffset ultima)
        {
            return true;
        }

        return DateTimeOffset.UtcNow - ultima >= TimeSpan.FromHours(preferencias.IntervaloCopiasHoras);
    }

    public IReadOnlyList<CopiaSeguridadInfo> Listar()
    {
        if (!Directory.Exists(CarpetaRaiz))
        {
            return [];
        }

        var lista = new List<CopiaSeguridadInfo>();
        foreach (var carpeta in Directory.EnumerateDirectories(CarpetaRaiz))
        {
            var manifiestoRuta = Path.Combine(carpeta, "manifiesto.json");
            if (File.Exists(manifiestoRuta))
            {
                try
                {
                    var manifiesto = JsonSerializer.Deserialize<ManifiestoCopia>(
                        File.ReadAllText(manifiestoRuta), JsonOptions);
                    if (manifiesto is null)
                    {
                        continue;
                    }

                    lista.Add(new CopiaSeguridadInfo
                    {
                        Carpeta = carpeta,
                        Fecha = manifiesto.Fecha,
                        Origen = manifiesto.Origen,
                        Fichas = manifiesto.Fichas,
                        Areas = manifiesto.Areas,
                        Movimientos = manifiesto.Movimientos,
                        Recibos = manifiesto.Recibos,
                        IncluyeImagenes = manifiesto.IncluyeImagenes,
                        IncluyeRecibos = manifiesto.IncluyeRecibos
                    });
                    continue;
                }
                catch
                {
                    // Se trata como carpeta incompleta más abajo.
                }
            }

            var fecha = Directory.GetCreationTime(carpeta);
            lista.Add(new CopiaSeguridadInfo
            {
                Carpeta = carpeta,
                Fecha = new DateTimeOffset(fecha),
                Origen = "manual",
                Fichas = 0,
                Areas = 0,
                IncluyeImagenes = Directory.Exists(Path.Combine(carpeta, "imagenes-fichas")),
                IncluyeRecibos = Directory.Exists(Path.Combine(carpeta, "recibos-movimientos"))
            });
        }

        foreach (var archivo in Directory.EnumerateFiles(CarpetaRaiz, "*.json"))
        {
            lista.Add(new CopiaSeguridadInfo
            {
                Carpeta = archivo,
                Fecha = new DateTimeOffset(File.GetCreationTime(archivo)),
                Origen = "antes-de-borrar",
                Fichas = 0,
                Areas = 0,
                IncluyeImagenes = false
            });
        }

        return lista.OrderByDescending(c => c.Fecha).ToList();
    }

    public void Restaurar(string ruta)
    {
        lock (App.Instance.Repositorio.SyncRoot)
        {
            if (File.Exists(ruta) && ruta.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(ruta, App.Instance.Repositorio.RutaArchivo, overwrite: true);
            }
            else
            {
                if (!Directory.Exists(ruta))
                {
                    throw new InvalidOperationException("La copia de seguridad ya no existe.");
                }

                RestaurarArchivo(Path.Combine(ruta, "fichas-tecnicas.json"), App.Instance.Repositorio.RutaArchivo);
                RestaurarArchivo(Path.Combine(ruta, "fichas-caracteristicas.json"), AlmacenCaracteristicas.RutaArchivo);
                RestaurarArchivo(Path.Combine(ruta, "areas-operativas.json"), AreasOperativas.RutaArchivo);
                RestaurarArchivo(Path.Combine(ruta, "desplegables.json"), Catalogos.RutaArchivo);
                RestaurarArchivo(Path.Combine(ruta, "recursos-humanos.json"), App.Instance.Rh.RutaArchivo);
                RestaurarArchivo(Path.Combine(ruta, "personal-medico.json"), App.Instance.PersonalMedico.RutaArchivo);
                var registroCopia = Path.Combine(ruta, "registro-ingresos.json");
                if (File.Exists(registroCopia))
                {
                    RestaurarArchivo(registroCopia, App.Instance.Registro.RutaArchivo);
                }
                else if (File.Exists(App.Instance.Registro.RutaArchivo))
                {
                    File.Delete(App.Instance.Registro.RutaArchivo);
                }

                var preciosCopia = Path.Combine(ruta, "historial-precios.json");
                if (File.Exists(preciosCopia))
                {
                    RestaurarArchivo(preciosCopia, App.Instance.HistorialPrecios.RutaArchivo);
                }
                else if (File.Exists(App.Instance.HistorialPrecios.RutaArchivo))
                {
                    File.Delete(App.Instance.HistorialPrecios.RutaArchivo);
                }

                var ordenesCopia = Path.Combine(ruta, "ordenes-compra.json");
                if (File.Exists(ordenesCopia))
                {
                    RestaurarArchivo(ordenesCopia, App.Instance.OrdenesCompra.RutaArchivo);
                }

                var mermaCopia = Path.Combine(ruta, "merma.json");
                if (File.Exists(mermaCopia))
                {
                    RestaurarArchivo(mermaCopia, App.Instance.Mermas.RutaArchivo);
                }

                var controlCopia = Path.Combine(ruta, "control-mantenimiento.json");
                if (File.Exists(controlCopia))
                {
                    RestaurarArchivo(controlCopia, App.Instance.ControlMantenimiento.RutaArchivo);
                }

                var proveedoresCopia = Path.Combine(ruta, "proveedores.json");
                if (File.Exists(proveedoresCopia))
                {
                    RestaurarArchivo(proveedoresCopia, App.Instance.Proveedores.RutaArchivo);
                }

                var imagenes = Path.Combine(ruta, "imagenes-fichas");
                if (Directory.Exists(imagenes))
                {
                    ReemplazarDirectorio(imagenes, ImagenFichaService.Carpeta);
                }

                var recibos = Path.Combine(ruta, "recibos-movimientos");
                if (Directory.Exists(recibos))
                {
                    ReemplazarDirectorio(recibos, ReciboMovimientoService.Carpeta);
                }
            }
        }

        App.RecargarDatos();
    }

    private void RecortarExcedentes(int maximo)
    {
        var copias = Directory.Exists(CarpetaRaiz)
            ? Directory.GetDirectories(CarpetaRaiz)
                .OrderByDescending(Directory.GetCreationTimeUtc)
                .ToList()
            : [];

        foreach (var antigua in copias.Skip(Math.Max(1, maximo)))
        {
            try
            {
                Directory.Delete(antigua, recursive: true);
            }
            catch
            {
                // Si un archivo está en uso, se deja y se recorta en la siguiente copia.
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

    private static void RestaurarArchivo(string origen, string destino)
    {
        if (!File.Exists(origen))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        File.Copy(origen, destino, overwrite: true);
    }

    private static void ReemplazarDirectorio(string origen, string destino)
    {
        if (Directory.Exists(destino))
        {
            try
            {
                Directory.Delete(destino, recursive: true);
            }
            catch
            {
                foreach (var archivo in Directory.EnumerateFiles(destino).ToList())
                {
                    try
                    {
                        File.Delete(archivo);
                    }
                    catch
                    {
                    }
                }
            }
        }

        CopiarDirectorio(origen, destino);
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
