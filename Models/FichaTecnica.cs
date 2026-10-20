using System.Text.Json.Serialization;
using IdermaFichas.Helpers;
using CanalContacto = IdermaFichas.Helpers.CanalDatoProveedor;

namespace IdermaFichas.Models;

public class FichaTecnica
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [JsonPropertyName("area")]
    [JsonConverter(typeof(AreaIdJsonConverter))]
    public Guid? Area { get; set; }

    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    public string EstadoFisico { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Dimensiones { get; set; } = string.Empty;
    public string PesoVolumen { get; set; } = string.Empty;
    public string MaterialFabricacion { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;

    public string Marca { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string NormasCertificaciones { get; set; } = string.Empty;
    public string VidaUtil { get; set; } = string.Empty;
    public double Existencia { get; set; }
    public double StockMinimo { get; set; }

    public string Fabricante { get; set; } = string.Empty;
    public string PaisOrigen { get; set; } = string.Empty;
    public string PlantaOrigen { get; set; } = string.Empty;
    public string TipoOrigen { get; set; } = string.Empty;
    public string Proveedor { get; set; } = string.Empty;
    public string ContactoProveedor { get; set; } = string.Empty;
    public string TelefonoProveedor1 { get; set; } = string.Empty;
    public string TelefonoProveedor2 { get; set; } = string.Empty;
    public string DatoProveedor { get; set; } = string.Empty;
    public string CanalDatoProveedor { get; set; } = "web";
    public string ContactoCanal1 { get; set; } = "whatsapp";
    public string ContactoDato1 { get; set; } = string.Empty;
    public string ContactoCanal2 { get; set; } = "whatsapp";
    public string ContactoDato2 { get; set; } = string.Empty;
    public string ContactoCanal3 { get; set; } = "web";
    public string ContactoDato3 { get; set; } = string.Empty;
    public bool ContactosProveedorDefinidos { get; set; }
    public string Lote { get; set; } = string.Empty;
    /// <summary>Lotes con cantidad y caducidad propias. La existencia de la ficha es la suma.</summary>
    public List<PartidaInventario> Partidas { get; set; } = [];
    public DateTimeOffset? FechaAdquisicion { get; set; }
    public string NumeroFactura { get; set; } = string.Empty;
    public string RegistroSanitario { get; set; } = string.Empty;

    public bool Esteril { get; set; }
    public bool RequiereRefrigeracion { get; set; }
    public DateTimeOffset? FechaCaducidad { get; set; }
    public bool Reutilizable { get; set; }

    public string Ubicacion { get; set; } = string.Empty;
    public string NumeroSerie { get; set; } = string.Empty;
    public double GarantiaMeses { get; set; }
    public bool RequiereMantenimiento { get; set; }
    public string PeriodicidadMantenimiento { get; set; } = string.Empty;
    public string Voltaje { get; set; } = string.Empty;
    public string Compatibilidad { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;

    public string PrincipioActivo { get; set; } = string.Empty;
    public string Concentracion { get; set; } = string.Empty;
    public string Ph { get; set; } = string.Empty;
    public string SuperficiesUso { get; set; } = string.Empty;
    public string TiempoContacto { get; set; } = string.Empty;
    public string Dilucion { get; set; } = string.Empty;
    public string Peligrosidad { get; set; } = string.Empty;
    public string HojaSeguridad { get; set; } = string.Empty;
    public string EppRequerido { get; set; } = string.Empty;
    public string MaterialesCompatibles { get; set; } = string.Empty;
    public string ResiduoGenerado { get; set; } = string.Empty;

    public string Observaciones { get; set; } = string.Empty;

    /// <summary>Hasta 20 características adicionales, persistidas aparte del catálogo.</summary>
    public List<CaracteristicaProducto> Caracteristicas { get; set; } = [];

    public DateTimeOffset FechaCreacion { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset FechaActualizacion { get; set; } = DateTimeOffset.Now;

    /// <summary>null: copia local anterior, con la ficha entera. false: solo llegaron las columnas de las listas.</summary>
    public bool? LecturaCompleta { get; set; }

    [JsonIgnore]
    public bool TieneFichaCompleta => LecturaCompleta != false;

    [JsonIgnore]
    public string AreaTitulo => AreasOperativas.Titulo(Area);

    [JsonIgnore]
    public string Resumen => string.IsNullOrWhiteSpace(Categoria)
        ? Codigo
        : $"{Codigo} · {Categoria}";

    [JsonIgnore]
    public bool EstaStockCritico =>
        StockMinimo > 0 && Existencia <= StockMinimo;

    [JsonIgnore]
    public bool EstaVencido =>
        Partidas.Any(p => p.Cantidad > 0.0001 && p.EstaVencido)
        || (Partidas.Count == 0
            && FechaCaducidad.HasValue
            && FechaCaducidad.Value.Date < DateTimeOffset.Now.Date);

    [JsonIgnore]
    public bool EstaProximoAVencer =>
        Partidas.Any(p => p.Cantidad > 0.0001 && p.EstaProximoAVencer)
        || (Partidas.Count == 0
            && FechaCaducidad.HasValue
            && !EstaVencido
            && FechaCaducidad.Value.Date <= DateTimeOffset.Now.Date.AddDays(30));

    [JsonIgnore]
    public int? DiasParaVencer =>
        FechaCaducidad.HasValue
            ? (int)(FechaCaducidad.Value.Date - DateTimeOffset.Now.Date).TotalDays
            : null;

    [JsonIgnore]
    public string FechaCaducidadTexto =>
        FechaCaducidad?.ToString("dd/MM/yyyy") ?? "Sin caducidad";

    [JsonIgnore]
    public string EstadoCaducidadTexto
    {
        get
        {
            if (!FechaCaducidad.HasValue)
            {
                return "Sin fecha de caducidad";
            }

            if (EstaVencido)
            {
                var dias = Math.Abs(DiasParaVencer ?? 0);
                return $"Vencido hace {dias} día(s)";
            }

            var restantes = DiasParaVencer ?? 0;
            return restantes == 0 ? "Vence hoy" : $"Vence en {restantes} día(s)";
        }
    }

    public FichaTecnica Clonar()
    {
        var copia = (FichaTecnica)MemberwiseClone();
        copia.Caracteristicas = (Caracteristicas ?? []).Select(c => c.Clonar()).ToList();
        copia.Partidas = (Partidas ?? []).Select(p => p.Clonar()).ToList();
        return copia;
    }

    public void PrepararContactosProveedor()
    {
        if (ContactosProveedorDefinidos)
        {
            SincronizarCamposProveedorLegados();
            return;
        }

        var items = new List<(string Canal, string Dato)>();
        void Agregar(string canal, string? valor)
        {
            var texto = (valor ?? string.Empty).Trim();
            if (texto.Length == 0 || items.Count >= 2)
            {
                return;
            }

            items.Add((CanalContacto.Normalizar(canal), texto));
        }

        Agregar(CanalContacto.WhatsApp, TelefonoProveedor1);
        Agregar(CanalContacto.WhatsApp, TelefonoProveedor2);
        Agregar(CanalDatoProveedor, DatoProveedor);

        ContactoCanal1 = items.ElementAtOrDefault(0).Canal ?? CanalContacto.WhatsApp;
        ContactoDato1 = items.ElementAtOrDefault(0).Dato ?? string.Empty;
        ContactoCanal2 = items.ElementAtOrDefault(1).Canal ?? CanalContacto.Web;
        ContactoDato2 = items.ElementAtOrDefault(1).Dato ?? string.Empty;
        ContactoCanal3 = CanalContacto.Web;
        ContactoDato3 = string.Empty;
        if (items.Count == 0)
        {
            ContactoCanal1 = CanalContacto.WhatsApp;
            ContactoCanal2 = CanalContacto.Web;
        }

        ContactosProveedorDefinidos = true;
        SincronizarCamposProveedorLegados();
    }

    public void SincronizarCamposProveedorLegados()
    {
        var slots = new (string Canal, string Dato)[]
        {
            (CanalContacto.Normalizar(ContactoCanal1), (ContactoDato1 ?? string.Empty).Trim()),
            (CanalContacto.Normalizar(ContactoCanal2), (ContactoDato2 ?? string.Empty).Trim())
        };
        ContactoCanal1 = slots[0].Canal;
        ContactoDato1 = slots[0].Dato;
        ContactoCanal2 = slots[1].Canal;
        ContactoDato2 = slots[1].Dato;
        ContactoCanal3 = CanalContacto.Web;
        ContactoDato3 = string.Empty;

        var whatsapp = slots.Where(s => s.Canal == CanalContacto.WhatsApp && s.Dato.Length > 0).ToList();
        TelefonoProveedor1 = whatsapp.ElementAtOrDefault(0).Dato ?? string.Empty;
        TelefonoProveedor2 = whatsapp.ElementAtOrDefault(1).Dato ?? string.Empty;

        var extra = slots.FirstOrDefault(s => s.Dato.Length > 0 && s.Canal != CanalContacto.WhatsApp);
        if (!string.IsNullOrEmpty(extra.Dato))
        {
            DatoProveedor = extra.Dato;
            CanalDatoProveedor = extra.Canal;
        }
        else if (whatsapp.Count > 1)
        {
            DatoProveedor = whatsapp[1].Dato;
            CanalDatoProveedor = CanalContacto.WhatsApp;
        }
    }
}
