using System.Globalization;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class InventarioDocumento
{
    public const string Coleccion = "inventario";

    public static readonly string[] ColumnasPartida =
    [
        "Área", "Código", "Nombre", "Lote", "Caducidad", "Cantidad", "Estado"
    ];

    public static Dictionary<string, object> Crear(FichaTecnica ficha)
    {
        var origen = ficha.Clonar();
        origen.PrepararContactosProveedor();
        var contactos = Contactos(origen);
        object[] valores =
        [
            origen.AreaTitulo,
            origen.Codigo,
            origen.Nombre,
            origen.Categoria,
            origen.Descripcion,
            origen.EstadoFisico,
            origen.UnidadMedida,
            origen.Marca,
            origen.Modelo,
            origen.NormasCertificaciones,
            origen.VidaUtil,
            origen.Existencia,
            origen.StockMinimo,
            origen.Fabricante,
            origen.PaisOrigen,
            origen.PlantaOrigen,
            origen.TipoOrigen,
            origen.Proveedor,
            contactos.Canal1,
            contactos.Dato1,
            contactos.Canal2,
            contactos.Dato2,
            origen.Lote,
            Fecha(origen.FechaAdquisicion),
            origen.NumeroFactura,
            origen.RegistroSanitario,
            SiNo(origen.Esteril),
            SiNo(origen.RequiereRefrigeracion),
            Fecha(origen.FechaCaducidad),
            SiNo(origen.Reutilizable),
            origen.Ubicacion,
            origen.NumeroSerie,
            origen.GarantiaMeses,
            SiNo(origen.RequiereMantenimiento),
            origen.PeriodicidadMantenimiento,
            origen.Voltaje,
            origen.Compatibilidad,
            origen.Responsable,
            origen.PrincipioActivo,
            origen.Concentracion,
            origen.Ph,
            origen.SuperficiesUso,
            origen.TiempoContacto,
            origen.Dilucion,
            origen.Peligrosidad,
            origen.HojaSeguridad,
            origen.EppRequerido,
            origen.MaterialesCompatibles,
            origen.ResiduoGenerado,
            origen.Observaciones
        ];

        var documento = new Dictionary<string, object>(ExcelFichaService.Encabezados.Length + 2);
        for (var i = 0; i < ExcelFichaService.Encabezados.Length; i++)
        {
            documento[ExcelFichaService.Encabezados[i]] = valores[i] ?? string.Empty;
        }

        documento["Partidas"] = origen.Partidas.Select(partida => (object)new Dictionary<string, object>
        {
            ["Id"] = partida.Id.ToString("D"),
            ["Área"] = origen.AreaTitulo,
            ["Código"] = origen.Codigo,
            ["Nombre"] = origen.Nombre,
            ["Lote"] = partida.Lote,
            ["Caducidad"] = Fecha(partida.FechaCaducidad),
            ["Cantidad"] = partida.Cantidad,
            ["Estado"] = partida.EstadoCaducidadTexto
        }).ToList();
        documento["Meta"] = new Dictionary<string, object>
        {
            ["Id área"] = origen.Area?.ToString("D") ?? string.Empty,
            ["Fecha creación"] = FechaHora(origen.FechaCreacion),
            ["Fecha actualización"] = FechaHora(origen.FechaActualizacion)
        };
        return documento;
    }

    public static FichaTecnica Leer(string idDocumento, IDictionary<string, object> datos)
    {
        var meta = Mapa(datos.TryGetValue("Meta", out var metaRaw) ? metaRaw : null);
        var areaTexto = Texto(datos, "Área");
        var areaId = Texto(meta, "Id área");
        var ficha = new FichaTecnica
        {
            Id = Guid.TryParse(idDocumento, out var id) ? id : Guid.NewGuid(),
            Area = Guid.TryParse(areaId, out var area) ? area : AreasOperativas.ResolverId(areaTexto),
            Codigo = Texto(datos, "Código"),
            Nombre = Texto(datos, "Nombre"),
            Categoria = Texto(datos, "Categoría"),
            Descripcion = Texto(datos, "Descripción"),
            EstadoFisico = Texto(datos, "Estado físico"),
            Color = Texto(datos, "Color"),
            Dimensiones = Texto(datos, "Dimensiones"),
            PesoVolumen = Texto(datos, "Peso / volumen"),
            MaterialFabricacion = Texto(datos, "Material"),
            UnidadMedida = Texto(datos, "Unidad"),
            Marca = Texto(datos, "Marca"),
            Modelo = Texto(datos, "Modelo"),
            NormasCertificaciones = Texto(datos, "Normas"),
            VidaUtil = Texto(datos, "Vida útil"),
            Existencia = Numero(datos, "Existencia"),
            StockMinimo = Numero(datos, "Stock mínimo"),
            Fabricante = Texto(datos, "Fabricante"),
            PaisOrigen = Texto(datos, "País"),
            PlantaOrigen = Texto(datos, "Planta"),
            TipoOrigen = Texto(datos, "Tipo origen"),
            Proveedor = Texto(datos, "Proveedor"),
            ContactoProveedor = Texto(datos, "Contacto"),
            ContactoCanal1 = Canal(Texto(datos, "Canal contacto 1")),
            ContactoDato1 = Texto(datos, "Dato contacto 1"),
            ContactoCanal2 = Canal(Texto(datos, "Canal contacto 2")),
            ContactoDato2 = Texto(datos, "Dato contacto 2"),
            Lote = Texto(datos, "Lote"),
            FechaAdquisicion = ParseFecha(Texto(datos, "Fecha adquisición")),
            NumeroFactura = Texto(datos, "Factura"),
            RegistroSanitario = Texto(datos, "Registro sanitario"),
            Esteril = EsSi(Texto(datos, "Estéril")),
            RequiereRefrigeracion = EsSi(Texto(datos, "Refrigeración")),
            FechaCaducidad = ParseFecha(Texto(datos, "Fecha caducidad")),
            Reutilizable = EsSi(Texto(datos, "Reutilizable")),
            Ubicacion = Texto(datos, "Ubicación"),
            NumeroSerie = Texto(datos, "Número de serie"),
            GarantiaMeses = Numero(datos, "Garantía meses"),
            RequiereMantenimiento = EsSi(Texto(datos, "Mantenimiento")),
            PeriodicidadMantenimiento = Texto(datos, "Periodicidad"),
            Voltaje = Texto(datos, "Voltaje"),
            Compatibilidad = Texto(datos, "Compatibilidad"),
            Responsable = Texto(datos, "Responsable"),
            PrincipioActivo = Texto(datos, "Principio activo"),
            Concentracion = Texto(datos, "Concentración"),
            Ph = Texto(datos, "pH"),
            SuperficiesUso = Texto(datos, "Superficies"),
            TiempoContacto = Texto(datos, "Tiempo contacto"),
            Dilucion = Texto(datos, "Dilución"),
            Peligrosidad = Texto(datos, "Peligrosidad"),
            HojaSeguridad = Texto(datos, "SDS"),
            EppRequerido = Texto(datos, "EPP"),
            MaterialesCompatibles = Texto(datos, "Materiales compatibles"),
            ResiduoGenerado = Texto(datos, "Residuo"),
            Observaciones = Texto(datos, "Observaciones"),
            Caracteristicas = AlmacenCaracteristicas.ParsearLinea(Texto(datos, "Características extra")),
            Partidas = LeerPartidas(datos)
        };

        var creacion = ParseFecha(Texto(meta, "Fecha creación"));
        var actualizacion = ParseFecha(Texto(meta, "Fecha actualización"));
        if (creacion.HasValue)
        {
            ficha.FechaCreacion = creacion.Value;
        }

        if (actualizacion.HasValue)
        {
            ficha.FechaActualizacion = actualizacion.Value;
        }

        if (!string.IsNullOrWhiteSpace(ficha.ContactoDato1) || !string.IsNullOrWhiteSpace(ficha.ContactoDato2))
        {
            if (string.IsNullOrWhiteSpace(ficha.ContactoCanal1))
            {
                ficha.ContactoCanal1 = CanalDatoProveedor.WhatsApp;
            }

            if (string.IsNullOrWhiteSpace(ficha.ContactoCanal2))
            {
                ficha.ContactoCanal2 = CanalDatoProveedor.Web;
            }

            ficha.ContactosProveedorDefinidos = true;
            ficha.SincronizarCamposProveedorLegados();
        }

        PartidasInventario.Normalizar(ficha);
        return ficha;
    }

    public static void CopiarCampos(FichaTecnica destino, FichaTecnica origen, IEnumerable<string> campos)
    {
        var presentes = campos as IReadOnlySet<string> ?? new HashSet<string>(campos, StringComparer.Ordinal);
        void Si(string campo, Action copiar)
        {
            if (presentes.Contains(campo))
            {
                copiar();
            }
        }

        if (presentes.Contains("Área") || presentes.Contains("Meta"))
        {
            destino.Area = origen.Area;
        }

        if (presentes.Contains("Meta"))
        {
            destino.FechaCreacion = origen.FechaCreacion;
            destino.FechaActualizacion = origen.FechaActualizacion;
        }

        Si("Código", () => destino.Codigo = origen.Codigo);
        Si("Nombre", () => destino.Nombre = origen.Nombre);
        Si("Categoría", () => destino.Categoria = origen.Categoria);
        Si("Descripción", () => destino.Descripcion = origen.Descripcion);
        Si("Estado físico", () => destino.EstadoFisico = origen.EstadoFisico);
        Si("Color", () => destino.Color = origen.Color);
        Si("Dimensiones", () => destino.Dimensiones = origen.Dimensiones);
        Si("Peso / volumen", () => destino.PesoVolumen = origen.PesoVolumen);
        Si("Material", () => destino.MaterialFabricacion = origen.MaterialFabricacion);
        Si("Unidad", () => destino.UnidadMedida = origen.UnidadMedida);
        Si("Marca", () => destino.Marca = origen.Marca);
        Si("Modelo", () => destino.Modelo = origen.Modelo);
        Si("Normas", () => destino.NormasCertificaciones = origen.NormasCertificaciones);
        Si("Vida útil", () => destino.VidaUtil = origen.VidaUtil);
        Si("Existencia", () => destino.Existencia = origen.Existencia);
        Si("Stock mínimo", () => destino.StockMinimo = origen.StockMinimo);
        Si("Fabricante", () => destino.Fabricante = origen.Fabricante);
        Si("País", () => destino.PaisOrigen = origen.PaisOrigen);
        Si("Planta", () => destino.PlantaOrigen = origen.PlantaOrigen);
        Si("Tipo origen", () => destino.TipoOrigen = origen.TipoOrigen);
        Si("Proveedor", () => destino.Proveedor = origen.Proveedor);
        Si("Contacto", () => destino.ContactoProveedor = origen.ContactoProveedor);
        Si("Canal contacto 1", () => destino.ContactoCanal1 = origen.ContactoCanal1);
        Si("Dato contacto 1", () => destino.ContactoDato1 = origen.ContactoDato1);
        Si("Canal contacto 2", () => destino.ContactoCanal2 = origen.ContactoCanal2);
        Si("Dato contacto 2", () => destino.ContactoDato2 = origen.ContactoDato2);
        Si("Lote", () => destino.Lote = origen.Lote);
        Si("Fecha adquisición", () => destino.FechaAdquisicion = origen.FechaAdquisicion);
        Si("Factura", () => destino.NumeroFactura = origen.NumeroFactura);
        Si("Registro sanitario", () => destino.RegistroSanitario = origen.RegistroSanitario);
        Si("Estéril", () => destino.Esteril = origen.Esteril);
        Si("Refrigeración", () => destino.RequiereRefrigeracion = origen.RequiereRefrigeracion);
        Si("Fecha caducidad", () => destino.FechaCaducidad = origen.FechaCaducidad);
        Si("Reutilizable", () => destino.Reutilizable = origen.Reutilizable);
        Si("Ubicación", () => destino.Ubicacion = origen.Ubicacion);
        Si("Número de serie", () => destino.NumeroSerie = origen.NumeroSerie);
        Si("Garantía meses", () => destino.GarantiaMeses = origen.GarantiaMeses);
        Si("Mantenimiento", () => destino.RequiereMantenimiento = origen.RequiereMantenimiento);
        Si("Periodicidad", () => destino.PeriodicidadMantenimiento = origen.PeriodicidadMantenimiento);
        Si("Voltaje", () => destino.Voltaje = origen.Voltaje);
        Si("Compatibilidad", () => destino.Compatibilidad = origen.Compatibilidad);
        Si("Responsable", () => destino.Responsable = origen.Responsable);
        Si("Principio activo", () => destino.PrincipioActivo = origen.PrincipioActivo);
        Si("Concentración", () => destino.Concentracion = origen.Concentracion);
        Si("pH", () => destino.Ph = origen.Ph);
        Si("Superficies", () => destino.SuperficiesUso = origen.SuperficiesUso);
        Si("Tiempo contacto", () => destino.TiempoContacto = origen.TiempoContacto);
        Si("Dilución", () => destino.Dilucion = origen.Dilucion);
        Si("Peligrosidad", () => destino.Peligrosidad = origen.Peligrosidad);
        Si("SDS", () => destino.HojaSeguridad = origen.HojaSeguridad);
        Si("EPP", () => destino.EppRequerido = origen.EppRequerido);
        Si("Materiales compatibles", () => destino.MaterialesCompatibles = origen.MaterialesCompatibles);
        Si("Residuo", () => destino.ResiduoGenerado = origen.ResiduoGenerado);
        Si("Observaciones", () => destino.Observaciones = origen.Observaciones);
        Si("Características extra", () => destino.Caracteristicas = origen.Caracteristicas.Select(c => c.Clonar()).ToList());
        Si("Partidas", () => destino.Partidas = origen.Partidas.Select(p => p.Clonar()).ToList());

        if (presentes.Contains("Contacto")
            || presentes.Contains("Canal contacto 1")
            || presentes.Contains("Dato contacto 1")
            || presentes.Contains("Canal contacto 2")
            || presentes.Contains("Dato contacto 2"))
        {
            destino.ContactosProveedorDefinidos = origen.ContactosProveedorDefinidos;
        }
    }

    public static FichaTecnica CombinarListaConNube(FichaTecnica editada, FichaTecnica nube, IEnumerable<string> camposCargados)
    {
        var resultado = nube.Clonar();
        resultado.Id = editada.Id;
        CopiarCampos(resultado, editada, camposCargados);
        resultado.LecturaCompleta = true;
        return resultado;
    }

    public static string Huella(FichaTecnica ficha)
    {
        var documento = Crear(ficha);
        var partes = new List<string>(documento.Count);
        foreach (var clave in documento.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (clave is "Partidas" or "Meta")
            {
                continue;
            }

            partes.Add(clave + "=" + ValorTexto(documento[clave]));
        }

        if (documento["Partidas"] is IEnumerable<object> partidas)
        {
            foreach (var partida in partidas.OfType<IDictionary<string, object>>())
            {
                partes.Add(string.Join("|", ColumnasPartida.Select(c => Texto(partida, c))) + "|" + Texto(partida, "Id"));
            }
        }

        var meta = Mapa(documento["Meta"]);
        partes.Add(Texto(meta, "Id área"));
        return string.Join("\n", partes);
    }

    private static List<PartidaInventario> LeerPartidas(IDictionary<string, object> datos)
    {
        if (!datos.TryGetValue("Partidas", out var raw) || raw is not IEnumerable<object> lista)
        {
            return [];
        }

        var partidas = new List<PartidaInventario>();
        foreach (var item in lista)
        {
            var mapa = Mapa(item);
            if (mapa.Count == 0)
            {
                continue;
            }

            partidas.Add(new PartidaInventario
            {
                Id = Guid.TryParse(Texto(mapa, "Id"), out var id) ? id : Guid.NewGuid(),
                Lote = Texto(mapa, "Lote"),
                FechaCaducidad = ParseFecha(Texto(mapa, "Caducidad")),
                Cantidad = Numero(mapa, "Cantidad")
            });
        }

        return partidas;
    }

    private static (string Canal1, string Dato1, string Canal2, string Dato2) Contactos(FichaTecnica ficha) =>
    (
        string.IsNullOrWhiteSpace(ficha.ContactoDato1) ? string.Empty : CanalDatoProveedor.De(ficha.ContactoCanal1).Titulo,
        ficha.ContactoDato1,
        string.IsNullOrWhiteSpace(ficha.ContactoDato2) ? string.Empty : CanalDatoProveedor.De(ficha.ContactoCanal2).Titulo,
        ficha.ContactoDato2
    );

    private static string Canal(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var opcion = CanalDatoProveedor.Opciones.FirstOrDefault(o =>
            string.Equals(o.Id, texto, StringComparison.OrdinalIgnoreCase)
            || string.Equals(o.Titulo, texto, StringComparison.CurrentCultureIgnoreCase));
        return opcion?.Id ?? CanalDatoProveedor.Web;
    }

    private static Dictionary<string, object> Mapa(object? valor) =>
        valor as Dictionary<string, object>
        ?? (valor as IDictionary<string, object>)?.ToDictionary(p => p.Key, p => p.Value)
        ?? [];

    private static string Texto(IDictionary<string, object> datos, string clave) =>
        datos.TryGetValue(clave, out var valor) ? ValorTexto(valor) : string.Empty;

    private static string ValorTexto(object? valor) => valor switch
    {
        null => string.Empty,
        string texto => texto.Trim(),
        double numero => numero.ToString("0.##", CultureInfo.InvariantCulture),
        float numero => numero.ToString("0.##", CultureInfo.InvariantCulture),
        long numero => numero.ToString(CultureInfo.InvariantCulture),
        int numero => numero.ToString(CultureInfo.InvariantCulture),
        bool bandera => bandera ? "Sí" : "No",
        _ => Convert.ToString(valor, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty
    };

    private static double Numero(IDictionary<string, object> datos, string clave)
    {
        if (!datos.TryGetValue(clave, out var valor) || valor is null)
        {
            return 0;
        }

        return valor switch
        {
            double numero => numero,
            float numero => numero,
            long numero => numero,
            int numero => numero,
            string texto when double.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariante) => invariante,
            string texto when double.TryParse(texto, NumberStyles.Any, CultureInfo.CurrentCulture, out var local) => local,
            _ => 0
        };
    }

    private static string Fecha(DateTimeOffset? fecha) =>
        fecha?.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FechaHora(DateTimeOffset fecha) =>
        fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    private static DateTimeOffset? ParseFecha(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var formatos = new[] { "dd/MM/yyyy HH:mm", "d/M/yyyy H:mm", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };
        if (DateTime.TryParseExact(texto, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exacta)
            || DateTime.TryParse(texto, CultureInfo.GetCultureInfo("es-MX"), DateTimeStyles.None, out exacta))
        {
            return new DateTimeOffset(DateTime.SpecifyKind(exacta, DateTimeKind.Local));
        }

        return null;
    }

    private static string SiNo(bool valor) => valor ? "Sí" : "No";

    private static bool EsSi(string texto)
    {
        var normal = texto.Trim().ToLowerInvariant();
        return normal is "si" or "sí" or "true" or "1" or "x" or "yes";
    }
}
