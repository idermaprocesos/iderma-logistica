using System.Text.Json;
using System.Text.Json.Serialization;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public sealed class RhRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public RhDatos Datos { get; private set; } = new();

    public string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "recursos-humanos.json");

    public void Cargar()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        if (!File.Exists(RutaArchivo))
        {
            Datos = CrearEjemplos();
            Guardar();
            return;
        }

        var json = File.ReadAllText(RutaArchivo);
        Datos = JsonSerializer.Deserialize<RhDatos>(json, JsonOptions) ?? CrearEjemplos();
        if (CompletarHorarios())
        {
            Guardar();
        }
    }

    public void Guardar()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        ArchivoAtomico.EscribirTexto(RutaArchivo, JsonSerializer.Serialize(Datos, JsonOptions));
    }

    public IEnumerable<Colaborador> ColaboradoresActivos() =>
        Datos.Colaboradores.Where(c => c.Activo).OrderBy(c => c.Nombre);

    public Colaborador? BuscarColaborador(Guid id) =>
        Datos.Colaboradores.FirstOrDefault(c => c.Id == id);

    public string NombreColaborador(Guid id) =>
        BuscarColaborador(id)?.Nombre ?? "Colaborador no encontrado";

    public HorarioColaborador HorarioDe(Guid colaboradorId)
    {
        var horario = Datos.Horarios.FirstOrDefault(h => h.ColaboradorId == colaboradorId);
        if (horario is not null)
        {
            return horario;
        }

        horario = new HorarioColaborador { ColaboradorId = colaboradorId };
        Datos.Horarios.Add(horario);
        Guardar();
        return horario;
    }

    public int TardanzasDelMes(Guid colaboradorId)
    {
        var inicio = new DateTimeOffset(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0, DateTimeOffset.Now.Offset);
        return Datos.Tardanzas.Count(t => t.ColaboradorId == colaboradorId && t.Fecha >= inicio);
    }

    public int FaltasDelMes(Guid colaboradorId, bool? justificada = null)
    {
        var inicio = new DateTimeOffset(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0, DateTimeOffset.Now.Offset);
        return Datos.Faltas.Count(f =>
            f.ColaboradorId == colaboradorId
            && f.Fecha >= inicio
            && (justificada is null || f.Justificada == justificada));
    }

    public NivelRiesgo? RiesgoActual(Guid colaboradorId) =>
        Datos.Riesgos
            .Where(r => r.ColaboradorId == colaboradorId)
            .OrderByDescending(r => r.Fecha)
            .FirstOrDefault()?.Nivel;

    public void GuardarColaborador(Colaborador colaborador)
    {
        var i = Datos.Colaboradores.FindIndex(c => c.Id == colaborador.Id);
        if (i < 0)
        {
            Datos.Colaboradores.Add(colaborador);
            Datos.Horarios.Add(new HorarioColaborador { ColaboradorId = colaborador.Id });
        }
        else
        {
            Datos.Colaboradores[i] = colaborador;
        }

        Guardar();
    }

    public void BorrarColaborador(Guid id)
    {
        Datos.Colaboradores.RemoveAll(c => c.Id == id);
        Datos.Horarios.RemoveAll(h => h.ColaboradorId == id);
        Datos.Tardanzas.RemoveAll(t => t.ColaboradorId == id);
        Datos.Faltas.RemoveAll(f => f.ColaboradorId == id);
        Datos.Riesgos.RemoveAll(r => r.ColaboradorId == id);
        Guardar();
    }

    public void GuardarTardanza(Tardanza item)
    {
        Upsert(Datos.Tardanzas, item, t => t.Id);
        Guardar();
    }

    public void BorrarTardanza(Guid id)
    {
        Datos.Tardanzas.RemoveAll(t => t.Id == id);
        Guardar();
    }

    public void GuardarFalta(Falta item)
    {
        Upsert(Datos.Faltas, item, f => f.Id);
        Guardar();
    }

    public void BorrarFalta(Guid id)
    {
        Datos.Faltas.RemoveAll(f => f.Id == id);
        Guardar();
    }

    public void GuardarRiesgo(RiesgoColaborador item)
    {
        Upsert(Datos.Riesgos, item, r => r.Id);
        Guardar();
    }

    public void BorrarRiesgo(Guid id)
    {
        Datos.Riesgos.RemoveAll(r => r.Id == id);
        Guardar();
    }

    private bool CompletarHorarios()
    {
        var agregado = false;
        foreach (var colaborador in Datos.Colaboradores)
        {
            if (Datos.Horarios.All(h => h.ColaboradorId != colaborador.Id))
            {
                Datos.Horarios.Add(new HorarioColaborador { ColaboradorId = colaborador.Id });
                agregado = true;
            }
        }

        return agregado;
    }

    private static void Upsert<T>(List<T> lista, T item, Func<T, Guid> id)
    {
        var i = lista.FindIndex(x => id(x) == id(item));
        if (i < 0)
        {
            lista.Add(item);
        }
        else
        {
            lista[i] = item;
        }
    }

    private static RhDatos CrearEjemplos()
    {
        var ana = new Colaborador
        {
            Nombre = "Ana Martínez",
            Puesto = "Coordinadora de clínica",
            Area = "Clínica",
            Telefono = "555-0101",
            FechaIngreso = DateTimeOffset.Now.Date.AddYears(-3)
        };
        var luis = new Colaborador
        {
            Nombre = "Luis Ortega",
            Puesto = "Asistente de recepción",
            Area = "Oficina",
            Telefono = "555-0102",
            FechaIngreso = DateTimeOffset.Now.Date.AddMonths(-11)
        };
        var maria = new Colaborador
        {
            Nombre = "María López",
            Puesto = "Auxiliar de limpieza",
            Area = "Limpieza",
            Telefono = "555-0103",
            FechaIngreso = DateTimeOffset.Now.Date.AddMonths(-6)
        };

        return new RhDatos
        {
            Colaboradores = [ana, luis, maria],
            Horarios =
            [
                new HorarioColaborador { ColaboradorId = ana.Id },
                new HorarioColaborador { ColaboradorId = luis.Id },
                new HorarioColaborador
                {
                    ColaboradorId = maria.Id,
                    Dias =
                    [
                        new() { Dia = DayOfWeek.Monday, Entrada = new(7, 0, 0), Salida = new(15, 0, 0) },
                        new() { Dia = DayOfWeek.Tuesday, Entrada = new(7, 0, 0), Salida = new(15, 0, 0) },
                        new() { Dia = DayOfWeek.Wednesday, Entrada = new(7, 0, 0), Salida = new(15, 0, 0) },
                        new() { Dia = DayOfWeek.Thursday, Entrada = new(7, 0, 0), Salida = new(15, 0, 0) },
                        new() { Dia = DayOfWeek.Friday, Entrada = new(7, 0, 0), Salida = new(15, 0, 0) },
                        new() { Dia = DayOfWeek.Saturday, Entrada = new(8, 0, 0), Salida = new(12, 0, 0) },
                        new() { Dia = DayOfWeek.Sunday, Laborable = false }
                    ]
                }
            ],
            Tardanzas =
            [
                new Tardanza
                {
                    ColaboradorId = luis.Id,
                    Fecha = DateTimeOffset.Now.Date.AddDays(-4),
                    Minutos = 18,
                    Motivo = "Tráfico"
                }
            ],
            Faltas =
            [
                new Falta
                {
                    ColaboradorId = maria.Id,
                    Fecha = DateTimeOffset.Now.Date.AddDays(-12),
                    Justificada = true,
                    Motivo = "Cita médica",
                    Documento = "Constancia clínica"
                },
                new Falta
                {
                    ColaboradorId = luis.Id,
                    Fecha = DateTimeOffset.Now.Date.AddDays(-20),
                    Justificada = false,
                    Motivo = "No se presentó"
                }
            ],
            Riesgos =
            [
                new RiesgoColaborador
                {
                    ColaboradorId = luis.Id,
                    Nivel = NivelRiesgo.Medio,
                    Fecha = DateTimeOffset.Now.Date.AddDays(-2),
                    Motivo = "Acumulación de tardanzas en el mes",
                    Acciones = "Acompañamiento y seguimiento semanal"
                }
            ]
        };
    }
}
