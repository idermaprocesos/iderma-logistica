using System.Text.Json.Serialization;
using IdermaFichas.Helpers;

namespace IdermaFichas.Models;

public sealed record ContactoProveedor
{
    public string Nombre { get; init; } = string.Empty;
    public string Canal1 { get; init; } = CanalDatoProveedor.WhatsApp;
    public string Dato1 { get; init; } = string.Empty;
    public string Canal2 { get; init; } = CanalDatoProveedor.Web;
    public string Dato2 { get; init; } = string.Empty;

    public string Telefono1 { get; init; } = string.Empty;
    public string Telefono2 { get; init; } = string.Empty;
    public string Dato { get; init; } = string.Empty;
    public string Canal { get; init; } = CanalDatoProveedor.Web;
    public string Canal3 { get; init; } = CanalDatoProveedor.Web;
    public string Dato3 { get; init; } = string.Empty;

    [JsonIgnore]
    public int Usos { get; init; }

    [JsonIgnore]
    public string Resumen { get; init; } = string.Empty;

    [JsonIgnore]
    public bool EstaVacio =>
        Nombre.Length == 0 && Dato1.Length == 0 && Dato2.Length == 0
        && Telefono1.Length == 0 && Telefono2.Length == 0 && Dato.Length == 0 && Dato3.Length == 0;

    [JsonIgnore]
    public string Clave => $"{Nombre}\n{Canal1}\n{Dato1}\n{Canal2}\n{Dato2}";

    public static ContactoProveedor Crear(
        string? nombre,
        string? canal1,
        string? dato1,
        string? canal2,
        string? dato2,
        int usos = 1)
    {
        var limpio = (nombre ?? string.Empty).Trim();
        var c1 = CanalDatoProveedor.Normalizar(canal1);
        var d1 = (dato1 ?? string.Empty).Trim();
        var c2 = CanalDatoProveedor.Normalizar(canal2);
        var d2 = (dato2 ?? string.Empty).Trim();
        var contacto = new ContactoProveedor
        {
            Nombre = limpio,
            Canal1 = c1,
            Dato1 = d1,
            Canal2 = c2,
            Dato2 = d2,
            Telefono1 = d1,
            Telefono2 = d2,
            Dato = d2,
            Canal = c2,
            Usos = usos
        };
        return contacto with { Resumen = ArmarResumen(contacto, usos) };
    }

    public void AplicarA(FichaTecnica ficha)
    {
        var limpio = Normalizar(this);
        ficha.Proveedor = limpio.Nombre;
        ficha.ContactoCanal1 = limpio.Canal1;
        ficha.ContactoDato1 = limpio.Dato1;
        ficha.ContactoCanal2 = limpio.Canal2;
        ficha.ContactoDato2 = limpio.Dato2;
        ficha.ContactoCanal3 = CanalDatoProveedor.Web;
        ficha.ContactoDato3 = string.Empty;
        ficha.ContactosProveedorDefinidos = true;
        ficha.SincronizarCamposProveedorLegados();
    }

    public static ContactoProveedor Normalizar(ContactoProveedor origen, int usos = 1)
    {
        var nombre = (origen.Nombre ?? string.Empty).Trim();
        if ((origen.Dato1 ?? string.Empty).Trim().Length > 0
            || (origen.Dato2 ?? string.Empty).Trim().Length > 0
            || (origen.Dato3 ?? string.Empty).Trim().Length > 0
            || nombre.Length > 0)
        {
            var d2 = (origen.Dato2 ?? string.Empty).Trim();
            var c2 = origen.Canal2;
            if (d2.Length == 0 && (origen.Dato3 ?? string.Empty).Trim().Length > 0)
            {
                d2 = (origen.Dato3 ?? string.Empty).Trim();
                c2 = origen.Canal3;
            }

            return Crear(nombre, origen.Canal1, origen.Dato1, c2, d2, usos);
        }

        return Crear(
            nombre,
            CanalDatoProveedor.WhatsApp,
            origen.Telefono1,
            string.IsNullOrWhiteSpace(origen.Dato) ? CanalDatoProveedor.WhatsApp : origen.Canal,
            string.IsNullOrWhiteSpace(origen.Dato) ? origen.Telefono2 : origen.Dato,
            usos);
    }

    public static IReadOnlyList<ContactoProveedor> Combinar(
        IEnumerable<ContactoProveedor> guardados,
        IEnumerable<FichaTecnica> fichas,
        IEnumerable<string>? ocultos = null)
    {
        var deFichas = fichas.Select(De).Where(c => !c.EstaVacio).ToList();
        var deGuardados = guardados.Select(g => Normalizar(g)).Where(c => !c.EstaVacio).ToList();
        var clavesGuardadas = deGuardados
            .Select(c => c.Clave)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ocultosSet = (ocultos ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return deGuardados.Concat(deFichas)
            .Where(c => !ocultosSet.Contains(c.Clave))
            .GroupBy(c => c.Clave, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var primero = g.First();
                return Normalizar(primero, g.Count());
            })
            .OrderByDescending(c => clavesGuardadas.Contains(c.Clave))
            .ThenByDescending(c => c.Usos)
            .ThenBy(c => c.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ContactoProveedor> DesdeFichas(IEnumerable<FichaTecnica> fichas) =>
        Combinar([], fichas);

    private static ContactoProveedor De(FichaTecnica ficha)
    {
        if (ficha.ContactosProveedorDefinidos)
        {
            return Crear(
                ficha.Proveedor,
                ficha.ContactoCanal1,
                ficha.ContactoDato1,
                ficha.ContactoCanal2,
                ficha.ContactoDato2);
        }

        return Crear(
            ficha.Proveedor,
            CanalDatoProveedor.WhatsApp,
            ficha.TelefonoProveedor1,
            string.IsNullOrWhiteSpace(ficha.DatoProveedor) ? CanalDatoProveedor.WhatsApp : ficha.CanalDatoProveedor,
            string.IsNullOrWhiteSpace(ficha.DatoProveedor) ? ficha.TelefonoProveedor2 : ficha.DatoProveedor);
    }

    private static string ArmarResumen(ContactoProveedor contacto, int usos)
    {
        var partes = new List<string>();
        if (contacto.Nombre.Length > 0)
        {
            partes.Add(contacto.Nombre);
        }

        void Agregar(string canal, string dato)
        {
            if (dato.Length == 0)
            {
                return;
            }

            partes.Add($"{CanalDatoProveedor.De(canal).Titulo}: {dato}");
        }

        Agregar(contacto.Canal1, contacto.Dato1);
        Agregar(contacto.Canal2, contacto.Dato2);
        var texto = partes.Count == 0 ? "(sin nombre)" : string.Join(" · ", partes);
        return usos > 1 ? $"{texto}  ({usos})" : texto;
    }
}
