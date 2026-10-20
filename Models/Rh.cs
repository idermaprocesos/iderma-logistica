namespace IdermaFichas.Models;

public enum NivelRiesgo
{
    Bajo,
    Medio,
    Alto
}

public sealed class Colaborador
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = "";
    public string Puesto { get; set; } = "";
    public string Area { get; set; } = "";
    public string Telefono { get; set; } = "";
    public DateTimeOffset FechaIngreso { get; set; } = DateTimeOffset.Now.Date;
    public bool Activo { get; set; } = true;
    public string Notas { get; set; } = "";

    public string Resumen => string.Join(" · ", new[] { Puesto, Area }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public string EstadoTexto => Activo ? "Activo" : "Inactivo";
    public string FechaIngresoTexto => FechaIngreso.ToString("dd/MM/yyyy");
}

public sealed class HorarioDia
{
    public DayOfWeek Dia { get; set; }
    public bool Laborable { get; set; } = true;
    public TimeSpan Entrada { get; set; } = new(8, 0, 0);
    public TimeSpan Salida { get; set; } = new(17, 0, 0);

    public string DiaTexto => Dia switch
    {
        DayOfWeek.Monday => "Lunes",
        DayOfWeek.Tuesday => "Martes",
        DayOfWeek.Wednesday => "Miércoles",
        DayOfWeek.Thursday => "Jueves",
        DayOfWeek.Friday => "Viernes",
        DayOfWeek.Saturday => "Sábado",
        DayOfWeek.Sunday => "Domingo",
        _ => Dia.ToString()
    };

    public string HorarioTexto => Laborable
        ? $"{Entrada:hh\\:mm} – {Salida:hh\\:mm}"
        : "Descanso";
}

public sealed class HorarioColaborador
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ColaboradorId { get; set; }
    public List<HorarioDia> Dias { get; set; } = CrearSemana();

    public static List<HorarioDia> CrearSemana() =>
    [
        new() { Dia = DayOfWeek.Monday },
        new() { Dia = DayOfWeek.Tuesday },
        new() { Dia = DayOfWeek.Wednesday },
        new() { Dia = DayOfWeek.Thursday },
        new() { Dia = DayOfWeek.Friday },
        new() { Dia = DayOfWeek.Saturday, Laborable = false },
        new() { Dia = DayOfWeek.Sunday, Laborable = false }
    ];
}

public sealed class Tardanza
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ColaboradorId { get; set; }
    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Now.Date;
    public int Minutos { get; set; }
    public string Motivo { get; set; } = "";

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string MinutosTexto => Minutos == 1 ? "1 minuto" : $"{Minutos} minutos";
}

public sealed class Falta
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ColaboradorId { get; set; }
    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Now.Date;
    public bool Justificada { get; set; }
    public string Motivo { get; set; } = "";
    public string Documento { get; set; } = "";

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string TipoTexto => Justificada ? "Falta justificada" : "Falta";
}

public sealed class RiesgoColaborador
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ColaboradorId { get; set; }
    public NivelRiesgo Nivel { get; set; } = NivelRiesgo.Medio;
    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Now.Date;
    public string Motivo { get; set; } = "";
    public string Acciones { get; set; } = "";

    public string FechaTexto => Fecha.ToString("dd/MM/yyyy");
    public string NivelTexto => Nivel switch
    {
        NivelRiesgo.Bajo => "Bajo",
        NivelRiesgo.Alto => "Alto",
        _ => "Medio"
    };
}

public sealed class RhDatos
{
    public List<Colaborador> Colaboradores { get; set; } = [];
    public List<HorarioColaborador> Horarios { get; set; } = [];
    public List<Tardanza> Tardanzas { get; set; } = [];
    public List<Falta> Faltas { get; set; } = [];
    public List<RiesgoColaborador> Riesgos { get; set; } = [];
}
