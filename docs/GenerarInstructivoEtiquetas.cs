using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

const string Navy = "#1A305A";
const string Oro = "#C5A059";
const string Fondo = "#F4EFE4";
const string Gris = "#5A6570";
const string Linea = "#D8D2C6";

var salida = Path.Combine(
    @"c:\Users\Iderma Capilar 1\Documents\ficha2",
    "docs",
    "Instructivo-Generador-de-etiquetas-Iderma.pdf");
Directory.CreateDirectory(Path.GetDirectoryName(salida)!);

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
                    t.Item().Text("Instructivo interno · Área logística · Generador de etiquetas")
                        .FontSize(9).FontColor(Gris);
                });
            });
            col.Item().PaddingTop(8).Height(3).Background(Oro);
        });

        pagina.Footer().Row(fila =>
        {
            fila.RelativeItem().Text("Documento de uso interno · Iderma Capilar · No sustituye la etiqueta del fabricante")
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
            col.Spacing(10);

            col.Item().Text("INSTRUCTIVO DE USO").FontSize(11).Bold().FontColor(Oro);
            col.Item().Text("Generador de etiquetas").FontSize(18).Bold();
            col.Item().Text($"Versión 1.0  ·  {DateTime.Now:dd/MM/yyyy}  ·  Sistema de fichas técnicas de materiales")
                .FontSize(9).FontColor(Gris);

            Caja(col, "Para quién es este documento",
                "Este instructivo explica, paso a paso, cómo generar etiquetas de productos (código + nombre) desde un archivo de Excel, " +
                "cómo interpretar la cantidad de copias, cómo elegir el tipo de código y el tamaño de hoja, y qué hacer si aparece un error. " +
                "Está pensado para personal de logística, almacén e inventario de Iderma Capilar.");

            Titulo(col, "1. Qué hace esta sección");
            Parrafo(col, "El Generador de etiquetas convierte una lista de productos en un PDF listo para imprimir. Cada fila del Excel " +
                "es un producto. La columna CANTIDAD no es el stock del almacén: es cuántas etiquetas iguales se van a imprimir de ese código. " +
                "Si CANTIDAD es 8, salen 8 etiquetas idénticas. Si es 0, ese producto no se imprime.");
            Parrafo(col, "Cada etiqueta muestra: el símbolo (QR, código de barras u otro), el código del producto y el nombre. " +
                "A la derecha de la pantalla hay una vista previa que se actualiza al cambiar el tipo de código o el tamaño.");

            Titulo(col, "2. Cómo entrar");
            Paso(col, "1", "Abra Iderma Capilar · Fichas técnicas.");
            Paso(col, "2", "En el menú izquierdo, busque el encabezado Área logística.");
            Paso(col, "3", "Pulse Generador de etiquetas.");
            Paso(col, "4", "Debe ver tres bloques: Archivo de productos (izquierda), Tipo de etiqueta y cuadrícula (izquierda, abajo), Vista previa (derecha) y, más abajo, Productos del Excel.");

            Titulo(col, "3. Preparar el Excel (obligatorio)");
            Parrafo(col, "El programa no acepta cualquier hoja. La primera fila debe ser exactamente estos cuatro encabezados, en este orden, en las columnas A a D:");

            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(90);
                    c.RelativeColumn();
                    c.RelativeColumn();
                });
                EncabezadoTabla(t, "Columna", "Encabezado", "Qué escribir");
                FilaTabla(t, "A", "CÓDIGO", "Identificador único. Ej.: CDB_002. No puede ir vacío.");
                FilaTabla(t, "B", "ÁREA", "Área o familia. Ej.: CDB. Puede ir vacío, pero se recomienda llenarlo.");
                FilaTabla(t, "C", "PRODUCTO", "Nombre completo. Puede ser largo; en la etiqueta se parte en varias líneas.");
                FilaTabla(t, "D", "CANTIDAD", "Número entero ≥ 0. Es el número de etiquetas a imprimir de esa fila. 0 = no imprimir.");
            });

            Parrafo(col, "Mayúsculas, minúsculas y acentos en los encabezados no importan (CÓDIGO y Codigo se aceptan). Los espacios de más se ignoran. " +
                "Lo que sí importa es el orden: no ponga PRODUCTO en la columna A.");

            Subtitulo(col, "3.1 Descargar la plantilla oficial (recomendado)");
            Paso(col, "1", "En Archivo de productos pulse Descargar plantilla.");
            Paso(col, "2", "Guarde el archivo (nombre sugerido: plantilla-etiquetas-iderma.xlsx).");
            Paso(col, "3", "Si el programa lo ofrece, ábralo. Verá tres filas de ejemplo:");
            Viñeta(col, "CDB_002 · MONALISA (HARD TYPE) FUCSIA · 8 → se imprimen 8 etiquetas.");
            Viñeta(col, "CDB_004 · MONALISA (SOFT TYPE) VERDE · 0 → no se imprime (sirve para dejar el producto en la lista sin gastar etiquetas).");
            Viñeta(col, "CDB_007 · BIODERMA CICABIO CREME 40 ML · 7 → se imprimen 7 etiquetas.");
            Paso(col, "4", "Borre o sustituya los ejemplos por sus productos reales. No borre la fila 1 de encabezados.");
            Paso(col, "5", "Guarde el Excel. Puede usar .xlsx de Microsoft Excel o compatible.");

            Subtitulo(col, "3.2 Reglas de cada fila");
            Viñeta(col, "Si CÓDIGO y PRODUCTO van vacíos, esa fila se salta (no da error).");
            Viñeta(col, "Si hay nombre pero no hay código, el archivo se rechaza: «La fila N no tiene CÓDIGO».");
            Viñeta(col, "CANTIDAD debe ser un entero: 0, 1, 2, 8… No use 1,5 ni texto («ocho»).");
            Viñeta(col, "No ponga hojas de cálculo extra delante: el programa lee la primera hoja del libro.");
            Viñeta(col, "No fusione celdas de encabezado ni inserte una fila de título encima de CÓDIGO | ÁREA | PRODUCTO | CANTIDAD.");

            Titulo(col, "4. Adjuntar el Excel a la aplicación");
            Paso(col, "1", "Pulse Adjuntar Excel.");
            Paso(col, "2", "Elija el archivo. Espere a que se valide.");
            Paso(col, "3", "Si todo está bien, verá un aviso verde: «El archivo cumple la estructura y ya puede generar las etiquetas».");
            Paso(col, "4", "Debajo verá cuántos productos hay y cuántas etiquetas se imprimirán (la suma de CANTIDAD).");
            Paso(col, "5", "En Productos del Excel revise la tabla: Código, Área, Producto y Se imprimen. Confirme que las cantidades coinciden con lo que necesita pegar o entregar.");
            Paso(col, "6", "Si se equivocó de archivo, adjunte otro. El anterior se reemplaza.");

            Titulo(col, "5. Elegir el tipo de código y la hoja");
            Parrafo(col, "Estos controles están en Tipo de etiqueta y cuadrícula. La vista previa de la derecha cambia al instante.");

            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(2);
                });
                EncabezadoTabla(t, "Opción", "Cuándo usarla");
                FilaTabla(t, "Código QR (predeterminado)", "Códigos alfanuméricos internos (CDB_002, etc.). Es el más flexible.");
                FilaTabla(t, "Código de barras (Code 128)", "Cuando el lector de almacén espera barras tradicionales y el código puede tener letras y números.");
                FilaTabla(t, "Code 39", "Lectores antiguos. Evite caracteres raros; use letras, números y pocos símbolos.");
                FilaTabla(t, "EAN-13", "Solo para códigos comerciales de 12 o 13 dígitos (sin letras). Si el código no cumple, la vista previa usa un ejemplo numérico y, al generar, puede caer a Code 128.");
                FilaTabla(t, "Data Matrix / PDF417 / Aztec", "Cuando un equipo o proveedor pide ese formato concreto.");
            });

            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(2);
                });
                EncabezadoTabla(t, "Tamaño de hoja", "Uso");
                FilaTabla(t, "A4 (predeterminada)", "La mayoría de impresoras de oficina en México.");
                FilaTabla(t, "Carta", "Hojas letter (216 × 279 mm).");
                FilaTabla(t, "Oficio / Legal", "Hojas más largas.");
                FilaTabla(t, "A5", "Mitad de A4; etiquetas más grandes si usa pocas por hoja.");
            });

            Titulo(col, "6. Cuadrícula y tamaño del símbolo");
            Paso(col, "1", "Etiquetas por fila (columnas): de 1 a 8. Valor inicial: 3.");
            Paso(col, "2", "Etiquetas por columna (filas): de 1 a 16. Valor inicial: 8.");
            Paso(col, "3", "Con 3 × 8 salen 24 etiquetas por hoja. El resumen indica cuántas páginas ocupará su tirada.");
            Paso(col, "4", "Tamaño del código: control deslizante de 10 mm a 70 mm (inicial 20 mm). Si el símbolo no cabe en la celda, el programa lo reduce solo.");
            Paso(col, "5", "Más columnas y filas = etiquetas más chicas. Si el nombre del producto es muy largo, use menos columnas (por ejemplo 2 × 6) para que se lea.");
            Paso(col, "6", "Lea el texto de resumen bajo los controles: tipo de código, cuadrícula, milímetros y total de etiquetas/páginas. Las filas con cantidad 0 se mencionan como omitidas.");

            Titulo(col, "7. Prueba de impresión (calibrar la impresora)");
            Parrafo(col, "Hágalo la primera vez que use una impresora nueva o si las etiquetas salen cortadas o borrosas.");
            Paso(col, "1", "Pulse Prueba de impresión (no hace falta tener Excel adjunto).");
            Paso(col, "2", "Guarde el PDF (nombre sugerido: prueba-impresion-calibracion-iderma).");
            Paso(col, "3", "Ábralo e imprímalo al 100 %. No use «Ajustar a la página» ni «Encoger».");
            Paso(col, "4", "Mida las reglas de 20, 50, 70 y 100 mm. Si no coinciden, en la impresora desactive cualquier escala y vuelva a imprimir.");
            Paso(col, "5", "Revise que el QR de la prueba se lea con el celular o el lector.");

            Titulo(col, "8. Generar el PDF de etiquetas");
            Paso(col, "1", "Confirme que el Excel ya está adjunto y que el total de etiquetas es correcto.");
            Paso(col, "2", "Pulse Generar etiquetas (PDF).");
            Paso(col, "3", "Si no hay archivo, verá: «Adjunte primero un Excel con la estructura obligatoria».");
            Paso(col, "4", "Elija dónde guardar (nombre sugerido: etiquetas-iderma.pdf). Si cancela, no se genera nada.");
            Paso(col, "5", "Aparece un diálogo con barra de progreso: preparación, generación de códigos, composición del PDF y guardado. Espere; no cierre la aplicación.");
            Paso(col, "6", "Al terminar verá un aviso de éxito con el total de etiquetas. Si se ofrece Abrir, ábralo y revise una página completa antes de mandar a imprimir todo el lote.");
            Paso(col, "7", "Imprima al 100 %, a color o blanco y negro según el lector. El pie del PDF indica Iderma Capilar · Generador de etiquetas y el número de página.");

            Titulo(col, "9. Cómo se rellenan las hojas");
            Viñeta(col, "Las etiquetas se colocan de izquierda a derecha y de arriba abajo.");
            Viñeta(col, "Un mismo código se repite tantas veces como CANTIDAD.");
            Viñeta(col, "Si el último producto no llena la hoja, el resto de celdas quedan vacías.");
            Viñeta(col, "Cambiar columnas/filas no cambia cuántas etiquetas hay; solo cómo se acomodan en el papel.");

            Titulo(col, "10. Recomendaciones de trabajo diario");
            Viñeta(col, "Mantenga una plantilla maestra y duplíquela por lote (fecha o pedido), no reutilice un Excel a medias.");
            Viñeta(col, "Ponga en CANTIDAD las piezas físicas a etiquetar ese día, no el inventario histórico.");
            Viñeta(col, "Para un producto que no se va a pegar hoy, deje CANTIDAD en 0 en lugar de borrar la fila.");
            Viñeta(col, "Si usa EAN-13, verifique que el código tenga 12 o 13 dígitos antes de adjuntar.");
            Viñeta(col, "Nombres muy largos: acórtelos en el Excel si en la vista previa no se leen.");

            col.Item().PageBreak();
            Titulo(col, "11. Posibles errores y soluciones");
            Parrafo(col, "Los mensajes aparecen en la barra de color de la parte superior de la pantalla. Copie el texto si pide ayuda a sistemas.");

            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.15f);
                    c.RelativeColumn(1.85f);
                });
                EncabezadoTabla(t, "Qué ve o qué pasa", "Qué hacer");
                FilaTabla(t, "«El Excel no sigue la estructura obligatoria. La primera fila debe ser exactamente: CÓDIGO | ÁREA | PRODUCTO | CANTIDAD.»",
                    "Descargue la plantilla. No ponga un título en la fila 1. No cambie el orden de columnas. Quite filas vacías insertadas arriba. Guarde como .xlsx y adjunte de nuevo.");
                FilaTabla(t, "«El archivo no tiene hojas.»",
                    "El libro está dañado o vacío. Ábralo en Excel, confirme que hay al menos una hoja con datos y vuelva a guardar.");
                FilaTabla(t, "«La fila N no tiene CÓDIGO.»",
                    "Esa fila tiene producto (o cantidad) pero la columna A está vacía. Escriba el código o borre toda la fila.");
                FilaTabla(t, "«La fila N (CÓDIGO) no tiene CANTIDAD válida.»",
                    "Ponga un entero 0 o mayor. Elimine letras, espacios raros o decimales. Si Excel muestra fecha, cambie el formato de la columna D a Número.");
                FilaTabla(t, "«El archivo no contiene productos debajo de los encabezados.»",
                    "Solo está la fila 1. Agregue al menos un producto con código.");
                FilaTabla(t, "«Adjunte primero un Excel con la estructura obligatoria.»",
                    "Pulsó Generar sin adjuntar, o el último adjunto falló y se limpió la lista. Adjuntar Excel otra vez.");
                FilaTabla(t, "«No hay etiquetas que generar. Todas las filas tienen cantidad 0.»",
                    "El archivo es válido pero nada se imprime. Ponga CANTIDAD ≥ 1 en los productos que sí necesita.");
                FilaTabla(t, "Aviso rojo al adjuntar y la lista queda vacía",
                    "Lea el mensaje completo. Corrija el Excel (no «a ojo» en la app: hay que editar el archivo y adjuntarlo de nuevo).");
                FilaTabla(t, "La vista previa no muestra mi producto",
                    "La vista previa usa el primer producto con CANTIDAD > 0. Si todos están en 0, muestra el ejemplo MONALISA. Suba una cantidad y vuelva a adjuntar o revise la lista inferior.");
                FilaTabla(t, "Elegí EAN-13 y el código no es numérico",
                    "EAN-13 exige 12 o 13 dígitos. Use QR o Code 128 para códigos tipo CDB_002. Si EAN-13 falla al dibujar, el sistema puede mostrar barras Code 128.");
                FilaTabla(t, "El PDF se genera pero al imprimir se corta o sale chico",
                    "Imprima al 100 %, márgenes normales, sin «ajustar a la página». Haga antes Prueba de impresión y mida las reglas.");
                FilaTabla(t, "El lector no lee el código",
                    "Suba el tamaño (mm), use menos etiquetas por hoja, imprima más oscuro, pruebe QR o Code 128. Evite papel arrugado o plastificado brillante.");
                FilaTabla(t, "El nombre se recorta en la etiqueta",
                    "Reduzca columnas/filas o acorte el PRODUCTO en el Excel. El programa parte el texto, pero celdas muy pequeñas tienen límite.");
                FilaTabla(t, "La generación se queda mucho tiempo en «Generando códigos»",
                    "Es normal con cientos de códigos distintos. No cierre. Si hay miles de filas, divida el Excel en varios lotes.");
                FilaTabla(t, "Cancelé el cuadro de guardar y no pasó nada",
                    "Es correcto: no se crea archivo. Vuelva a pulsar Generar y elija carpeta.");
                FilaTabla(t, "No puedo pegar el Excel porque está abierto en Excel",
                    "Algunos archivos bloqueados fallan. Cierre el libro en Excel o guarde una copia y adjunte la copia.");
                FilaTabla(t, "El programa no abre / no veo Área logística",
                    "Actualice o reinicie Iderma Capilar. El ítem está en el menú, debajo de Recursos humanos, con el nombre Generador de etiquetas.");
                FilaTabla(t, "Mensaje genérico «No se pudo generar…»",
                    "Revise disco lleno, carpeta sin permiso o antivirus bloqueando el PDF. Pruebe guardar en Documentos. Si persiste, anote la hora y el mensaje completo para sistemas.");
            });

            Titulo(col, "12. Lista rápida de comprobación");
            Viñeta(col, "Plantilla con encabezados CÓDIGO · ÁREA · PRODUCTO · CANTIDAD.");
            Viñeta(col, "Cada producto tiene código; CANTIDAD es un entero; 0 = no imprimir.");
            Viñeta(col, "Adjuntar Excel → aviso verde → revisar tabla y total.");
            Viñeta(col, "Tipo de código, hoja y cuadrícula; mirar vista previa.");
            Viñeta(col, "Primera vez: prueba de impresión al 100 %.");
            Viñeta(col, "Generar etiquetas (PDF) → abrir → imprimir sin escalar.");

            col.Item().PaddingTop(8).Background(Fondo).Padding(10).Text(t =>
            {
                t.Span("Soporte interno. ").Bold();
                t.Span("Este instructivo describe el comportamiento del Generador de etiquetas de Iderma Capilar. " +
                    "No sustituye procedimientos de farmacia ni la etiqueta original del fabricante. " +
                    "Si un lote de etiquetas no coincide con el Excel, no imprima: corrija cantidades y genere de nuevo.");
            });
        });
    });
})
.WithMetadata(new DocumentMetadata
{
    Title = "Instructivo · Generador de etiquetas · Iderma Capilar",
    Author = "Iderma Capilar",
    Subject = "Área logística",
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

static void EncabezadoTabla(TableDescriptor t, string a, string b, string? c = null)
{
    t.Header(h =>
    {
        h.Cell().Background("#1A305A").Padding(5).Text(a).FontColor(Colors.White).Bold().FontSize(8.5f);
        h.Cell().Background("#1A305A").Padding(5).Text(b).FontColor(Colors.White).Bold().FontSize(8.5f);
        if (c is not null)
        {
            h.Cell().Background("#1A305A").Padding(5).Text(c).FontColor(Colors.White).Bold().FontSize(8.5f);
        }
    });
}

static void FilaTabla(TableDescriptor t, string a, string b, string? c = null)
{
    t.Cell().BorderBottom(0.5f).BorderColor("#D8D2C6").Padding(5).Text(a).FontSize(8.5f);
    t.Cell().BorderBottom(0.5f).BorderColor("#D8D2C6").Padding(5).Text(b).FontSize(8.5f);
    if (c is not null)
    {
        t.Cell().BorderBottom(0.5f).BorderColor("#D8D2C6").Padding(5).Text(c).FontSize(8.5f);
    }
}
