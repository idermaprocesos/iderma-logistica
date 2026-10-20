namespace IdermaFichas.Models;

public sealed record IconoAreaOpcion(string Nombre, string Glifo);

public static class IconosArea
{
    public static IReadOnlyList<IconoAreaOpcion> Opciones { get; } =
    [
        new("Clínica", "\uE95E"),
        new("Salud", "\uE95B"),
        new("Hospital", "\uE91B"),
        new("Pastilla", "\uEDA2"),
        new("Estetoscopio", "\uEA37"),
        new("Corazón", "\uEB51"),
        new("Estrella", "\uE734"),
        new("Estrella rellena", "\uE735"),
        new("Limpieza", "\uEA18"),
        new("Escudo", "\uE730"),
        new("Candado", "\uE72E"),
        new("Carpeta", "\uE8F1"),
        new("Abrir", "\uE838"),
        new("Documento", "\uE8A5"),
        new("Página", "\uE7C3"),
        new("Portapapeles", "\uE8A1"),
        new("Buscar", "\uE81E"),
        new("Lista", "\uE14C"),
        new("Paquete", "\uE7B8"),
        new("Contacto", "\uE8B7"),
        new("Chincheta", "\uE718"),
        new("Personas", "\uE716"),
        new("Usuario", "\uE77B"),
        new("Casa", "\uE80F"),
        new("Inicio", "\uE10F"),
        new("Ciudad", "\uEC07"),
        new("Calendario", "\uE787"),
        new("Reloj", "\uE121"),
        new("Teléfono", "\uE717"),
        new("Correo", "\uE715"),
        new("Mensaje", "\uE8BD"),
        new("Cámara", "\uE722"),
        new("Foto", "\uEB9F"),
        new("Imagen", "\uE158"),
        new("Vídeo", "\uE714"),
        new("Mundo", "\uE774"),
        new("Mapa", "\uE707"),
        new("Ubicación", "\uE1C4"),
        new("Tienda", "\uE14D"),
        new("Carrito", "\uE7BF"),
        new("Dinero", "\uE8D4"),
        new("Banco", "\uE825"),
        new("Camión", "\uE7EC"),
        new("Coche", "\uE804"),
        new("Avión", "\uE709"),
        new("Impresora", "\uE749"),
        new("Monitor", "\uE7F4"),
        new("Portátil", "\uE7F8"),
        new("Tablet", "\uE70A"),
        new("Wi‑Fi", "\uE701"),
        new("Bluetooth", "\uE702"),
        new("Engranaje", "\uE713"),
        new("Filtro", "\uE71D"),
        new("Llave", "\uE192"),
        new("Bombilla", "\uEA80"),
        new("Rayo", "\uE945"),
        new("Gráfico", "\uE9D2"),
        new("Calculadora", "\uE8EF"),
        new("Laboratorio", "\uE90F"),
        new("Vista", "\uE8B3"),
        new("Gafas", "\uEA5F"),
        new("Bandera", "\uE7C1"),
        new("Clip", "\uE71E"),
        new("Nube", "\uE753"),
        new("Descargar", "\uE896"),
        new("Subir", "\uE898"),
        new("Sincronizar", "\uE895"),
        new("Advertencia", "\uE7BA"),
        new("Información", "\uE946"),
        new("Ayuda", "\uE897"),
        new("Comentario", "\uE8F2"),
        new("Volumen", "\uE767"),
        new("Música", "\uE8D6"),
        new("Comida", "\uE7ED"),
        new("Flor", "\uEA35"),
        new("Hoja", "\uE90E"),
        new("Temperatura", "\uE9CA"),
        new("Papelera", "\uE74D"),
        new("Reciclar", "\uE75C"),
        new("Robot", "\uE99A"),
        new("Batería", "\uE83F"),
        new("Sol", "\uE706"),
        new("Luna", "\uE708"),
        new("Regalo", "\uE8F6"),
        new("Me gusta", "\uE8E1"),
        new("Enviar", "\uE724"),
        new("Guardar", "\uE74E"),
        new("Copiar", "\uE8C8"),
        new("Editar", "\uE70F"),
        new("Añadir", "\uE710"),
        new("Quitar", "\uE711"),
        new("Aceptar", "\uE73E"),
        new("Cerrar", "\uE10A"),
        new("Libreta", "\uEB42"),
        new("Etiqueta tienda", "\uE8EC"),
        new("Contacto info", "\uEA8C"),
        new("Ambulancia", "\uE95C"),
        new("Termómetro", "\uE9C9"),
        new("Gotas", "\uE912")
    ];

    public static int IndiceDe(IReadOnlyList<IconoAreaOpcion> opciones, string? glifo)
    {
        if (string.IsNullOrWhiteSpace(glifo))
        {
            return -1;
        }

        for (var i = 0; i < opciones.Count; i++)
        {
            if (GlifosWinUi.Mismo(opciones[i].Glifo, glifo))
            {
                return i;
            }
        }

        return -1;
    }
}
