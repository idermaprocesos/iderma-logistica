using IdermaFichas.Models;

namespace IdermaFichas.Services;

internal static class EjemplosControlMantenimiento
{
    public static RangosControlEquipo Rangos() => new()
    {
        PreventivoDias = 90,
        AvisoPreventivoDias = 14,
        CorrectivoDias = 30,
        AvisoCorrectivoDias = 7,
        CriticoDias = 7,
        AvisoCriticoDias = 2
    };

    public static List<EquipoControl> Equipos()
    {
        var hoy = DateTime.Today;
        return
        [
            Equipo("c1a10001-0000-4000-8000-000000000001", "EQ-001", "Unidad de extracción FUE", "Sala FUE 1", "Enfermería quirúrgica", ClaseControlEquipo.Preventivo, 180, 14, hoy.AddDays(-200), "Motor y pedal. El preventivo semestral ya venció."),
            Equipo("c1a10001-0000-4000-8000-000000000006", "EQ-006", "Autoclave clase B 18 L", "Central de esterilización", "Esterilización", ClaseControlEquipo.Preventivo, 90, 14, hoy.AddDays(-80), "Prueba biológica del trimestre en la ventana de aviso."),
            Equipo("c1a10001-0000-4000-8000-000000000003", "EQ-003", "Implanters Choi", "Sala DHI", "Enfermería quirúrgica", ClaseControlEquipo.Preventivo, 30, 7, hoy.AddDays(-20), "Revisión del muelle después de las jornadas de implantación."),
            Equipo("c1a10001-0000-4000-8000-000000000013", "EQ-013", "Monitor multiparamétrico", "Quirófano", "Quirófano", ClaseControlEquipo.Preventivo, 365, 30, hoy.AddDays(-40), "Constantes durante extracción e implantación."),
            Equipo("c1a10001-0000-4000-8000-000000000009", "EQ-009", "Centrífuga de PRP", "Laboratorio PRP", "Laboratorio", ClaseControlEquipo.Correctivo, 30, 7, hoy.AddDays(-45), "Retirada de uso clínico por desviación de velocidad."),
            Equipo("c1a10001-0000-4000-8000-000000000015", "EQ-015", "Equipo de mesoterapia", "Consultorio 1", "Consulta", ClaseControlEquipo.Correctivo, 30, 5, hoy.AddDays(-4), "Fuga en la pieza de mano. Control reciente, sigue en seguimiento."),
            Equipo("c1a10001-0000-4000-8000-000000000010", "EQ-010", "Nevera de conservación de folículos", "Sala de injertos", "Laboratorio de injertos", ClaseControlEquipo.Critico, 7, 2, hoy.AddDays(-10), "Cadena de frío 2–8 °C. El control semanal está vencido."),
            Equipo("c1a10001-0000-4000-8000-000000000011", "EQ-011", "Termómetro registrador de cadena de frío", "Nevera de folículos", "Laboratorio de injertos", ClaseControlEquipo.Critico, 7, 1, hoy.AddDays(-1), "Registro continuo de la nevera de folículos.")
        ];
    }

    private static EquipoControl Equipo(
        string id,
        string codigo,
        string nombre,
        string ubicacion,
        string responsable,
        ClaseControlEquipo clase,
        int intervalo,
        int aviso,
        DateTime ultimo,
        string nota) => new()
    {
        Id = Guid.Parse(id),
        Codigo = codigo,
        Nombre = nombre,
        Ubicacion = ubicacion,
        Responsable = responsable,
        Clase = clase,
        IntervaloDias = intervalo,
        AvisoDias = aviso,
        UltimoControl = new DateTimeOffset(ultimo),
        Nota = nota
    };
}
