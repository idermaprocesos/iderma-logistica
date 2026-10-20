using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

const string Navy = "#1A305A";
const string Oro = "#C5A059";
const string Fondo = "#F4EFE4";
const string Gris = "#5A6570";

var carpeta = @"c:\Users\Iderma Capilar 1\Documents\ficha2\docs";
Directory.CreateDirectory(carpeta);
var salida = Path.Combine(carpeta, "Instructivo-Fichas-tecnicas-Iderma.pdf");

var logo = new[]
{
    Path.Combine(@"c:\Users\Iderma Capilar 1\Documents\ficha2", "img", "IDERMA LOGO.jpg"),
    Path.Combine(AppContext.BaseDirectory, "img", "IDERMA LOGO.jpg")
}.FirstOrDefault(File.Exists);

Document.Create(contenedor =>
{
    contenedor.Page(pagina =>
    {
        pagina.Size(PageSizes.A4);
        pagina.MarginHorizontal(36);
        pagina.MarginTop(26);
        pagina.MarginBottom(22);
        pagina.DefaultTextStyle(x => x.FontFamily(Fonts.Calibri).FontSize(10).FontColor(Navy).LineHeight(1.28f));

        pagina.Header().Column(col =>
        {
            col.Item().Row(fila =>
            {
                if (logo is not null)
                {
                    fila.ConstantItem(52).Height(40).Image(logo).FitArea();
                    fila.ConstantItem(10);
                }

                fila.RelativeItem().Column(t =>
                {
                    t.Item().Text("IDERMA CAPILAR").FontSize(16).Bold().FontColor(Navy);
                    t.Item().Text("Clínica de trasplante e implante capilar").FontSize(9).FontColor(Oro);
                    t.Item().Text("Manual interno · Fichas técnicas de materiales")
                        .FontSize(9).FontColor(Gris);
                });
            });
            col.Item().PaddingTop(8).Height(3).Background(Oro);
        });

        pagina.Footer().Row(fila =>
        {
            fila.RelativeItem().Text("Uso interno · Iderma Capilar · No sustituye la etiqueta del fabricante")
                .FontSize(7.5f).FontColor(Gris);
            fila.AutoItem().Text(t =>
            {
                t.Span("Pág. ").FontSize(7.5f).FontColor(Gris);
                t.CurrentPageNumber().FontSize(7.5f).FontColor(Gris);
                t.Span(" / ").FontSize(7.5f).FontColor(Gris);
                t.TotalPages().FontSize(7.5f).FontColor(Gris);
            });
        });

        pagina.Content().PaddingTop(14).Column(col =>
        {
            col.Spacing(9);

            col.Item().Text("MANUAL DE USO — PASO A PASO").FontSize(11).Bold().FontColor(Oro);
            col.Item().Text("Fichas técnicas de materiales").FontSize(18).Bold();
            col.Item().Text($"Versión 2.0  ·  {DateTime.Now:dd/MM/yyyy}  ·  Del arranque al consolidado PDF/Excel")
                .FontSize(9).FontColor(Gris);

            Caja(col, "Qué cubre este manual",
                "Desde abrir el programa hasta crear, editar, buscar, importar, exportar una ficha y armar un consolidado de varias. " +
                "Al final hay una guía de errores y soluciones. No cubre Recursos humanos ni el Generador de etiquetas.");

            Titulo(col, "1. Ideas que debe tener claras antes de empezar");
            Parrafo(col, "Una ficha técnica es la ficha de un material (insumo, equipo o producto de limpieza): código, nombre, datos físicos, técnicos, procedencia y campos del área. El catálogo es único y se guarda en esta computadora (carpeta local IdermaCapilar); no necesita internet.");
            Viñeta(col, "Tres áreas: Clínica (procedimiento), Oficina (administración) y Limpieza (desinfectantes y EPP). Cada ficha nace en un área y se queda ahí.");
            Viñeta(col, "Marcar no es lo mismo que abrir. El recuadro (checkbox) a la izquierda sirve para consolidar o exportar. Pulsar el nombre abre el formulario.");
            Viñeta(col, "Una ficha = botón «Exportar ficha». Varias fichas = botón «Exportar seleccionadas» (consolidado).");
            Viñeta(col, "Hasta que pulse Guardar ficha, los cambios no quedan en el catálogo.");

            Titulo(col, "2. Arrancar el programa (paso a paso)");
            Paso(col, "1", "Encienda la computadora e inicie sesión de Windows.");
            Paso(col, "2", "Abra Iderma Capilar · Fichas técnicas (acceso directo o menú Inicio).");
            Paso(col, "3", "Espere a que cargue. La primera pantalla es Inicio (icono de casa en el menú izquierdo).");
            Paso(col, "4", "Si no ve el menú, pulse el botón de panel en la barra de título para mostrarlo u ocultarlo.");
            Paso(col, "5", "En Inicio verá: tres tarjetas (Clínica, Oficina, Limpieza) con el número de fichas; recuadros de Productos vencidos y Próximos a vencer (30 días); y Fichas recientes.");
            Paso(col, "6", "Pulse una tarjeta de área para ir a esa sección, un recuadro de caducidad para ir a Vencimientos, o una ficha reciente para abrirla en su área.");

            Titulo(col, "3. Mapa del menú");
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.15f);
                    c.RelativeColumn(1.85f);
                });
                EncabezadoTabla(t, "Menú", "Para qué sirve");
                FilaTabla(t, "Inicio", "Resumen, accesos por área y alertas de caducidad.");
                FilaTabla(t, "Catálogo", "Todas las fichas juntas. Buscar, filtrar por área, marcar y consolidar.");
                FilaTabla(t, "Vencimientos", "Vencidos y próximos a caducar (horizonte 30, 60 o 90 días).");
                FilaTabla(t, "Clínica / Oficina / Limpieza", "Alta, edición, baja, importación Excel y exportación de esa área.");
                FilaTabla(t, "Configuración (engranaje abajo)", "Listas de desplegables, plantilla Excel, JSON y vaciar base.");
            });

            Titulo(col, "4. Crear una ficha nueva (inicio a fin)");
            Paso(col, "1", "Entre al área correcta: Clínica, Oficina o Limpieza (menú o tarjeta de Inicio). La ficha nacerá ahí; no se cambia de área después salvo importando de nuevo con otra Área.");
            Paso(col, "2", "Arriba a la derecha pulse Nueva ficha (botón destacado).");
            Paso(col, "3", "A la derecha aparece el formulario. A la izquierda la lista no cambia hasta que guarde.");
            Paso(col, "4", "Pestaña Identificación: el código se propone solo (CLI-…, OFI-… o LIM-…). Puede dejarlo o escribir otro. El nombre del material es obligatorio. Complete categoría, unidad y descripción si aplica.");
            Paso(col, "5", "Pestaña Físicas: estado, color, presentación, dimensiones, peso/volumen y material. En los desplegables puede elegir o escribir un valor propio.");
            Paso(col, "6", "Pestaña Técnicas: marca, modelo, normas, vida útil, existencia, stock mínimo, especificaciones, almacenamiento e instrucciones de uso.");
            Paso(col, "7", "Pestaña Procedencia: fabricante, país, planta, tipo de origen, proveedor, contacto, lote, factura, fecha de adquisición y registro sanitario.");
            Paso(col, "8", "Pestaña Área (cambia según donde esté):");
            Viñeta(col, "Clínica: uso clínico, procedimiento, riesgo biológico, temperatura, fecha de caducidad, estéril, refrigeración, reutilizable, composición y contraindicaciones.");
            Viñeta(col, "Oficina: ubicación, número de serie, responsable, garantía, voltaje, mantenimiento, compatibilidad.");
            Viñeta(col, "Limpieza: principio activo, concentración, pH, tiempo de contacto, dilución, peligrosidad, SDS, EPP, superficies, compatibilidad y residuo.");
            Paso(col, "9", "Pestaña Notas: observaciones internas, alertas de lote o acuerdos con el proveedor.");
            Paso(col, "10", "Si un desplegable no trae el valor que necesita, vaya a Configuración, agréguelo y vuelva a la ficha.");
            Paso(col, "11", "Pulse Guardar ficha. Debe aparecer «Ficha técnica guardada». Si falta código o nombre verá: «El código y el nombre son obligatorios.» y no guarda.");
            Paso(col, "12", "La ficha aparece en la lista de la izquierda. El código se guarda en mayúsculas.");
            Paso(col, "13", "Si se equivocó y aún no quiere guardar, pulse Descartar cambios (vuelve a lo último guardado o deja el formulario vacío).");

            Titulo(col, "5. Buscar, ordenar y abrir una ficha");
            Paso(col, "1", "En el área, escriba en «Buscar por código, nombre o marca». La lista se filtra al instante.");
            Paso(col, "2", "El desplegable de orden: Nombre A–Z o Z–A, Código A–Z, existencia menor/mayor, caducidad más próxima o más lejana.");
            Paso(col, "3", "Pulse el nombre (no el checkbox) para abrir el formulario a la derecha.");
            Paso(col, "4", "En Catálogo: busque también por proveedor o fabricante; filtre Todas las áreas / Clínica / Oficina / Limpieza; pulse una fila para saltar al área y abrir esa ficha.");

            Titulo(col, "6. Editar una ficha");
            Paso(col, "1", "Ábrala (lista del área, Catálogo o Fichas recientes).");
            Paso(col, "2", "Cambie los campos en las pestañas que correspondan.");
            Paso(col, "3", "Pulse Guardar ficha. Sin Guardar, al cambiar de ficha o de pantalla puede perder lo escrito.");
            Paso(col, "4", "Descartar cambios restaura lo último guardado de esa ficha.");

            Titulo(col, "7. Eliminar una ficha");
            Paso(col, "1", "Ábrala en su área.");
            Paso(col, "2", "Abajo a la derecha pulse Eliminar.");
            Paso(col, "3", "Confirme en el cuadro de diálogo. Aparece «La ficha se eliminó del catálogo.»");
            Paso(col, "4", "Solo se borra del sistema. Los PDF o Excel que ya haya exportado no se borran.");

            col.Item().PageBreak();
            Titulo(col, "8. Exportar UNA sola ficha (PDF o Excel)");
            Parrafo(col, "Sirve para imprimir, archivar o enviar un material. Siempre aparece el cuadro Guardar: elija carpeta, nombre y tipo (PDF o Excel). Nombre sugerido: ficha-CODIGO.");
            Subtitulo(col, "8.1 Desde Clínica, Oficina o Limpieza");
            Paso(col, "1", "Abra la ficha (nombre) o márquela con el recuadro de la izquierda.");
            Paso(col, "2", "Opción A — en el formulario: «Exportar esta ficha (PDF/Excel)».");
            Paso(col, "3", "Opción B — icono de documento a la derecha de la fila en la lista.");
            Paso(col, "4", "Opción C — botón superior «Exportar ficha (PDF/Excel)»: si marcó exactamente una, exporta esa; si no, exporta la que está abierta.");
            Paso(col, "5", "Si no hay ficha abierta ni una sola marcada: «Abra una ficha, márquela o pulse el botón de exportar en su fila.»");
            Paso(col, "6", "Elija PDF (documento impreso) o Excel (tabla editable). Confirme Guardar.");
            Paso(col, "7", "Si pregunta si desea abrirlo ahora, pulse Abrir para revisarlo. Debe ver «Se exportó la ficha técnica…».");

            Subtitulo(col, "8.2 Desde Catálogo");
            Paso(col, "1", "Marque UNA sola casilla, o pulse Exportar en la columna derecha de esa fila (sin casillas).");
            Paso(col, "2", "Pulse «Exportar ficha (PDF/Excel)». Si marcó cero o más de una: «Marque una sola ficha para exportarla, o pulse Exportar en su fila.» Varias casillas = use consolidado (sección 9).");

            Titulo(col, "9. Consolidado de varias fichas (detalle completo)");
            Caja(col, "Qué es y para qué sirve",
                "Un solo archivo con todas las fichas que usted marcó en ESA pantalla. PDF: portada-resumen y después cada ficha completa. Excel: una tabla con las columnas del catálogo. Útil para auditorías, compras, entrega a otra sucursal o respaldo de un lote.");

            Subtitulo(col, "9.1 Preparar la lista");
            Paso(col, "1", "Decida dónde marcar: Catálogo (varias áreas a la vez) o un área (solo esa). Las marcas de una pantalla no se mezclan con las de otra.");
            Paso(col, "2", "Opcional: filtre. Ejemplo en Catálogo: Área = Clínica y busque «sutura». Ejemplo en Limpieza: busque «alcohol».");
            Paso(col, "3", "El filtro no sustituye al consolidado: el archivo llevará solo las que marque, no «todo lo filtrado».");

            Subtitulo(col, "9.2 Marcar (checkbox)");
            Paso(col, "1", "A la izquierda de cada renglón hay un recuadro. Púlselo hasta que quede marcado. En Catálogo el texto de resumen indica cuántas van seleccionadas.");
            Paso(col, "2", "Marque dos o más. Una sola marcada y «Exportar seleccionadas» también genera consolidado de una; para un solo material es más claro usar «Exportar ficha».");
            Paso(col, "3", "No pulse el nombre si solo quiere marcar: el nombre abre la ficha.");
            Paso(col, "4", "Para desmarcar, vuelva a pulsar el recuadro. Si cambia el filtro, revise qué sigue marcado antes de exportar.");

            Subtitulo(col, "9.3 Generar el archivo");
            Paso(col, "1", "Pulse «Exportar seleccionadas (PDF/Excel)» (arriba, en Catálogo o en el área).");
            Paso(col, "2", "Si no marcó ninguna: en área, «Marque al menos una ficha de la lista para exportar el consolidado.» En Catálogo: «Marque al menos una ficha de la lista para exportar.» Marque y reintente. No pulse Cancelar y asuma que se guardó: no se crea archivo.");
            Paso(col, "3", "En Guardar, elija carpeta (Documentos, escritorio, USB…). Nombre sugerido: consolidado-fichas-AAAAMMDD (el programa propone uno con la fecha).");
            Paso(col, "4", "Tipo de archivo:");
            Viñeta(col, "PDF — imprimir, firmar, enviar por correo. Incluye listado y el detalle de cada ficha. Puede ser largo si marcó muchas.");
            Viñeta(col, "Excel — filtrar, compartir con administración o reutilizar datos. No es el mismo archivo que la plantilla de importación, pero sirve para revisar columnas.");
            Paso(col, "5", "Pulse Guardar. Debe aparecer que se exportó el consolidado con el número de fichas.");
            Paso(col, "6", "Si ofrece abrir el archivo, ábralo y compruebe: cantidad de materiales, códigos y que no falte ninguno del lote.");
            Paso(col, "7", "Guarde el PDF/Excel donde lo pidan (auditoría, compras). El original sigue en el programa; el archivo es una copia de ese momento.");

            Subtitulo(col, "9.4 Ejemplos de trabajo");
            Viñeta(col, "Auditoría de quirófano: Clínica → filtre u ordene → marque 10–20 insumos → Exportar seleccionadas → PDF.");
            Viñeta(col, "Inventario mixto: Catálogo → Todas las áreas → marque clínica y limpieza → Excel.");
            Viñeta(col, "Solo desinfectantes: Limpieza → marque los de la lista → consolidado PDF.");
            Viñeta(col, "Un solo material: no use consolidado; use el icono de la fila o Exportar ficha.");
            Viñeta(col, "Lote de compras: marque los de existencia baja (ordene por existencia menor) → Excel para el proveedor.");

            Subtitulo(col, "9.5 Qué no hace el consolidado");
            Viñeta(col, "No cambia ni borra fichas del catálogo.");
            Viñeta(col, "No incluye fichas de otra pantalla aunque las haya marcado ahí antes.");
            Viñeta(col, "No sustituye el envase físico: confirme lote y caducidad en el producto.");

            Titulo(col, "10. Importar muchas fichas desde Excel");
            Paso(col, "1", "En Catálogo pulse Plantilla Excel, o en Configuración «Descargar plantilla Excel». Guarde plantilla-fichas-iderma.xlsx.");
            Paso(col, "2", "Abra el archivo. Hay membrete Iderma. Busque la fila de encabezados Área | Código | Nombre y el resto de campos. Hay una fila de ejemplo (p. ej. gasas).");
            Paso(col, "3", "No borre la fila de encabezados. Una fila = un material. Área debe ser exactamente Clínica, Oficina o Limpieza. Nombre es obligatorio. Si deja Código vacío, el sistema asigna el siguiente (CLI-, OFI-, LIM-).");
            Paso(col, "4", "Guarde el Excel y ciérrelo (Excel abierto a veces impide importar).");
            Paso(col, "5", "Importar desde un área (botón Importar Excel): esas filas se fuerzan a esa área aunque el Excel diga otra.");
            Paso(col, "6", "Importar desde Catálogo o Configuración: se respeta la columna Área. Filas sin área válida se omiten.");
            Paso(col, "7", "Elija el .xlsx. El programa suma nuevas, actualiza si el código ya existe en esa área, y omite filas sin nombre. Lea el recuento de nuevas / actualizadas / omitidas.");
            Paso(col, "8", "Mismo código en la misma área = se actualiza, no se duplica. Códigos distintos (CLI-001 vs CLI-1) sí crean dos fichas.");

            Titulo(col, "11. Vencimientos");
            Paso(col, "1", "Menú Vencimientos, o recuadros de Inicio.");
            Paso(col, "2", "Elija horizonte: próximos 30, 60 o 90 días.");
            Paso(col, "3", "Izquierda: vencidos (no usar en procedimiento). Derecha: próximos a vencer (reponer).");
            Paso(col, "4", "Pulse un renglón para abrir la ficha en su área y corregir lote o Fecha de caducidad (pestaña Área en Clínica) y Guardar.");
            Paso(col, "5", "Si no aparece un producto, suele faltar la fecha de caducidad en la ficha.");

            Titulo(col, "12. Configuración (engranaje)");
            Paso(col, "1", "Desplegables: pestaña Clínica/Oficina/Limpieza → elija la lista → Agregar, Guardar cambio, Borrar o Restaurar esta lista.");
            Paso(col, "2", "Anote la ruta del JSON de fichas y de las copias de seguridad (por si hay que restaurar).");
            Paso(col, "3", "Exportar JSON = respaldo completo. Importar JSON = sustituye/carga ese respaldo.");
            Paso(col, "4", "Restaurar ejemplos: borra el catálogo real y pone fichas de demostración. Solo con confirmación; no lo use en producción llena.");
            Paso(col, "5", "Borrar base de datos: vacía fichas y deja una copia interna con fecha y hora. Irreversible salvo esa copia.");

            col.Item().PageBreak();
            Titulo(col, "13. Posibles errores y soluciones");
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(1.8f);
                });
                EncabezadoTabla(t, "Qué ve o qué pasa", "Qué hacer");
                FilaTabla(t, "«El código y el nombre son obligatorios.»",
                    "Pestaña Identificación: complete ambos y pulse Guardar ficha otra vez.");
                FilaTabla(t, "«Marque al menos una ficha…» / consolidado vacío",
                    "Marque los checkbox de la lista (no solo el nombre). Luego Exportar seleccionadas.");
                FilaTabla(t, "«Marque una sola ficha para exportarla» (Catálogo)",
                    "Una ficha: una casilla o el botón Exportar de la fila. Varias casillas: use Exportar seleccionadas.");
                FilaTabla(t, "«Abra una ficha, márquela o pulse el botón de exportar en su fila.»",
                    "En el área no hay ficha abierta ni una sola marcada. Ábrala o use el icono de la fila.");
                FilaTabla(t, "Pulsé el nombre y pensé que iba al consolidado",
                    "El nombre abre el formulario. Para el lote use los recuadros y Exportar seleccionadas.");
                FilaTabla(t, "Al importar no aparecen filas",
                    "Falta Nombre, o Área no es Clínica/Oficina/Limpieza (si importó desde Catálogo). Use la plantilla; el programa busca la fila Área | Código | Nombre (no tiene que ser la fila 1 si hay membrete). Cierre Excel e intente de nuevo.");
                FilaTabla(t, "«No se pudo importar el Excel…»",
                    "Archivo dañado, no es .xlsx, o está abierto en otro programa. Guarde una copia de la plantilla oficial y reintente.");
                FilaTabla(t, "Se duplicó un producto",
                    "Códigos distintos (CLI-001 y CLI-1). Unifique el código, borre el duplicado o reimporte para actualizar el mismo código.");
                FilaTabla(t, "Guardé pero no la veo en otra área",
                    "Pertenece a un solo área. Búsquela en Catálogo o en el área donde la creó.");
                FilaTabla(t, "El consolidado PDF es muy largo / tarda",
                    "Marque menos fichas o exporte Excel. El PDF incluye la ficha completa de cada una.");
                FilaTabla(t, "Cancelé Guardar y no hay archivo",
                    "Es normal. Vuelva a exportar y elija carpeta y tipo (PDF o Excel).");
                FilaTabla(t, "No encuentro el archivo después",
                    "Revise la carpeta del cuadro Guardar (suele ser Documentos). Nombres típicos: ficha-CODIGO o consolidado-fichas- y la fecha.");
                FilaTabla(t, "Listas del formulario incompletas",
                    "Configuración → área → desplegable → escriba el valor → Agregar.");
                FilaTabla(t, "Caducidad no sale en Vencimientos",
                    "En Clínica, pestaña Área, Fecha de caducidad → Guardar. Sin fecha no entra al listado.");
                FilaTabla(t, "Existencia en 0 y no «desaparece»",
                    "Sigue en el catálogo. Ordene por existencia menor o busque el código. Actualice existencia y Guardar.");
                FilaTabla(t, "Descartar cambios no hizo nada útil",
                    "Solo restaura si había una ficha ya guardada seleccionada. En ficha nueva vacía no hay versión anterior.");
                FilaTabla(t, "Restauré ejemplos y perdí datos",
                    "Esa acción sustituye el catálogo. Recupere con Importar JSON o la copia de la carpeta de respaldos (ruta en Configuración).");
                FilaTabla(t, "Borré la base por error",
                    "Configuración indica la copia con fecha y hora del borrado. Restaure desde esa copia o un JSON previo. No hay deshacer en pantalla.");
                FilaTabla(t, "El menú izquierdo no se ve",
                    "Botón de panel en la barra de título. Amplíe la ventana si está muy estrecha.");
                FilaTabla(t, "El programa no abre o está en blanco",
                    "Cierre y vuelva a abrir. Si persiste, el JSON de datos puede estar dañado: restaure un respaldo (pida apoyo a quien administre la PC).");
            });

            Titulo(col, "14. Lista rápida (de inicio a fin)");
            Viñeta(col, "Abrir programa → Inicio.");
            Viñeta(col, "Elegir área (o Catálogo para ver todo).");
            Viñeta(col, "Nueva ficha → pestañas → Guardar (código + nombre).");
            Viñeta(col, "Una ficha: icono de fila o Exportar ficha → PDF o Excel.");
            Viñeta(col, "Varias: checkbox → Exportar seleccionadas → consolidado PDF o Excel → revisar el archivo.");
            Viñeta(col, "Lote inicial: Plantilla Excel → llenar → cerrar Excel → Importar Excel.");
            Viñeta(col, "Caducados: Vencimientos o tarjetas de Inicio → abrir ficha → corregir fecha → Guardar.");
            Viñeta(col, "Respaldo: Configuración → Exportar JSON.");

            col.Item().PaddingTop(8).Background(Fondo).Padding(10).Text(t =>
            {
                t.Span("Uso interno. ").Bold();
                t.Span("La ficha no sustituye la etiqueta del fabricante ni el registro sanitario. " +
                    "Antes de un procedimiento, confirme lote y caducidad en el envase físico.");
            });
        });
    });
})
.WithMetadata(new DocumentMetadata
{
    Title = "Manual · Fichas técnicas · Iderma Capilar",
    Author = "Iderma Capilar",
    Subject = "Catálogo de materiales — paso a paso y consolidados",
    Creator = "Sistema de fichas técnicas de materiales"
})
.GeneratePdf(salida);

Console.WriteLine(salida);

static void Titulo(ColumnDescriptor col, string texto) =>
    col.Item().PaddingTop(6).Text(texto).FontSize(12.5f).Bold().FontColor("#1A305A");

static void Subtitulo(ColumnDescriptor col, string texto) =>
    col.Item().Text(texto).FontSize(11).Bold().FontColor("#C5A059");

static void Parrafo(ColumnDescriptor col, string texto) =>
    col.Item().Text(texto).FontSize(10).FontColor("#1A305A");

static void Viñeta(ColumnDescriptor col, string texto) =>
    col.Item().Row(r =>
    {
        r.ConstantItem(12).Text("•").FontColor("#C5A059");
        r.RelativeItem().Text(texto);
    });

static void Paso(ColumnDescriptor col, string n, string texto) =>
    col.Item().Row(r =>
    {
        r.ConstantItem(22).Text(n + ".").Bold().FontColor("#C5A059");
        r.RelativeItem().Text(texto);
    });

static void Caja(ColumnDescriptor col, string titulo, string cuerpo) =>
    col.Item().Border(1).BorderColor("#D8D2C6").Background("#F4EFE4").Padding(10).Column(c =>
    {
        c.Item().Text(titulo).Bold().FontSize(10);
        c.Item().PaddingTop(4).Text(cuerpo).FontSize(9.5f);
    });

static void EncabezadoTabla(TableDescriptor t, string a, string b)
{
    t.Header(h =>
    {
        h.Cell().Background("#1A305A").Padding(5).Text(a).FontColor(Colors.White).Bold().FontSize(8.5f);
        h.Cell().Background("#1A305A").Padding(5).Text(b).FontColor(Colors.White).Bold().FontSize(8.5f);
    });
}

static void FilaTabla(TableDescriptor t, string a, string b)
{
    t.Cell().BorderBottom(0.5f).BorderColor("#D8D2C6").Padding(5).Text(a).FontSize(8.5f);
    t.Cell().BorderBottom(0.5f).BorderColor("#D8D2C6").Padding(5).Text(b).FontSize(8.5f);
}
