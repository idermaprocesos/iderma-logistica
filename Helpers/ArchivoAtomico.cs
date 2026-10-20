namespace IdermaFichas.Helpers;

public static class ArchivoAtomico
{
    public static void EscribirTexto(string ruta, string contenido)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        var temporal = ruta + ".tmp";
        File.WriteAllText(temporal, contenido);
        File.Move(temporal, ruta, overwrite: true);
    }

    public static string? AislarDañado(string ruta)
    {
        if (!File.Exists(ruta))
        {
            return null;
        }

        var destino = ruta + ".danado-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Move(ruta, destino);
        return destino;
    }
}
