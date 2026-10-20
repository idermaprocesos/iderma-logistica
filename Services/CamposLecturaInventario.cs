namespace IdermaFichas.Services;

public enum VistaLecturaInventario
{
    Inventario,
    Vencimientos,
    Registro,
    Precios,
    Ordenes,
    Area
}

public static class CamposLecturaInventario
{
    private static readonly string[] Meta = ["Meta", "Actualizado"];

    public static readonly string[] Inventario =
    [
        "Área", "Código", "Nombre", "Categoría",
        "Existencia", "Stock mínimo", "Fecha caducidad",
        "Proveedor", "Ubicación", "Lote", "Partidas",
        .. Meta
    ];

    public static readonly string[] Vencimientos =
    [
        "Área", "Código", "Nombre", "Unidad",
        "Fecha caducidad", "Partidas",
        .. Meta
    ];

    public static readonly string[] Registro =
    [
        "Área", "Código", "Nombre", "Categoría", "Unidad",
        "Existencia", "Stock mínimo", "Fecha caducidad",
        "Proveedor", "Ubicación", "Lote", "Partidas",
        .. Meta
    ];

    public static readonly string[] Precios =
    [
        "Área", "Código", "Nombre", "Categoría",
        "Existencia", "Stock mínimo", "Fecha caducidad",
        "Proveedor", "Ubicación", "Lote", "Partidas",
        .. Meta
    ];

    public static readonly string[] Ordenes =
    [
        "Área", "Código", "Nombre", "Categoría", "Unidad", "Marca",
        "Existencia", "Stock mínimo", "Fecha caducidad",
        "Proveedor", "Ubicación", "Lote", "Partidas",
        .. Meta
    ];

    public static readonly string[] Area =
    [
        "Área", "Código", "Nombre", "Categoría", "Fabricante",
        "Fecha caducidad", "Partidas",
        .. Meta
    ];

    public static IReadOnlyList<string> De(VistaLecturaInventario vista) => vista switch
    {
        VistaLecturaInventario.Vencimientos => Vencimientos,
        VistaLecturaInventario.Registro => Registro,
        VistaLecturaInventario.Precios => Precios,
        VistaLecturaInventario.Ordenes => Ordenes,
        VistaLecturaInventario.Area => Area,
        _ => Inventario
    };

    public static IReadOnlyList<string> Listas { get; } = Unir(
        Inventario, Vencimientos, Registro, Precios, Ordenes, Area);

    private static readonly HashSet<string> ConjuntoListas = new(Listas, StringComparer.Ordinal);

    public static bool EsDeLista(string campo) => ConjuntoListas.Contains(campo);

    private static string[] Unir(params string[][] grupos)
    {
        var unicos = new List<string>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var grupo in grupos)
        {
            foreach (var campo in grupo)
            {
                if (vistos.Add(campo))
                {
                    unicos.Add(campo);
                }
            }
        }

        return unicos.ToArray();
    }
}
