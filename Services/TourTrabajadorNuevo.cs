namespace IdermaFichas.Services;

public enum ObjetivoTour
{
    ElementoVentana,
    ElementoPagina,
    ItemMenu,
    ItemConfiguracion
}

public sealed record PasoTour(
    string Titulo,
    string Texto,
    string? Destino,
    ObjetivoTour Objetivo,
    string? Nombre,
    bool ExpandirDashboards = false);

public static class TourTrabajadorNuevo
{
    public static IReadOnlyList<PasoTour> Pasos { get; } =
    [
        new(
            "Recorrido para personal nuevo",
            "Le vamos a mostrar el menú, las secciones de trabajo y esta pantalla de configuración. Use Siguiente para avanzar o Salir para cortar el recorrido cuando quiera.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaAyudaTrabajador"),
        new(
            "Menú lateral",
            "Aquí están todas las secciones. Pulse el icono de las tres líneas en la barra superior si el menú está cerrado. El engranaje de abajo abre Configuración.",
            "inicio",
            ObjetivoTour.ItemMenu,
            "inicio"),
        new(
            "Búsqueda",
            "Escriba el nombre de un producto: saldrá en catálogo, vencimientos, registro e historial de precios si está en esas listas. Pulse Enter o elija un resultado para ir directo.",
            "inicio",
            ObjetivoTour.ElementoVentana,
            "CajaBusquedaGlobal"),
        new(
            "Inicio",
            "Es el tablero del día: totales, vencidos, stock bajo y accesos a cada área. Conviene empezar aquí al abrir el programa.",
            "inicio",
            ObjetivoTour.ItemMenu,
            "inicio"),
        new(
            "Resumen de Inicio",
            "Estas tarjetas resumen el inventario. Pulse las de caducidad o inventario para entrar a esa lista. Más abajo verá las áreas y la actividad reciente.",
            "inicio",
            ObjetivoTour.ElementoPagina,
            "PanelKpis"),
        new(
            "Inventario",
            "Lista de todas las fichas técnicas, sin filtrar por área. Desde aquí puede buscar, exportar o abrir un producto.",
            "catalogo",
            ObjetivoTour.ItemMenu,
            "catalogo"),
        new(
            "Lista del inventario",
            "Use los botones de la derecha para exportar o borrar. Pulse una fila para abrir la ficha en su área.",
            "catalogo",
            ObjetivoTour.ElementoPagina,
            "TituloPagina"),
        new(
            "Vencimientos",
            "Materiales vencidos o por caducar, por lote. Sirve para retirar o reponer a tiempo.",
            "caducidad",
            ObjetivoTour.ItemMenu,
            "caducidad"),
        new(
            "Registro",
            "Historial de ingresos y salidas por producto y unidad. Aquí se controla el movimiento de inventario.",
            "registro",
            ObjetivoTour.ItemMenu,
            "registro"),
        new(
            "Historial de precios",
            "Lista de productos con los mismos filtros del catálogo. Al elegir uno se ve y se edita su historial de precios en soles o dólares.",
            "historial-precios",
            ObjetivoTour.ItemMenu,
            "historial-precios"),
        new(
            "Proveedores",
            "Debajo de Orden de compra. Registre proveedores, califíquelos (puntual, problemas, retraso…) y deje notas. Puede filtrar por el mes o el año en que se agregaron.",
            "proveedores",
            ObjetivoTour.ItemMenu,
            "proveedores"),
        new(
            "Áreas operativas",
            "De inicio están Insumos Médicos, Productos de Venta, Laboratorio, Materiales y Suministros y Sin área. Cada una tiene su lista de productos y su formulario. El orden se cambia en Configuración.",
            "primera-area",
            ObjetivoTour.ItemMenu,
            "primera-area"),
        new(
            "Productos del área",
            "En el área se dan de alta fichas, se editan existencias y se imprimen o exportan. El código usa el prefijo del área (por ejemplo CDE_001).",
            "primera-area",
            ObjetivoTour.ElementoPagina,
            "TituloArea"),
        new(
            "Generador de etiquetas",
            "Área logística: importa un Excel, elige el tipo de código y genera la cuadrícula de etiquetas.",
            "etiquetas",
            ObjetivoTour.ItemMenu,
            "etiquetas"),
        new(
            "Acerca de",
            "Datos del programa y de este equipo. Si hay un problema técnico, esta pantalla ayuda a identificar la versión.",
            "acerca",
            ObjetivoTour.ItemMenu,
            "acerca"),
        new(
            "Configuración",
            "El engranaje del menú abre los ajustes: autoguardado, copias, áreas, desplegables, importar y exportar. Siguiente recorre cada bloque.",
            "configuracion",
            ObjetivoTour.ItemConfiguracion,
            null),
        new(
            "Troubleshooting",
            "Comprueba si este equipo entra a la base de datos. Si falla, muestra el error y cómo corregirlo.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaTroubleshooting"),
        new(
            "Actualizar programa",
            "Iderma Launcher comprueba esta misma versión de GitHub al iniciar sesión. Aquí también puede descargarla: Instalar cierra el programa y aplica el cambio. Restaurar usa la penúltima publicada.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaActualizacion"),
        new(
            "Autoguardado",
            "Si edita una ficha, se guarda sola a los 15 segundos. El intervalo extra es un respaldo. Déjelo activado en el día a día.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaAutoguardado"),
        new(
            "Medicación común",
            "Importe un Excel con medicamentos frecuentes. En Recetas aparecerán como sugerencia al escribir el nombre.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaMedicacion"),
        new(
            "Copias de seguridad",
            "Guarda fichas, áreas, RR. HH. y el registro. Puede crear una copia ahora, restaurar una anterior o abrir la carpeta.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaCopias"),
        new(
            "Áreas en Configuración",
            "Aquí se crea o renombra un área, se elige icono, prefijo de código y tipo de formulario. Las flechas cambian el orden del menú.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaAreas"),
        new(
            "Desplegables",
            "Listas de los formularios: categoría, unidad, color, etc. Elija el área, luego el desplegable, y agregue o edite valores.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaDesplegables"),
        new(
            "Exportar información",
            "Descarga un ZIP con lo que marque: fichas, fotos, registro, preferencias. Útil para llevar datos a otro equipo.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaExportar"),
        new(
            "Archivo e importación",
            "Aquí ve la ruta de los datos. Puede importar Excel o JSON, descargar la plantilla o restaurar los ejemplos de la clínica.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaArchivoDatos"),
        new(
            "Borrar base de datos",
            "Vacía el catálogo. Antes se crea una copia con fecha. No lo use en el trabajo diario; pida apoyo si hace falta.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaBorrarBase"),
        new(
            "Listo para trabajar",
            "Ya vio el menú y la configuración. Puede repetir este recorrido cuando quiera desde esta misma tarjeta.",
            "configuracion",
            ObjetivoTour.ElementoPagina,
            "TarjetaAyudaTrabajador")
    ];
}
