using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class BusquedaGlobalService
{
    private const int MaximoTotal = 24;
    private const int MaximoSecciones = 5;
    private const int MaximoConfig = 3;
    private const int MaximoProductos = 4;
    private const int MaximoCaracteristicas = 4;
    private const int MinimoCaracteresProducto = 2;
    private const int HorizonteVencimientos = 90;

    public static IReadOnlyList<ResultadoBusquedaGlobal> Buscar(string consulta)
    {
        var filtro = IndiceBusquedaFichas.Normalizar(consulta);
        if (filtro.Length < 1)
        {
            return [];
        }

        var resultados = new List<ResultadoBusquedaGlobal>(MaximoTotal);
        Agregar(resultados, BuscarSecciones(filtro), MaximoSecciones);
        Agregar(resultados, BuscarConfiguracion(filtro), MaximoConfig);
        if (filtro.Replace(" ", string.Empty).Length >= MinimoCaracteresProducto)
        {
            Agregar(resultados, BuscarProductosYCaracteristicas(filtro), MaximoTotal);
        }

        return resultados;
    }

    private static void Agregar(
        List<ResultadoBusquedaGlobal> destino,
        IEnumerable<ResultadoBusquedaGlobal> origen,
        int cupo)
    {
        foreach (var item in origen)
        {
            if (destino.Count >= MaximoTotal || cupo-- <= 0)
            {
                return;
            }

            destino.Add(item);
        }
    }

    private static IEnumerable<ResultadoBusquedaGlobal> BuscarSecciones(string filtro)
    {
        foreach (var seccion in SeccionesFijas)
        {
            if (Coincide($"{seccion.Titulo} {seccion.Subtitulo}", filtro))
            {
                yield return seccion;
            }
        }

        foreach (var area in AreasOperativas.Todas)
        {
            if (!Coincide($"{area.Nombre} {area.Descripcion} area operativa", filtro))
            {
                continue;
            }

            yield return new ResultadoBusquedaGlobal
            {
                Titulo = area.Nombre,
                Subtitulo = string.IsNullOrWhiteSpace(area.Descripcion)
                    ? "Área operativa"
                    : area.Descripcion,
                Categoria = "Sección",
                Glifo = string.IsNullOrWhiteSpace(area.Glifo) ? "\uE8F1" : area.Glifo,
                Destino = AreasOperativas.EtiquetaMenu(area.Id),
                AreaId = area.Id
            };
        }
    }

    private static readonly ResultadoBusquedaGlobal[] SeccionesFijas =
    [
        Seccion("Inicio", "Página principal", "inicio", "\uE80F"),
        Seccion("Inventario", "Todos los productos", "catalogo", "\uE8A5"),
        Seccion("Vencimientos", "Caducidad y alertas", "caducidad", "\uE787"),
        Seccion("Registro", "Ingresos por producto y unidad", "registro", "\uE81C"),
        Seccion("Historial de precios", "Precios por producto en soles o dólares", "historial-precios", "\uE8D4"),
        Seccion("Orden de compra", "Listado, filtros y órdenes anteriores", "orden-compra", "\uE7BF"),
        Seccion("Orden de servicio", "Ampliación de contratos por meses", "orden-servicio", "\uE7C3"),
        Seccion("Proveedores", "Calificación, notas y alta por mes o año", "proveedores", "\uE716"),
        Seccion("Notificaciones", "Solicitudes de clínica: aceptar, denegar e historial", "notificaciones", "\uEA8F"),
        Seccion("Merma", "Retirar productos del inventario por vencimiento, daño u otro motivo", "merma", "\uE7BA"),
        Seccion("Sin área", "Productos sin área operativa", AreasOperativas.EtiquetaSinArea, "\uE8F1"),
        Seccion("Generador de etiquetas", "Área logística", "etiquetas", "\uE71B"),
        Seccion("Acerca de", "Información del programa", "acerca", "\uE946"),
        Seccion("Configuración", "Ajustes del programa", "configuracion", "\uE713")
    ];

    private static ResultadoBusquedaGlobal Seccion(string titulo, string subtitulo, string destino, string glifo) =>
        new()
        {
            Titulo = titulo,
            Subtitulo = subtitulo,
            Categoria = "Sección",
            Glifo = glifo,
            Destino = destino
        };

    private static IEnumerable<ResultadoBusquedaGlobal> BuscarConfiguracion(string filtro)
    {
        foreach (var ajuste in Ajustes)
        {
            if (!Coincide($"{ajuste.Titulo} {ajuste.Subtitulo} configuracion ajustes", filtro))
            {
                continue;
            }

            yield return new ResultadoBusquedaGlobal
            {
                Titulo = ajuste.Titulo,
                Subtitulo = ajuste.Subtitulo,
                Categoria = "Configuración",
                Glifo = "\uE713",
                Destino = "configuracion"
            };
        }
    }

    private static readonly (string Titulo, string Subtitulo)[] Ajustes =
    [
        ("Autoguardado", "Intervalo y activación"),
        ("Actualizar programa", "GitHub Releases y copias al reinstalar"),
        ("Copias de seguridad", "Fichas, registro, recibos e imágenes"),
        ("Áreas operativas", "Nombre, prefijo y formulario"),
        ("Desplegables de los formularios", "Categorías, unidades y listas"),
        ("Archivo de datos", "Ruta JSON e importación"),
        ("Importar Excel", "Cargar fichas desde Excel"),
        ("Exportar información", "ZIP con fichas, registro, recibos y fotos"),
        ("Exportar JSON", "Copia del catálogo"),
        ("Borrar base de datos", "Vaciar el catálogo con copia previa"),
        ("Ayuda a trabajador nuevo", "Recorrido guiado del menú y la configuración"),
        ("Troubleshooting", "Comprobar el acceso a la base de datos")
    ];

    private static IEnumerable<ResultadoBusquedaGlobal> BuscarProductosYCaracteristicas(string filtro)
    {
        var fichas = App.Instance.Repositorio.BuscarLimitado(filtro, MaximoProductos);
        var caracteristicas = 0;
        foreach (var ficha in fichas)
        {
            var nombre = string.IsNullOrWhiteSpace(ficha.Nombre) ? ficha.Codigo : ficha.Nombre;
            yield return ProductoEnSeccion(
                ficha,
                nombre,
                "Abrir en el inventario",
                "Inventario",
                "catalogo",
                "\uE8A5");

            if (EstaEnVencimientos(ficha))
            {
                yield return ProductoEnSeccion(
                    ficha,
                    nombre,
                    ficha.EstaVencido
                        ? $"Vencido · {ficha.FechaCaducidadTexto}"
                        : $"Caduca {ficha.FechaCaducidadTexto}",
                    "Vencimientos",
                    "caducidad",
                    "\uE787");
            }

            yield return ProductoEnSeccion(
                ficha,
                nombre,
                "Ingresos y salidas",
                "Registro",
                "registro",
                "\uE81C");

            var ultimoPrecio = App.Instance.HistorialPrecios.Ultimo(ficha.Id);
            yield return ProductoEnSeccion(
                ficha,
                nombre,
                ultimoPrecio is null
                    ? "Sin precio registrado"
                    : $"Último precio {MonedaPrecio.Formato(ultimoPrecio.Monto, ultimoPrecio.Moneda)}",
                "Historial de precios",
                "historial-precios",
                "\uE8D4");

            yield return ProductoEnSeccion(
                ficha,
                nombre,
                string.IsNullOrWhiteSpace(ficha.Categoria)
                    ? "Abrir ficha técnica"
                    : ficha.Categoria,
                "Ficha técnica",
                "producto",
                "\uE8A5");

            if (caracteristicas >= MaximoCaracteristicas)
            {
                continue;
            }

            foreach (var caracteristica in ficha.Caracteristicas)
            {
                if (caracteristicas >= MaximoCaracteristicas)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(caracteristica.Nombre) &&
                    string.IsNullOrWhiteSpace(caracteristica.Valor))
                {
                    continue;
                }

                if (!Coincide($"{caracteristica.Nombre} {caracteristica.Valor}", filtro))
                {
                    continue;
                }

                caracteristicas++;
                yield return new ResultadoBusquedaGlobal
                {
                    Titulo = string.IsNullOrWhiteSpace(caracteristica.Valor)
                        ? caracteristica.Nombre
                        : $"{caracteristica.Nombre}: {caracteristica.Valor}",
                    Subtitulo = $"Característica de {ficha.Nombre} ({ficha.Codigo})",
                    Categoria = "Característica",
                    Glifo = "\uE81E",
                    Destino = "producto",
                    AreaId = ficha.Area,
                    FichaId = ficha.Id
                };
            }
        }
    }

    private static ResultadoBusquedaGlobal ProductoEnSeccion(
        FichaTecnica ficha,
        string titulo,
        string detalle,
        string categoria,
        string destino,
        string glifo) =>
        new()
        {
            Titulo = titulo,
            Subtitulo = $"{ficha.Codigo} · {ficha.AreaTitulo} · {detalle}",
            Categoria = categoria,
            Glifo = glifo,
            Destino = destino,
            AreaId = ficha.Area,
            FichaId = ficha.Id
        };

    private static bool EstaEnVencimientos(FichaTecnica ficha)
    {
        if (ficha.EstaVencido)
        {
            return true;
        }

        var limite = DateTimeOffset.Now.Date.AddDays(HorizonteVencimientos);
        if (ficha.Partidas.Any(p =>
                p.Cantidad > 0.0001 &&
                p.FechaCaducidad.HasValue &&
                p.FechaCaducidad.Value.Date <= limite))
        {
            return true;
        }

        return ficha.Partidas.Count == 0
               && ficha.FechaCaducidad.HasValue
               && ficha.FechaCaducidad.Value.Date <= limite;
    }

    private static bool Coincide(string texto, string filtro) =>
        IndiceBusquedaFichas.Normalizar(texto).Contains(filtro, StringComparison.Ordinal);
}
