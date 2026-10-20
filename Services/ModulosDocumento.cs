using System.Globalization;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

internal static class ModulosDocumento
{
    public static readonly string[] CamposMovimiento =
    [
        "Id ficha", "Id partida", "Id área", "Fecha", "Tipo", "Origen", "Cantidad",
        "Existencia resultante", "Lote", "Fecha caducidad", "Tipo documento",
        "Número documento", "Nota", "Nombre recibo", "Actualizado"
    ];

    public static readonly string[] CamposPrecio =
    [
        "Id ficha", "Fecha", "Monto", "Moneda", "Nota", "Actualizado"
    ];

    public static Dictionary<string, object> CrearVencimiento(FichaTecnica ficha, PartidaInventario partida)
    {
        var documento = Identidad(ficha);
        documento["Id partida"] = partida.Id.ToString("D");
        documento["Lote"] = partida.Lote;
        documento["Fecha caducidad"] = Fecha(partida.FechaCaducidad);
        documento["Existencia"] = partida.Cantidad;
        documento["Estado lote"] = partida.EstadoCaducidadTexto;
        return documento;
    }

    public static bool LeerVencimiento(DocumentoNube documento, out Guid fichaId, out PartidaInventario partida)
    {
        fichaId = Guid.Empty;
        partida = new PartidaInventario();
        var datos = documento.Datos;
        if (!Guid.TryParse(Texto(datos, "Id ficha"), out fichaId) && !Guid.TryParse(documento.Id, out _))
        {
            return false;
        }

        if (!Guid.TryParse(Texto(datos, "Id partida"), out var partidaId))
        {
            partidaId = Guid.TryParse(documento.Id, out var idDocumento) ? idDocumento : Guid.Empty;
        }

        if (fichaId == Guid.Empty || partidaId == Guid.Empty)
        {
            return false;
        }

        partida = new PartidaInventario
        {
            Id = partidaId,
            Lote = Texto(datos, "Lote"),
            FechaCaducidad = ParseFecha(Texto(datos, "Fecha caducidad")),
            Cantidad = Numero(datos, "Existencia")
        };
        return true;
    }

    public static Dictionary<string, object> CrearMovimiento(MovimientoInventario movimiento, FichaTecnica? ficha)
    {
        var documento = Identidad(ficha);
        documento["Id ficha"] = movimiento.FichaId.ToString("D");
        documento["Id partida"] = movimiento.PartidaId?.ToString("D") ?? string.Empty;
        documento["Id área"] = movimiento.Area?.ToString("D") ?? string.Empty;
        documento["Fecha"] = FechaHora(movimiento.Fecha);
        documento["Tipo"] = string.IsNullOrWhiteSpace(movimiento.Tipo) ? "Ingreso" : movimiento.Tipo;
        documento["Origen"] = movimiento.Origen;
        documento["Cantidad"] = movimiento.Cantidad;
        documento["Existencia resultante"] = movimiento.ExistenciaResultante;
        documento["Lote"] = movimiento.Lote;
        documento["Fecha caducidad"] = Fecha(movimiento.FechaCaducidad);
        documento["Tipo documento"] = movimiento.TipoDocumento;
        documento["Número documento"] = movimiento.NumeroDocumento;
        documento["Nota"] = movimiento.Nota;
        documento["Nombre recibo"] = movimiento.NombreRecibo;
        return documento;
    }

    public static MovimientoInventario? LeerMovimiento(DocumentoNube documento)
    {
        if (!Guid.TryParse(documento.Id, out var id))
        {
            return null;
        }

        var datos = documento.Datos;
        if (!Guid.TryParse(Texto(datos, "Id ficha"), out var fichaId))
        {
            return null;
        }

        return new MovimientoInventario
        {
            Id = id,
            FichaId = fichaId,
            PartidaId = Guid.TryParse(Texto(datos, "Id partida"), out var partidaId) ? partidaId : null,
            Area = Guid.TryParse(Texto(datos, "Id área"), out var areaId) ? areaId : null,
            Fecha = ParseFecha(Texto(datos, "Fecha")) ?? DateTimeOffset.Now,
            Tipo = Texto(datos, "Tipo"),
            Origen = Texto(datos, "Origen"),
            Cantidad = Numero(datos, "Cantidad"),
            ExistenciaResultante = Numero(datos, "Existencia resultante"),
            Lote = Texto(datos, "Lote"),
            FechaCaducidad = ParseFecha(Texto(datos, "Fecha caducidad")),
            TipoDocumento = Texto(datos, "Tipo documento"),
            NumeroDocumento = Texto(datos, "Número documento"),
            Nota = Texto(datos, "Nota"),
            NombreRecibo = Texto(datos, "Nombre recibo")
        };
    }

    public static Dictionary<string, object> CrearPrecio(PrecioProducto precio, FichaTecnica? ficha)
    {
        var documento = Identidad(ficha);
        documento["Id ficha"] = precio.FichaId.ToString("D");
        documento["Fecha"] = FechaHora(precio.Fecha);
        documento["Monto"] = precio.Monto;
        documento["Moneda"] = MonedaPrecio.Normalizar(precio.Moneda);
        documento["Nota"] = precio.Nota;
        return documento;
    }

    public static PrecioProducto? LeerPrecio(DocumentoNube documento)
    {
        if (!Guid.TryParse(documento.Id, out var id) || !Guid.TryParse(Texto(documento.Datos, "Id ficha"), out var fichaId))
        {
            return null;
        }

        return new PrecioProducto
        {
            Id = id,
            FichaId = fichaId,
            Fecha = ParseFecha(Texto(documento.Datos, "Fecha")) ?? DateTimeOffset.Now,
            Monto = Numero(documento.Datos, "Monto"),
            Moneda = MonedaPrecio.Normalizar(Texto(documento.Datos, "Moneda")),
            Nota = Texto(documento.Datos, "Nota")
        };
    }

    public static Dictionary<string, object> CrearOrden(OrdenCompraRegistrada orden)
    {
        var documento = orden.Documento ?? new OrdenCompraDocumento();
        return new Dictionary<string, object>
        {
            ["Fecha registro"] = FechaHora(orden.FechaRegistro),
            ["Código formato"] = documento.CodigoFormato,
            ["Mes año"] = documento.MesAnio,
            ["Fecha elaboración"] = documento.FechaElaboracion,
            ["Elaborado por"] = documento.ElaboradoPor,
            ["Revisado por"] = documento.RevisadoPor,
            ["Periodo cobertura"] = documento.PeriodoCobertura,
            ["Estado"] = documento.Estado,
            ["Líneas"] = documento.Lineas.Select(linea => (object)Linea(linea)).ToList()
        };
    }

    public static OrdenCompraRegistrada? LeerOrden(DocumentoNube documento)
    {
        if (!Guid.TryParse(documento.Id, out var id))
        {
            return null;
        }

        var datos = documento.Datos;
        var lineas = new List<LineaOrdenExportacion>();
        if (datos.TryGetValue("Líneas", out var raw) && raw is IEnumerable<object> lista)
        {
            foreach (var item in lista)
            {
                var mapa = Mapa(item);
                if (mapa.Count == 0)
                {
                    continue;
                }

                lineas.Add(new LineaOrdenExportacion(
                    Texto(mapa, "Código"),
                    Texto(mapa, "Nombre"),
                    Texto(mapa, "Marca"),
                    Texto(mapa, "Proveedor"),
                    Texto(mapa, "RUC"),
                    Numero(mapa, "Consumo promedio"),
                    Numero(mapa, "Stock actual"),
                    Numero(mapa, "Cantidad a pedir"),
                    Texto(mapa, "Unidad"),
                    Numero(mapa, "Precio sin IGV"),
                    Numero(mapa, "IGV"),
                    Numero(mapa, "Total con IGV"),
                    Texto(mapa, "Tipo precio"),
                    Texto(mapa, "Estado línea"),
                    EsSi(Texto(mapa, "Tiene precio")),
                    MonedaPrecio.Normalizar(Texto(mapa, "Moneda")))
                {
                    FichaId = Guid.TryParse(Texto(mapa, "Id ficha"), out var fichaId) ? fichaId : Guid.Empty
                });
            }
        }

        return new OrdenCompraRegistrada
        {
            Id = id,
            FechaRegistro = ParseFecha(Texto(datos, "Fecha registro")) ?? DateTimeOffset.Now,
            Documento = new OrdenCompraDocumento
            {
                CodigoFormato = Texto(datos, "Código formato"),
                MesAnio = Texto(datos, "Mes año"),
                FechaElaboracion = Texto(datos, "Fecha elaboración"),
                ElaboradoPor = Texto(datos, "Elaborado por"),
                RevisadoPor = Texto(datos, "Revisado por"),
                PeriodoCobertura = Texto(datos, "Periodo cobertura"),
                Estado = Texto(datos, "Estado"),
                Lineas = lineas
            }
        };
    }

    public static Dictionary<string, object> CrearMerma(MermaRegistrada merma) =>
        new()
        {
            ["Fecha"] = FechaHora(merma.Fecha),
            ["Id ficha"] = merma.FichaId == Guid.Empty ? string.Empty : merma.FichaId.ToString("D"),
            ["Código"] = merma.Codigo,
            ["Nombre"] = merma.Nombre,
            ["Lote"] = merma.Lote,
            ["Fecha caducidad"] = merma.FechaCaducidad is null ? string.Empty : FechaHora(merma.FechaCaducidad.Value),
            ["Cantidad"] = merma.Cantidad,
            ["Unidad"] = merma.Unidad,
            ["Motivo"] = merma.Motivo,
            ["Detalle"] = merma.Detalle
        };

    public static MermaRegistrada? LeerMerma(DocumentoNube documento)
    {
        if (!Guid.TryParse(documento.Id, out var id))
        {
            return null;
        }

        var datos = documento.Datos;
        return new MermaRegistrada
        {
            Id = id,
            Fecha = ParseFecha(Texto(datos, "Fecha")) ?? DateTimeOffset.Now,
            FichaId = Guid.TryParse(Texto(datos, "Id ficha"), out var fichaId) ? fichaId : Guid.Empty,
            Codigo = Texto(datos, "Código"),
            Nombre = Texto(datos, "Nombre"),
            Lote = Texto(datos, "Lote"),
            FechaCaducidad = ParseFecha(Texto(datos, "Fecha caducidad")),
            Cantidad = Numero(datos, "Cantidad"),
            Unidad = Texto(datos, "Unidad"),
            Motivo = Texto(datos, "Motivo"),
            Detalle = Texto(datos, "Detalle")
        };
    }

    public static Dictionary<string, object> CrearEquipoControl(EquipoControl equipo) =>
        new()
        {
            ["Código"] = equipo.Codigo,
            ["Nombre"] = equipo.Nombre,
            ["Ubicación"] = equipo.Ubicacion,
            ["Responsable"] = equipo.Responsable,
            ["Clase"] = equipo.Clase.ToString(),
            ["Cada días"] = equipo.IntervaloDias,
            ["Aviso días"] = equipo.AvisoDias,
            ["Último control"] = equipo.UltimoControl is null ? string.Empty : FechaHora(equipo.UltimoControl.Value),
            ["Nota"] = equipo.Nota
        };

    public static EquipoControl? LeerEquipoControl(DocumentoNube documento)
    {
        if (documento.Id == ControlMantenimientoRepository.IdDocumentoRangos || !Guid.TryParse(documento.Id, out var id))
        {
            return null;
        }

        var datos = documento.Datos;
        return new EquipoControl
        {
            Id = id,
            Codigo = Texto(datos, "Código"),
            Nombre = Texto(datos, "Nombre"),
            Ubicacion = Texto(datos, "Ubicación"),
            Responsable = Texto(datos, "Responsable"),
            Clase = Clase(Texto(datos, "Clase")),
            IntervaloDias = (int)Numero(datos, "Cada días"),
            AvisoDias = (int)Numero(datos, "Aviso días"),
            UltimoControl = ParseFecha(Texto(datos, "Último control")),
            Nota = Texto(datos, "Nota")
        };
    }

    public static Dictionary<string, object> CrearRangosControl(RangosControlEquipo rangos) =>
        new()
        {
            ["Preventivo días"] = rangos.PreventivoDias,
            ["Aviso preventivo"] = rangos.AvisoPreventivoDias,
            ["Correctivo días"] = rangos.CorrectivoDias,
            ["Aviso correctivo"] = rangos.AvisoCorrectivoDias,
            ["Crítico días"] = rangos.CriticoDias,
            ["Aviso crítico"] = rangos.AvisoCriticoDias
        };

    public static RangosControlEquipo LeerRangosControl(DocumentoNube documento)
    {
        var datos = documento.Datos;
        var rangos = new RangosControlEquipo
        {
            PreventivoDias = (int)Numero(datos, "Preventivo días"),
            AvisoPreventivoDias = (int)Numero(datos, "Aviso preventivo"),
            CorrectivoDias = (int)Numero(datos, "Correctivo días"),
            AvisoCorrectivoDias = (int)Numero(datos, "Aviso correctivo"),
            CriticoDias = (int)Numero(datos, "Crítico días"),
            AvisoCriticoDias = (int)Numero(datos, "Aviso crítico")
        };
        rangos.Normalizar();
        return rangos;
    }

    private static ClaseControlEquipo Clase(string texto) => texto.Trim().ToLowerInvariant() switch
    {
        "correctivo" => ClaseControlEquipo.Correctivo,
        "critico" or "crítico" => ClaseControlEquipo.Critico,
        _ => ClaseControlEquipo.Preventivo
    };

    public static Dictionary<string, object> CrearProveedor(ProveedorRegistro proveedor, IReadOnlyList<FichaTecnica> fichas)
    {
        var productos = fichas
            .Where(f => string.Equals(f.Proveedor?.Trim(), proveedor.Nombre, StringComparison.OrdinalIgnoreCase))
            .Select(f => (object)new Dictionary<string, object>
            {
                ["Id ficha"] = f.Id.ToString("D"),
                ["Código"] = f.Codigo,
                ["Nombre"] = f.Nombre,
                ["Área"] = f.AreaTitulo,
                ["Marca"] = f.Marca
            })
            .ToList();
        return new Dictionary<string, object>
        {
            ["Nombre"] = proveedor.Nombre,
            ["Contacto"] = proveedor.Contacto,
            ["Canal contacto 1"] = Canal(proveedor.Canal1, proveedor.Dato1),
            ["Dato contacto 1"] = proveedor.Dato1,
            ["Canal contacto 2"] = Canal(proveedor.Canal2, proveedor.Dato2),
            ["Dato contacto 2"] = proveedor.Dato2,
            ["Notas"] = proveedor.Notas,
            ["Calificación"] = CalificacionProveedor.Normalizar(proveedor.Calificacion),
            ["RUC"] = proveedor.Ruc,
            ["Razón social"] = proveedor.RazonSocial,
            ["Tiempo entrega días"] = proveedor.TiempoEntregaDias,
            ["Acuerdo contrato"] = proveedor.AcuerdoContrato,
            ["Cantidad contrato"] = (double)proveedor.CantidadContrato,
            ["Unidad contrato"] = proveedor.UnidadContrato,
            ["Fecha agregado"] = FechaHora(proveedor.FechaAgregado),
            ["Fecha contacto"] = proveedor.FechaContacto == default ? string.Empty : FechaHora(proveedor.FechaContacto),
            ["Productos"] = productos
        };
    }

    public static ProveedorRegistro? LeerProveedor(DocumentoNube documento)
    {
        if (!Guid.TryParse(documento.Id, out var id))
        {
            return null;
        }

        var datos = documento.Datos;
        var nombre = Texto(datos, "Nombre");
        if (nombre.Length == 0)
        {
            return null;
        }

        var proveedor = new ProveedorRegistro
        {
            Id = id,
            Nombre = nombre,
            Contacto = Texto(datos, "Contacto"),
            Canal1 = CanalId(Texto(datos, "Canal contacto 1")),
            Dato1 = Texto(datos, "Dato contacto 1"),
            Canal2 = CanalId(Texto(datos, "Canal contacto 2")),
            Dato2 = Texto(datos, "Dato contacto 2"),
            Notas = Texto(datos, "Notas"),
            Calificacion = CalificacionProveedor.Normalizar(Texto(datos, "Calificación")),
            Ruc = ProveedorRegistro.NormalizarRuc(Texto(datos, "RUC")),
            RazonSocial = Texto(datos, "Razón social"),
            TiempoEntregaDias = (int)Math.Clamp(Numero(datos, "Tiempo entrega días"), 0, 365),
            AcuerdoContrato = EsSi(Texto(datos, "Acuerdo contrato")),
            CantidadContrato = (decimal)Numero(datos, "Cantidad contrato"),
            UnidadContrato = Texto(datos, "Unidad contrato"),
            FechaAgregado = ParseFecha(Texto(datos, "Fecha agregado")) ?? DateTimeOffset.Now,
            FechaContacto = ParseFecha(Texto(datos, "Fecha contacto")) ?? default
        };
        return proveedor;
    }

    private static Dictionary<string, object> Linea(LineaOrdenExportacion linea) =>
        new()
        {
            ["Código"] = linea.Codigo,
            ["Nombre"] = linea.Nombre,
            ["Marca"] = linea.Marca,
            ["Proveedor"] = linea.Proveedor,
            ["RUC"] = linea.Ruc,
            ["Unidad"] = linea.Unidad,
            ["Consumo promedio"] = linea.ConsumoPromedio,
            ["Stock actual"] = linea.StockActual,
            ["Cantidad a pedir"] = linea.CantidadPedir,
            ["Precio sin IGV"] = linea.PrecioUnitarioSinIgv,
            ["IGV"] = linea.Igv,
            ["Total con IGV"] = linea.TotalConIgv,
            ["Tipo precio"] = linea.TipoPrecio,
            ["Estado línea"] = linea.Estado,
            ["Tiene precio"] = linea.TienePrecio ? "Sí" : "No",
            ["Moneda"] = MonedaPrecio.Normalizar(linea.Moneda),
            ["Id ficha"] = linea.FichaId == Guid.Empty ? string.Empty : linea.FichaId.ToString("D")
        };

    private static Dictionary<string, object> Identidad(FichaTecnica? ficha) =>
        new()
        {
            ["Id ficha"] = ficha?.Id.ToString("D") ?? string.Empty,
            ["Código"] = ficha?.Codigo ?? string.Empty,
            ["Nombre"] = ficha?.Nombre ?? string.Empty,
            ["Área"] = ficha?.AreaTitulo ?? string.Empty,
            ["Marca"] = ficha?.Marca ?? string.Empty,
            ["Proveedor"] = ficha?.Proveedor ?? string.Empty
        };

    private static string Canal(string canal, string dato) =>
        string.IsNullOrWhiteSpace(dato) ? string.Empty : CanalDatoProveedor.De(canal).Titulo;

    private static string CanalId(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return CanalDatoProveedor.Web;
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

    private static bool EsSi(string texto)
    {
        var normal = texto.Trim().ToLowerInvariant();
        return normal is "si" or "sí" or "true" or "1" or "x" or "yes";
    }
}
