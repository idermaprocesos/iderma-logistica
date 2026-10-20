using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class ProveedorFichaSync
{
    public static int Contar(string nombre) => Coinciden(nombre).Count();

    public static void Publicar(string nombreAnterior, ProveedorRegistro proveedor)
    {
        var fichas = Coinciden(nombreAnterior).ToList();
        foreach (var ficha in fichas)
        {
            ficha.Proveedor = proveedor.Nombre.Trim();
            ficha.ContactoCanal1 = proveedor.Canal1;
            ficha.ContactoDato1 = proveedor.Dato1;
            ficha.ContactoCanal2 = proveedor.Canal2;
            ficha.ContactoDato2 = proveedor.Dato2;
            ficha.ContactosProveedorDefinidos = true;
            ficha.SincronizarCamposProveedorLegados();
        }

        if (fichas.Count > 0)
        {
            App.Instance.Repositorio.GuardarFichas(fichas, registrarIngresoAutomatico: false);
        }

        AlmacenHistorialProveedores.ReemplazarPorNombre(
            nombreAnterior,
            ContactoProveedor.Crear(
                proveedor.Nombre,
                proveedor.Canal1,
                proveedor.Dato1,
                proveedor.Canal2,
                proveedor.Dato2));
    }

    public static int Quitar(string nombre)
    {
        var fichas = Coinciden(nombre).ToList();
        foreach (var ficha in fichas)
        {
            ficha.Proveedor = string.Empty;
            ficha.ContactoCanal1 = CanalDatoProveedor.WhatsApp;
            ficha.ContactoDato1 = string.Empty;
            ficha.ContactoCanal2 = CanalDatoProveedor.Web;
            ficha.ContactoDato2 = string.Empty;
            ficha.ContactoCanal3 = CanalDatoProveedor.Web;
            ficha.ContactoDato3 = string.Empty;
            ficha.ContactosProveedorDefinidos = true;
            ficha.SincronizarCamposProveedorLegados();
        }

        if (fichas.Count > 0)
        {
            App.Instance.Repositorio.GuardarFichas(fichas, registrarIngresoAutomatico: false);
        }

        AlmacenHistorialProveedores.QuitarPorNombre(nombre);
        return fichas.Count;
    }

    private static IEnumerable<FichaTecnica> Coinciden(string nombre)
    {
        var limpio = (nombre ?? string.Empty).Trim();
        if (limpio.Length == 0)
        {
            return [];
        }

        return App.Instance.Repositorio.Todas.Where(f =>
            string.Equals((f.Proveedor ?? string.Empty).Trim(), limpio, StringComparison.OrdinalIgnoreCase));
    }
}
