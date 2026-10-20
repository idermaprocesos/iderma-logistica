using System.Text.Json;
using System.Text.Json.Serialization;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public sealed class PersonalMedicoRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public PersonalMedicoDatos Datos { get; private set; } = new();

    public string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "personal-medico.json");

    public void Cargar()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        if (!File.Exists(RutaArchivo))
        {
            Datos = CrearEjemplos();
            CargarMedicacionComunDesdeExcel();
            Guardar();
            return;
        }

        var json = File.ReadAllText(RutaArchivo);
        Datos = JsonSerializer.Deserialize<PersonalMedicoDatos>(json, JsonOptions) ?? CrearEjemplos();
        Datos.MedicacionesComunes ??= [];
        if (Datos.Doctores.Count == 0)
        {
            Datos = CrearEjemplos();
        }

        CargarMedicacionComunDesdeExcel();
        Guardar();
    }

    public void Guardar()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        ArchivoAtomico.EscribirTexto(RutaArchivo, JsonSerializer.Serialize(Datos, JsonOptions));
    }

    public static string? RutaExcelComun()
    {
        var candidatos = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "data", "COMUN.xlsx"),
            Path.Combine(AppContext.BaseDirectory, "COMUN.xlsx"),
            Path.Combine(AppContext.BaseDirectory, "docs", "COMUN.xlsx")
        };
        return candidatos.FirstOrDefault(File.Exists);
    }

    private void CargarMedicacionComunDesdeExcel()
    {
        var ruta = RutaExcelComun();
        if (ruta is null)
        {
            return;
        }

        var filas = ExcelMedicacionComunService.Leer(ruta);
        if (filas.Count == 0)
        {
            return;
        }

        var anteriores = Datos.MedicacionesComunes
            .Select(m => m.Nombre)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var nuevas = filas
            .Select(m => m.Nombre)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!anteriores.SequenceEqual(nuevas, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var lista in Datos.Listas)
            {
                lista.Items.Clear();
            }
        }

        Datos.MedicacionesComunes = filas;
    }

    public Doctor? BuscarDoctor(Guid id) =>
        Datos.Doctores.FirstOrDefault(d => d.Id == id);

    public IReadOnlyList<Doctor> DoctoresOrdenados() =>
        Datos.Doctores.OrderBy(d => d.Nombre).ToList();

    public IReadOnlyList<ListaReceta> ListasDe(Guid doctorId) =>
        Datos.Listas
            .Where(l => l.DoctorId == doctorId)
            .OrderBy(l => l.Nombre)
            .ToList();

    public void GuardarDoctor(Doctor doctor)
    {
        var i = Datos.Doctores.FindIndex(d => d.Id == doctor.Id);
        if (i < 0)
        {
            Datos.Doctores.Add(doctor);
        }
        else
        {
            Datos.Doctores[i] = doctor;
        }

        Guardar();
    }

    public void BorrarDoctor(Guid id)
    {
        Datos.Listas.RemoveAll(l => l.DoctorId == id);
        Datos.Doctores.RemoveAll(d => d.Id == id);
        Guardar();
    }

    public ListaReceta? BuscarLista(Guid id) =>
        Datos.Listas.FirstOrDefault(l => l.Id == id);

    public void GuardarLista(ListaReceta lista)
    {
        lista.FechaActualizacion = DateTimeOffset.Now;
        var i = Datos.Listas.FindIndex(l => l.Id == lista.Id);
        if (i < 0)
        {
            Datos.Listas.Add(lista);
        }
        else
        {
            Datos.Listas[i] = lista;
        }

        Guardar();
    }

    public void BorrarLista(Guid id)
    {
        Datos.Listas.RemoveAll(l => l.Id == id);
        Guardar();
    }

    public IReadOnlyList<MedicacionComun> MedicacionesOrdenadas() =>
        Datos.MedicacionesComunes.OrderBy(m => m.Nombre, StringComparer.CurrentCultureIgnoreCase).ToList();

    public IReadOnlyList<MedicacionComun> BuscarMedicacionComun(string texto, int limite = 12)
    {
        var t = texto.Trim();
        if (t.Length == 0)
        {
            return MedicacionesOrdenadas().Take(limite).ToList();
        }

        return Datos.MedicacionesComunes
            .Where(m => m.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .Take(limite)
            .ToList();
    }

    public MedicacionComun? BuscarMedicacionPorNombre(string nombre) =>
        Datos.MedicacionesComunes.FirstOrDefault(m =>
            string.Equals(m.Nombre, nombre, StringComparison.OrdinalIgnoreCase));

    public int ImportarMedicaciones(IReadOnlyList<MedicacionComun> filas)
    {
        var n = 0;
        foreach (var fila in filas)
        {
            if (string.IsNullOrWhiteSpace(fila.Nombre))
            {
                continue;
            }

            var i = Datos.MedicacionesComunes.FindIndex(m =>
                string.Equals(m.Nombre, fila.Nombre, StringComparison.OrdinalIgnoreCase));
            if (i < 0)
            {
                Datos.MedicacionesComunes.Add(fila);
            }
            else
            {
                fila.Id = Datos.MedicacionesComunes[i].Id;
                Datos.MedicacionesComunes[i] = fila;
            }

            n++;
        }

        Guardar();
        return n;
    }

    public RecetaEmitida RegistrarEmision(Doctor doctor, ListaReceta lista, RecetaEmitida receta)
    {
        receta.DoctorId = doctor.Id;
        receta.ListaId = lista.Id;
        receta.Fecha = DateTimeOffset.Now;
        receta.Numero = $"REC-{DateTime.Now:yyyyMMdd}-{Datos.Recetas.Count + 1:0000}";
        receta.Items = lista.Items.Select(ClonarItem).ToList();
        if (string.IsNullOrWhiteSpace(receta.Diagnostico))
        {
            receta.Diagnostico = lista.Diagnostico;
        }

        if (string.IsNullOrWhiteSpace(receta.Indicaciones))
        {
            receta.Indicaciones = lista.IndicacionesGenerales;
        }

        Datos.Recetas.Add(receta);
        Guardar();
        return receta;
    }

    public IReadOnlyList<RatioMedicamento> RatiosDe(Guid doctorId)
    {
        var nombres = Datos.Listas
            .Where(l => l.DoctorId == doctorId)
            .SelectMany(l => l.Items)
            .Select(i => Clave(i.Nombre))
            .Where(n => n.Length > 0)
            .ToList();

        var total = nombres.Count;
        var grupos = nombres
            .GroupBy(n => n)
            .Select(g => new { Nombre = g.Key, Veces = g.Count() })
            .OrderByDescending(g => g.Veces)
            .ThenBy(g => g.Nombre)
            .ToList();

        var fichas = App.Instance.Repositorio.Todas;
        return grupos.Select(g =>
        {
            var ficha = fichas.FirstOrDefault(f =>
                Clave(f.Nombre) == g.Nombre || Clave(f.Codigo) == g.Nombre);
            return new RatioMedicamento
            {
                Nombre = g.Nombre,
                Veces = g.Veces,
                Total = total,
                Porcentaje = total == 0 ? 0 : g.Veces * 100.0 / total,
                Existencia = ficha?.Existencia ?? 0,
                StockMinimo = ficha?.StockMinimo ?? 0,
                Unidad = ficha?.UnidadMedida ?? ""
            };
        }).ToList();
    }

    public RatioMedicamento RatioDeItem(Guid doctorId, ItemListaReceta item)
    {
        var clave = Clave(item.Nombre);
        var ratios = RatiosDe(doctorId);
        var hallado = ratios.FirstOrDefault(r => r.Nombre == clave);
        if (hallado is not null)
        {
            return hallado;
        }

        var ficha = item.FichaId is Guid id
            ? App.Instance.Repositorio.Obtener(id)
            : App.Instance.Repositorio.Todas.FirstOrDefault(f =>
                string.Equals(f.Codigo, item.Codigo, StringComparison.OrdinalIgnoreCase));

        return new RatioMedicamento
        {
            Nombre = item.Nombre,
            Existencia = ficha?.Existencia ?? 0,
            StockMinimo = ficha?.StockMinimo ?? 0,
            Unidad = ficha?.UnidadMedida ?? item.Unidad
        };
    }

    public List<ItemRecetaVista> VistasDe(ListaReceta lista)
    {
        return lista.Items.Select(item =>
        {
            var ratio = RatioDeItem(lista.DoctorId, item);
            var lineas = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.PosologiaTexto))
            {
                lineas.Add(item.PosologiaTexto);
            }

            if (!string.IsNullOrWhiteSpace(item.Indicaciones))
            {
                lineas.Add("Uso: " + item.Indicaciones.Trim());
            }

            if (!string.IsNullOrWhiteSpace(item.DespachoTexto))
            {
                lineas.Add("Despachar: " + item.DespachoTexto);
            }

            if (!string.IsNullOrWhiteSpace(ratio.InventarioTexto)
                && !string.Equals(ratio.InventarioTexto, "Sin ficha Iderma", StringComparison.Ordinal))
            {
                lineas.Add(ratio.InventarioTexto);
            }

            return new ItemRecetaVista
            {
                Id = item.Id,
                Codigo = string.IsNullOrWhiteSpace(item.Codigo) ? "—" : item.Codigo,
                Nombre = item.Nombre,
                Detalle = string.Join(Environment.NewLine, lineas),
                RatioUso = ratio.RatioTexto
            };
        }).ToList();
    }

    private static ItemListaReceta ClonarItem(ItemListaReceta item) => new()
    {
        Id = Guid.NewGuid(),
        FichaId = item.FichaId,
        Codigo = item.Codigo,
        Nombre = item.Nombre,
        Dosis = item.Dosis,
        DosisUnidad = item.DosisUnidad,
        ViaAdministracion = item.ViaAdministracion,
        Frecuencia = item.Frecuencia,
        Horario = item.Horario,
        Duracion = item.Duracion,
        Cantidad = item.Cantidad,
        Unidad = item.Unidad,
        Indicaciones = item.Indicaciones
    };

    private static string Clave(string texto) =>
        texto.Trim().ToLowerInvariant();

    private static PersonalMedicoDatos CrearEjemplos()
    {
        var camila = new Doctor
        {
            Nombre = "Dra. Camila Herrera Vásquez",
            Colegiatura = "65421",
            Rne = "RNE 32145",
            Especialidad = "Dermatología",
            Telefono = "01-445-1201",
            Correo = "camila.herrera@iderma.pe"
        };
        var andres = new Doctor
        {
            Nombre = "Dr. Andrés Paredes Núñez",
            Colegiatura = "58219",
            Rne = "RNE 29870",
            Especialidad = "Cirugía de cabeza y cuello",
            Telefono = "01-445-1202",
            Correo = "andres.paredes@iderma.pe"
        };
        var valeria = new Doctor
        {
            Nombre = "Dra. Valeria Chávez Rivas",
            Colegiatura = "70134",
            Rne = "RNE 35602",
            Especialidad = "Medicina capilar",
            Telefono = "01-445-1203",
            Correo = "valeria.chavez@iderma.pe"
        };
        var martin = new Doctor
        {
            Nombre = "Dr. Martín Salazar Quispe",
            Colegiatura = "49876",
            Rne = "RNE 27411",
            Especialidad = "Dermatología",
            Telefono = "01-445-1204",
            Correo = "martin.salazar@iderma.pe"
        };
        var lucia = new Doctor
        {
            Nombre = "Dra. Lucía Mendoza Arce",
            Colegiatura = "62308",
            Rne = "RNE 31088",
            Especialidad = "Medicina estética",
            Telefono = "01-445-1205",
            Correo = "lucia.mendoza@iderma.pe"
        };
        var diego = new Doctor
        {
            Nombre = "Dr. Diego Fuentes Alvarado",
            Colegiatura = "55790",
            Rne = "RNE 28754",
            Especialidad = "Cirugía plástica",
            Telefono = "01-445-1206",
            Correo = "diego.fuentes@iderma.pe"
        };
        var elena = new Doctor
        {
            Nombre = "Dra. Elena Rojas Palomino",
            Colegiatura = "68912",
            Rne = "RNE 34019",
            Especialidad = "Tricología",
            Telefono = "01-445-1207",
            Correo = "elena.rojas@iderma.pe"
        };

        var listas = new List<ListaReceta>
        {
            new()
            {
                DoctorId = camila.Id,
                Nombre = "Alopecia androgenética — esquema inicial",
                Diagnostico = "Alopecia androgenética femenina, estadio Ludwig I-II",
                IndicacionesGenerales = "Control en 12 semanas. No suspender minoxidil de forma abrupta.",
                Items = []
            },
            new()
            {
                DoctorId = andres.Id,
                Nombre = "Post operatorio injerto capilar — 7 días",
                Diagnostico = "Post operatorio de trasplante capilar FUE",
                IndicacionesGenerales = "No rascar zona donante. Dormir semisentado las primeras 72 h.",
                Items = []
            },
            new()
            {
                DoctorId = valeria.Id,
                Nombre = "Efluvio telógeno — soporte",
                Diagnostico = "Efluvio telógeno agudo",
                IndicacionesGenerales = "Revisar ferritina y TSH en el siguiente control.",
                Items = []
            },
            new()
            {
                DoctorId = elena.Id,
                Nombre = "Mantenimiento tricológico",
                Diagnostico = "Mantenimiento post tratamiento capilar",
                IndicacionesGenerales = "Continuar el esquema salvo irritación del cuero cabelludo.",
                Items = []
            }
        };

        return new PersonalMedicoDatos
        {
            Doctores = [camila, andres, valeria, martin, lucia, diego, elena],
            Listas = listas
        };
    }
}
