using System.Text.Json;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public sealed class ProveedoresRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly List<ProveedorRegistro> _items = [];
    private readonly object _candado = new();
    private bool _importadoDeProductos;
    private bool _desdeNube;

    public string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "proveedores.json");

    public bool ImportadoDeProductos
    {
        get
        {
            lock (_candado)
            {
                return _importadoDeProductos;
            }
        }
    }

    public void Cargar()
    {
        lock (_candado)
        {
            _items.Clear();
            _importadoDeProductos = false;
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
            if (!File.Exists(RutaArchivo))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(RutaArchivo);
                var documento = JsonSerializer.Deserialize<DocumentoProveedores>(json, JsonOptions);
                if (documento is null)
                {
                    return;
                }

                _importadoDeProductos = documento.ImportadoDeProductos;
                foreach (var proveedor in documento.Proveedores)
                {
                    var nombre = (proveedor.Nombre ?? string.Empty).Trim();
                    if (proveedor.Id == Guid.Empty || nombre.Length == 0)
                    {
                        continue;
                    }

                    proveedor.Nombre = nombre;
                    proveedor.Contacto = (proveedor.Contacto ?? string.Empty).Trim();
                    proveedor.Notas = (proveedor.Notas ?? string.Empty).Trim();
                    proveedor.Calificacion = CalificacionProveedor.Normalizar(proveedor.Calificacion);
                    NormalizarGestionCompra(proveedor);
                    if (string.IsNullOrWhiteSpace(proveedor.Dato1) &&
                        string.IsNullOrWhiteSpace(proveedor.Dato2) &&
                        !string.IsNullOrWhiteSpace(proveedor.Contacto))
                    {
                        proveedor.Dato1 = proveedor.Contacto.Trim();
                        proveedor.Canal1 = CanalDatoProveedor.Web;
                    }

                    NormalizarContacto(proveedor);
                    if (proveedor.FechaAgregado == default)
                    {
                        proveedor.FechaAgregado = DateTimeOffset.Now;
                    }

                    _items.Add(proveedor);
                }
            }
            catch
            {
                ArchivoAtomico.AislarDañado(RutaArchivo);
                _items.Clear();
                _importadoDeProductos = false;
            }
        }
    }

    public IReadOnlyList<ProveedorRegistro> Listar()
    {
        lock (_candado)
        {
            return _items
                .OrderBy(p => p.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }

    public IReadOnlyList<int> Anios()
    {
        lock (_candado)
        {
            return _items
                .Select(p => p.FechaAgregado.ToLocalTime().Year)
                .Distinct()
                .OrderByDescending(a => a)
                .ToList();
        }
    }

    public ProveedorRegistro? Obtener(Guid id)
    {
        lock (_candado)
        {
            return _items.FirstOrDefault(p => p.Id == id);
        }
    }

    public bool ExisteNombre(string nombre, Guid excepto)
    {
        var limpio = nombre.Trim();
        lock (_candado)
        {
            return _items.Any(p =>
                p.Id != excepto &&
                string.Equals(p.Nombre, limpio, StringComparison.OrdinalIgnoreCase));
        }
    }

    public bool Guardar(ProveedorRegistro proveedor)
    {
        var nombre = (proveedor.Nombre ?? string.Empty).Trim();
        if (nombre.Length == 0 || ExisteNombre(nombre, proveedor.Id))
        {
            return false;
        }

        lock (_candado)
        {
            proveedor.Nombre = nombre;
            proveedor.Notas = (proveedor.Notas ?? string.Empty).Trim();
            proveedor.Calificacion = CalificacionProveedor.Normalizar(proveedor.Calificacion);
            NormalizarContacto(proveedor);
            NormalizarGestionCompra(proveedor);

            var indice = _items.FindIndex(p => p.Id == proveedor.Id);
            if (indice < 0)
            {
                if (proveedor.Id == Guid.Empty)
                {
                    proveedor.Id = Guid.NewGuid();
                }

                if (proveedor.FechaAgregado == default)
                {
                    proveedor.FechaAgregado = DateTimeOffset.Now;
                }

                _items.Add(proveedor);
            }
            else
            {
                proveedor.FechaAgregado = _items[indice].FechaAgregado;
                _items[indice] = proveedor;
            }

            GuardarInterno();
            return true;
        }
    }

    public bool ActualizarContacto(Guid id, int indice, string? canal, string? dato, bool cambiarCanal, bool cambiarDato)
    {
        lock (_candado)
        {
            var proveedor = _items.FirstOrDefault(p => p.Id == id);
            if (proveedor is null)
            {
                return false;
            }

            var canalNormalizado = CanalDatoProveedor.Normalizar(canal);
            var datoLimpio = (dato ?? string.Empty).Trim();
            var cambio = false;
            if (indice == 2)
            {
                if (cambiarCanal && proveedor.Canal2 != canalNormalizado)
                {
                    proveedor.Canal2 = canalNormalizado;
                    cambio = true;
                }

                if (cambiarDato && proveedor.Dato2 != datoLimpio)
                {
                    proveedor.Dato2 = datoLimpio;
                    cambio = true;
                }
            }
            else
            {
                if (cambiarCanal && proveedor.Canal1 != canalNormalizado)
                {
                    proveedor.Canal1 = canalNormalizado;
                    cambio = true;
                }

                if (cambiarDato && proveedor.Dato1 != datoLimpio)
                {
                    proveedor.Dato1 = datoLimpio;
                    cambio = true;
                }
            }

            if (!cambio)
            {
                return true;
            }

            proveedor.FechaContacto = DateTimeOffset.Now;
            proveedor.Contacto = ResumenContacto(proveedor);
            GuardarInterno();
            return true;
        }
    }

    public int SincronizarDesdeFichas(IEnumerable<FichaTecnica> fichas, IEnumerable<ContactoProveedor> historial)
    {
        lock (_candado)
        {
            var cambio = false;
            var agregados = 0;
            var porNombre = new Dictionary<string, ProveedorRegistro>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _items)
            {
                porNombre.TryAdd(item.Nombre, item);
            }
            foreach (var grupo in fichas
                .Where(f => !string.IsNullOrWhiteSpace(f.Proveedor))
                .GroupBy(f => f.Proveedor.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var reciente = grupo.OrderByDescending(f => f.FechaActualizacion).First();
                var copia = reciente.Clonar();
                copia.PrepararContactosProveedor();
                var fecha = grupo.Max(f => f.FechaActualizacion);
                var alta = grupo
                    .Select(f => f.FechaCreacion == default ? fecha : f.FechaCreacion)
                    .DefaultIfEmpty(DateTimeOffset.Now)
                    .Min();
                if (!porNombre.TryGetValue(grupo.Key, out var registro))
                {
                    registro = new ProveedorRegistro
                    {
                        Nombre = grupo.Key,
                        Canal1 = copia.ContactoCanal1,
                        Dato1 = copia.ContactoDato1,
                        Canal2 = copia.ContactoCanal2,
                        Dato2 = copia.ContactoDato2,
                        FechaAgregado = alta == default ? DateTimeOffset.Now : alta,
                        FechaContacto = fecha
                    };
                    NormalizarContacto(registro);
                    _items.Add(registro);
                    porNombre[registro.Nombre] = registro;
                    AlmacenHistorialProveedores.ReemplazarPorNombre(registro.Nombre, ContactoDe(registro));
                    agregados++;
                    cambio = true;
                    continue;
                }

                if (registro.FechaContacto == default && alta != default && alta < registro.FechaAgregado)
                {
                    registro.FechaAgregado = alta;
                    cambio = true;
                }

                if (!ContactoDistinto(registro, copia.ContactoCanal1, copia.ContactoDato1, copia.ContactoCanal2, copia.ContactoDato2))
                {
                    continue;
                }

                if (!TieneDatos(copia.ContactoDato1, copia.ContactoDato2))
                {
                    continue;
                }

                var fichaMasNueva = fecha > registro.FechaContacto;
                if (!fichaMasNueva && registro.TieneDatosContacto)
                {
                    continue;
                }

                registro.Canal1 = copia.ContactoCanal1;
                registro.Dato1 = copia.ContactoDato1;
                registro.Canal2 = copia.ContactoCanal2;
                registro.Dato2 = copia.ContactoDato2;
                registro.FechaContacto = fecha == default ? DateTimeOffset.Now : fecha;
                NormalizarContacto(registro);
                AlmacenHistorialProveedores.ReemplazarPorNombre(registro.Nombre, ContactoDe(registro));
                cambio = true;
            }

            foreach (var contacto in historial.Select(c => ContactoProveedor.Normalizar(c)).Where(c => !c.EstaVacio))
            {
                if (contacto.Nombre.Length == 0)
                {
                    continue;
                }

                if (!porNombre.TryGetValue(contacto.Nombre, out var registro))
                {
                    registro = new ProveedorRegistro
                    {
                        Nombre = contacto.Nombre,
                        Canal1 = contacto.Canal1,
                        Dato1 = contacto.Dato1,
                        Canal2 = contacto.Canal2,
                        Dato2 = contacto.Dato2,
                        FechaAgregado = DateTimeOffset.Now,
                        FechaContacto = DateTimeOffset.Now
                    };
                    NormalizarContacto(registro);
                    _items.Add(registro);
                    porNombre[registro.Nombre] = registro;
                    agregados++;
                    cambio = true;
                    continue;
                }

                if (registro.TieneDatosContacto || !TieneDatos(contacto.Dato1, contacto.Dato2))
                {
                    continue;
                }

                registro.Canal1 = contacto.Canal1;
                registro.Dato1 = contacto.Dato1;
                registro.Canal2 = contacto.Canal2;
                registro.Dato2 = contacto.Dato2;
                if (registro.FechaContacto == default)
                {
                    registro.FechaContacto = DateTimeOffset.Now;
                }

                NormalizarContacto(registro);
                cambio = true;
            }

            var marcoImportado = !_importadoDeProductos;
            _importadoDeProductos = true;
            if (!cambio && !marcoImportado)
            {
                return agregados;
            }

            GuardarInterno();
            return agregados;
        }
    }

    public bool ActualizarNotas(Guid id, string notas)
    {
        var texto = (notas ?? string.Empty).Trim();
        lock (_candado)
        {
            var proveedor = _items.FirstOrDefault(p => p.Id == id);
            if (proveedor is null || proveedor.Notas == texto)
            {
                return proveedor is not null;
            }

            proveedor.Notas = texto;
            GuardarInterno();
            return true;
        }
    }

    public bool ActualizarCalificacion(Guid id, string calificacion)
    {
        var normalizada = CalificacionProveedor.Normalizar(calificacion);
        lock (_candado)
        {
            var proveedor = _items.FirstOrDefault(p => p.Id == id);
            if (proveedor is null || proveedor.Calificacion == normalizada)
            {
                return proveedor is not null;
            }

            proveedor.Calificacion = normalizada;
            GuardarInterno();
            return true;
        }
    }

    public bool Eliminar(Guid id)
    {
        lock (_candado)
        {
            var quitados = _items.RemoveAll(p => p.Id == id);
            if (quitados == 0)
            {
                return false;
            }

            GuardarInterno();
            return true;
        }
    }

    public int ImportarNombres(IEnumerable<string> nombres, bool marcarImportado)
    {
        lock (_candado)
        {
            var existentes = _items
                .Select(p => p.Nombre)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var agregados = 0;
            var ahora = DateTimeOffset.Now;
            foreach (var nombre in nombres)
            {
                var limpio = (nombre ?? string.Empty).Trim();
                if (limpio.Length == 0 || !existentes.Add(limpio))
                {
                    continue;
                }

                _items.Add(new ProveedorRegistro
                {
                    Nombre = limpio,
                    FechaAgregado = ahora
                });
                agregados++;
            }

            if (marcarImportado)
            {
                _importadoDeProductos = true;
            }

            if (agregados > 0 || marcarImportado)
            {
                GuardarInterno();
            }

            return agregados;
        }
    }

    private static void NormalizarGestionCompra(ProveedorRegistro proveedor)
    {
        proveedor.Ruc = ProveedorRegistro.NormalizarRuc(proveedor.Ruc);
        proveedor.RazonSocial = (proveedor.RazonSocial ?? string.Empty).Trim();
        proveedor.TiempoEntregaDias = Math.Clamp(proveedor.TiempoEntregaDias, 0, 365);
        proveedor.UnidadContrato = (proveedor.UnidadContrato ?? string.Empty).Trim();
        if (!proveedor.AcuerdoContrato || proveedor.CantidadContrato <= 0)
        {
            proveedor.AcuerdoContrato = false;
            proveedor.CantidadContrato = 0;
            proveedor.UnidadContrato = string.Empty;
        }
    }

    private static void NormalizarContacto(ProveedorRegistro proveedor)
    {
        var canal1Vacio = string.IsNullOrWhiteSpace(proveedor.Canal1);
        var canal2Vacio = string.IsNullOrWhiteSpace(proveedor.Canal2);
        proveedor.Dato1 = (proveedor.Dato1 ?? string.Empty).Trim();
        proveedor.Dato2 = (proveedor.Dato2 ?? string.Empty).Trim();
        proveedor.Canal1 = CanalDatoProveedor.Normalizar(canal1Vacio ? CanalDatoProveedor.WhatsApp : proveedor.Canal1);
        proveedor.Canal2 = CanalDatoProveedor.Normalizar(canal2Vacio ? CanalDatoProveedor.Web : proveedor.Canal2);
        proveedor.Contacto = ResumenContacto(proveedor);
    }

    private static string ResumenContacto(ProveedorRegistro proveedor)
    {
        var partes = new List<string>();
        void Agregar(string canal, string dato)
        {
            if (dato.Length == 0)
            {
                return;
            }

            partes.Add($"{CanalDatoProveedor.De(canal).Titulo}: {dato}");
        }

        Agregar(proveedor.Canal1, proveedor.Dato1);
        Agregar(proveedor.Canal2, proveedor.Dato2);
        return string.Join(" · ", partes);
    }

    private static bool TieneDatos(string? dato1, string? dato2) =>
        (dato1 ?? string.Empty).Trim().Length > 0 || (dato2 ?? string.Empty).Trim().Length > 0;

    private static bool ContactoDistinto(
        ProveedorRegistro registro, string canal1, string dato1, string canal2, string dato2) =>
        !string.Equals(CanalDatoProveedor.Normalizar(registro.Canal1), CanalDatoProveedor.Normalizar(canal1), StringComparison.OrdinalIgnoreCase)
        || !string.Equals(registro.Dato1.Trim(), (dato1 ?? string.Empty).Trim(), StringComparison.Ordinal)
        || !string.Equals(CanalDatoProveedor.Normalizar(registro.Canal2), CanalDatoProveedor.Normalizar(canal2), StringComparison.OrdinalIgnoreCase)
        || !string.Equals(registro.Dato2.Trim(), (dato2 ?? string.Empty).Trim(), StringComparison.Ordinal);

    private static ContactoProveedor ContactoDe(ProveedorRegistro proveedor) =>
        ContactoProveedor.Crear(proveedor.Nombre, proveedor.Canal1, proveedor.Dato1, proveedor.Canal2, proveedor.Dato2);

    private void GuardarInterno()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        var documento = new DocumentoProveedores
        {
            ImportadoDeProductos = _importadoDeProductos,
            Proveedores = _items
        };
        ArchivoAtomico.EscribirTexto(RutaArchivo, JsonSerializer.Serialize(documento, JsonOptions));
        if (_desdeNube)
        {
            return;
        }

        ModulosNube.PublicarProveedores(_items.Select(Clonar).ToList());
    }

    public bool AplicarDesdeNube(IReadOnlyList<ProveedorRegistro> cambios, IReadOnlyCollection<Guid> eliminados, bool reemplazar)
    {
        var huboCambio = false;
        lock (_candado)
        {
            _desdeNube = true;
            try
            {
                if (reemplazar)
                {
                    _items.Clear();
                    foreach (var cambio in cambios)
                    {
                        var proveedor = Clonar(cambio);
                        NormalizarContacto(proveedor);
                        NormalizarGestionCompra(proveedor);
                        _items.Add(proveedor);
                    }

                    huboCambio = true;
                }
                else
                {
                    var ids = eliminados.ToHashSet();
                    if (_items.RemoveAll(p => ids.Contains(p.Id)) > 0)
                    {
                        huboCambio = true;
                    }

                    foreach (var cambio in cambios)
                    {
                        var proveedor = Clonar(cambio);
                        NormalizarContacto(proveedor);
                        NormalizarGestionCompra(proveedor);
                        var indice = _items.FindIndex(p => p.Id == proveedor.Id);
                        if (indice >= 0)
                        {
                            if (Mismo(_items[indice], proveedor))
                            {
                                continue;
                            }

                            _items[indice] = proveedor;
                        }
                        else
                        {
                            _items.Add(proveedor);
                        }

                        huboCambio = true;
                    }
                }

                if (huboCambio)
                {
                    GuardarInterno();
                }
            }
            finally
            {
                _desdeNube = false;
            }
        }

        return huboCambio;
    }

    private static ProveedorRegistro Clonar(ProveedorRegistro proveedor) => new()
    {
        Id = proveedor.Id,
        Nombre = proveedor.Nombre,
        Contacto = proveedor.Contacto,
        Canal1 = proveedor.Canal1,
        Dato1 = proveedor.Dato1,
        Canal2 = proveedor.Canal2,
        Dato2 = proveedor.Dato2,
        Notas = proveedor.Notas,
        Calificacion = proveedor.Calificacion,
        Ruc = proveedor.Ruc,
        RazonSocial = proveedor.RazonSocial,
        TiempoEntregaDias = proveedor.TiempoEntregaDias,
        AcuerdoContrato = proveedor.AcuerdoContrato,
        CantidadContrato = proveedor.CantidadContrato,
        UnidadContrato = proveedor.UnidadContrato,
        FechaAgregado = proveedor.FechaAgregado,
        FechaContacto = proveedor.FechaContacto
    };

    private static bool Mismo(ProveedorRegistro actual, ProveedorRegistro cambio) =>
        actual.Nombre == cambio.Nombre
        && actual.Contacto == cambio.Contacto
        && actual.Canal1 == cambio.Canal1
        && actual.Dato1 == cambio.Dato1
        && actual.Canal2 == cambio.Canal2
        && actual.Dato2 == cambio.Dato2
        && actual.Notas == cambio.Notas
        && actual.Calificacion == cambio.Calificacion
        && actual.Ruc == cambio.Ruc
        && actual.RazonSocial == cambio.RazonSocial
        && actual.TiempoEntregaDias == cambio.TiempoEntregaDias
        && actual.AcuerdoContrato == cambio.AcuerdoContrato
        && actual.CantidadContrato == cambio.CantidadContrato
        && actual.UnidadContrato == cambio.UnidadContrato
        && actual.FechaAgregado == cambio.FechaAgregado
        && actual.FechaContacto == cambio.FechaContacto;

    private sealed class DocumentoProveedores
    {
        public bool ImportadoDeProductos { get; set; }
        public List<ProveedorRegistro> Proveedores { get; set; } = [];
    }
}
