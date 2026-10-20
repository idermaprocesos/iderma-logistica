using System.Text.Json;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public sealed class OrdenesServicioRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly List<OrdenServicioRegistrada> _ordenes = [];
    private readonly object _candado = new();

    public string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "ordenes-servicio-logistica.json");

    public void Cargar()
    {
        lock (_candado)
        {
            _ordenes.Clear();
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
            if (!File.Exists(RutaArchivo))
            {
                GuardarInterno();
                return;
            }

            try
            {
                var cargadas = JsonSerializer.Deserialize<List<OrdenServicioRegistrada>>(File.ReadAllText(RutaArchivo), JsonOptions);
                if (cargadas is not null)
                {
                    _ordenes.AddRange(cargadas.Where(o => o.Documento is not null));
                }
            }
            catch
            {
                ArchivoAtomico.AislarDañado(RutaArchivo);
                GuardarInterno();
            }
        }
    }

    public IReadOnlyList<OrdenServicioRegistrada> Listar()
    {
        lock (_candado)
        {
            return _ordenes.OrderByDescending(o => o.FechaRegistro).ThenByDescending(o => o.Id).ToList();
        }
    }

    public OrdenServicioRegistrada? Obtener(Guid id)
    {
        lock (_candado)
        {
            return _ordenes.FirstOrDefault(o => o.Id == id);
        }
    }

    public void Guardar(OrdenServicioRegistrada orden)
    {
        lock (_candado)
        {
            if (orden.Id == Guid.Empty)
            {
                orden.Id = Guid.NewGuid();
            }

            if (orden.FechaRegistro == default)
            {
                orden.FechaRegistro = DateTimeOffset.Now;
            }

            orden.Documento ??= new OrdenServicioDocumento();
            var indice = _ordenes.FindIndex(o => o.Id == orden.Id);
            if (indice >= 0)
            {
                _ordenes[indice] = orden;
            }
            else
            {
                _ordenes.Add(orden);
            }

            GuardarInterno();
        }
    }

    public bool Eliminar(Guid id)
    {
        lock (_candado)
        {
            if (_ordenes.RemoveAll(o => o.Id == id) == 0)
            {
                return false;
            }

            GuardarInterno();
            return true;
        }
    }

    public static LineaOrdenServicio DesdeProveedor(ProveedorRegistro proveedor, int meses = 1)
    {
        var contrato = proveedor.AcuerdoContrato && proveedor.CantidadContrato > 0
            ? proveedor.TextoGestionCompra
            : "Sin acuerdo de compra mínima";
        return new LineaOrdenServicio
        {
            ProveedorId = proveedor.Id,
            Proveedor = proveedor.Nombre,
            Ruc = proveedor.Ruc,
            Contrato = contrato,
            Meses = Math.Clamp(meses, 1, 36)
        };
    }

    public static LineaOrdenServicio DesdeGuardada(LineaOrdenServicioExportacion linea) => new()
    {
        ProveedorId = linea.ProveedorId,
        Proveedor = linea.Proveedor,
        Ruc = linea.Ruc,
        Contrato = linea.Contrato,
        Servicio = linea.Servicio,
        VigenciaActual = linea.VigenciaActual,
        Meses = Math.Clamp(linea.Meses, 1, 36),
        MontoMensual = linea.MontoMensual
    };

    public static int AplicarAmpliacion(OrdenServicioDocumento documento)
    {
        var aplicadas = 0;
        foreach (var linea in documento.Lineas)
        {
            var proveedor = App.Instance.Proveedores.Obtener(linea.ProveedorId);
            if (proveedor is null)
            {
                continue;
            }

            var nota = $"Orden de servicio: contrato ampliado {linea.Meses} mes(es) hasta el {linea.NuevaVigencia:dd/MM/yyyy}.";
            if (!proveedor.Notas.Contains(nota, StringComparison.Ordinal))
            {
                proveedor.Notas = string.IsNullOrWhiteSpace(proveedor.Notas)
                    ? nota
                    : $"{proveedor.Notas.Trim()} {nota}";
                App.Instance.Proveedores.Guardar(proveedor);
            }

            aplicadas++;
        }

        return aplicadas;
    }

    private void GuardarInterno()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        ArchivoAtomico.EscribirTexto(RutaArchivo, JsonSerializer.Serialize(_ordenes, JsonOptions));
    }
}
