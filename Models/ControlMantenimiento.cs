namespace IdermaFichas.Models;

public enum ClaseControlEquipo
{
    Preventivo,
    Correctivo,
    Critico
}

public sealed class RangosControlEquipo
{
    public int PreventivoDias { get; set; } = 90;
    public int AvisoPreventivoDias { get; set; } = 14;
    public int CorrectivoDias { get; set; } = 30;
    public int AvisoCorrectivoDias { get; set; } = 7;
    public int CriticoDias { get; set; } = 7;
    public int AvisoCriticoDias { get; set; } = 2;

    public int Intervalo(ClaseControlEquipo clase) => clase switch
    {
        ClaseControlEquipo.Correctivo => CorrectivoDias,
        ClaseControlEquipo.Critico => CriticoDias,
        _ => PreventivoDias
    };

    public int Aviso(ClaseControlEquipo clase) => clase switch
    {
        ClaseControlEquipo.Correctivo => AvisoCorrectivoDias,
        ClaseControlEquipo.Critico => AvisoCriticoDias,
        _ => AvisoPreventivoDias
    };

    public void Normalizar()
    {
        PreventivoDias = Ajustar(PreventivoDias);
        CorrectivoDias = Ajustar(CorrectivoDias);
        CriticoDias = Ajustar(CriticoDias);
        AvisoPreventivoDias = AjustarAviso(AvisoPreventivoDias, PreventivoDias);
        AvisoCorrectivoDias = AjustarAviso(AvisoCorrectivoDias, CorrectivoDias);
        AvisoCriticoDias = AjustarAviso(AvisoCriticoDias, CriticoDias);
    }

    private static int Ajustar(int dias) => Math.Clamp(dias, 1, 3650);

    private static int AjustarAviso(int aviso, int intervalo) => Math.Clamp(aviso, 0, intervalo);
}

public sealed class EquipoControl
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Ubicacion { get; set; } = string.Empty;
    public string Responsable { get; set; } = string.Empty;
    public ClaseControlEquipo Clase { get; set; }
    public int IntervaloDias { get; set; }
    public int AvisoDias { get; set; }
    public DateTimeOffset? UltimoControl { get; set; }
    public string Nota { get; set; } = string.Empty;

    public void NormalizarPlazo(RangosControlEquipo? heredados = null)
    {
        if (IntervaloDias <= 0)
        {
            var baseRangos = heredados ?? new RangosControlEquipo();
            IntervaloDias = baseRangos.Intervalo(Clase);
            if (AvisoDias <= 0)
            {
                AvisoDias = baseRangos.Aviso(Clase);
            }
        }

        IntervaloDias = Math.Clamp(IntervaloDias, 1, 3650);
        AvisoDias = Math.Clamp(AvisoDias, 0, IntervaloDias);
    }
}

public static class ControlMantenimientoCalculo
{
    public static string TextoClase(ClaseControlEquipo clase) => clase switch
    {
        ClaseControlEquipo.Correctivo => "Correctivo",
        ClaseControlEquipo.Critico => "Crítico",
        _ => "Preventivo"
    };

    public static DateTime? Proximo(EquipoControl equipo)
    {
        if (equipo.UltimoControl is not DateTimeOffset ultimo)
        {
            return null;
        }

        return ultimo.LocalDateTime.Date.AddDays(Math.Max(1, equipo.IntervaloDias));
    }

    public static string Estado(EquipoControl equipo)
    {
        var proximo = Proximo(equipo);
        if (proximo is null)
        {
            return "Sin control";
        }

        var dias = (proximo.Value.Date - DateTime.Today).Days;
        if (dias < 0)
        {
            return "Vencido";
        }

        if (dias <= Math.Max(0, equipo.AvisoDias))
        {
            return "Por vencer";
        }

        return "Al día";
    }
}
