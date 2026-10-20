using System.Text.Json.Serialization;

namespace IdermaFichas.Models;

public sealed class Doctor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = "";
    public string Colegiatura { get; set; } = "";
    public string Rne { get; set; } = "";
    public string Especialidad { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Correo { get; set; } = "";
    public bool Activo { get; set; } = true;

    [JsonIgnore]
    public string Resumen => string.Join(" · ", new[]
    {
        Especialidad,
        string.IsNullOrWhiteSpace(Colegiatura) ? "" : $"CMP {Colegiatura}"
    }.Where(s => !string.IsNullOrWhiteSpace(s)));

    [JsonIgnore]
    public string LineaDesplegable =>
        string.IsNullOrWhiteSpace(Resumen) ? Nombre : $"{Nombre}  ·  {Resumen}";

    [JsonIgnore]
    public string EstadoTexto => Activo ? "Activo" : "Inactivo";
}

public sealed class ItemListaReceta
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? FichaId { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Dosis { get; set; } = "";
    public string DosisUnidad { get; set; } = "";
    public string ViaAdministracion { get; set; } = "";
    public string Frecuencia { get; set; } = "";
    public string Horario { get; set; } = "";
    public string Duracion { get; set; } = "";
    public string Cantidad { get; set; } = "";
    public string Unidad { get; set; } = "";
    public string Indicaciones { get; set; } = "";

    [JsonIgnore]
    public string DosisTexto
    {
        get
        {
            var dosis = string.Join(" ", new[] { Dosis, DosisUnidad }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return string.IsNullOrWhiteSpace(dosis) ? "" : dosis;
        }
    }

    [JsonIgnore]
    public string DespachoTexto =>
        string.Join(" ", new[] { Cantidad, Unidad }.Where(s => !string.IsNullOrWhiteSpace(s)));

    [JsonIgnore]
    public string PosologiaTexto
    {
        get
        {
            var partes = new List<string>();
            if (!string.IsNullOrWhiteSpace(DosisTexto))
            {
                partes.Add($"Dosis: {DosisTexto}");
            }

            if (!string.IsNullOrWhiteSpace(ViaAdministracion))
            {
                partes.Add($"Vía: {ViaAdministracion}");
            }

            if (!string.IsNullOrWhiteSpace(Frecuencia))
            {
                partes.Add(Frecuencia);
            }

            if (!string.IsNullOrWhiteSpace(Horario))
            {
                partes.Add(Horario);
            }

            if (!string.IsNullOrWhiteSpace(Duracion))
            {
                partes.Add($"Durante {Duracion}");
            }

            return string.Join(" · ", partes);
        }
    }
}

public sealed class ListaReceta
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DoctorId { get; set; }
    public string Nombre { get; set; } = "";
    public string Diagnostico { get; set; } = "";
    public string IndicacionesGenerales { get; set; } = "";
    public DateTimeOffset FechaActualizacion { get; set; } = DateTimeOffset.Now;
    public List<ItemListaReceta> Items { get; set; } = [];

    [JsonIgnore]
    public string Resumen => Items.Count == 1
        ? "1 medicamento"
        : $"{Items.Count} medicamentos";

    [JsonIgnore]
    public string FechaTexto => FechaActualizacion.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}

public sealed class RecetaEmitida
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DoctorId { get; set; }
    public Guid ListaId { get; set; }
    public string Numero { get; set; } = "";
    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Now;
    public string PacienteNombre { get; set; } = "";
    public string PacienteDocumento { get; set; } = "";
    public string PacienteEdad { get; set; } = "";
    public string PacienteSexo { get; set; } = "";
    public string HistoriaClinica { get; set; } = "";
    public string Diagnostico { get; set; } = "";
    public string Indicaciones { get; set; } = "";
    public List<ItemListaReceta> Items { get; set; } = [];
    public string SmartHealthLink { get; set; } = "";
    public string SmartHealthCard { get; set; } = "";
}

public sealed class PersonalMedicoDatos
{
    public List<Doctor> Doctores { get; set; } = [];
    public List<ListaReceta> Listas { get; set; } = [];
    public List<RecetaEmitida> Recetas { get; set; } = [];
    public List<MedicacionComun> MedicacionesComunes { get; set; } = [];
}

public sealed class MedicacionComun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = "";
    public string Dosis { get; set; } = "";
    public string DosisUnidad { get; set; } = "";
    public string ViaAdministracion { get; set; } = "";
    public string Frecuencia { get; set; } = "";
    public string Horario { get; set; } = "";
    public string Duracion { get; set; } = "";
    public string Cantidad { get; set; } = "";
    public string Unidad { get; set; } = "";
    public string Indicaciones { get; set; } = "";

    [JsonIgnore]
    public string Resumen => string.Join(" · ", new[]
    {
        Nombre,
        string.Join(" ", new[] { Dosis, DosisUnidad }.Where(s => !string.IsNullOrWhiteSpace(s)))
    }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public sealed class ItemRecetaVista
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Detalle { get; set; } = "";
    public string RatioUso { get; set; } = "";
    public string IdTexto => Id.ToString("N");
}

public sealed class RatioMedicamento
{
    public string Nombre { get; set; } = "";
    public int Veces { get; set; }
    public int Total { get; set; }
    public double Porcentaje { get; set; }
    public double Existencia { get; set; }
    public double StockMinimo { get; set; }
    public string Unidad { get; set; } = "";

    public string RatioTexto => Total == 0
        ? "Sin historial"
        : $"{Porcentaje:0.#} % · {Veces} de {Total}";

    public string InventarioTexto
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Unidad) && Existencia <= 0 && StockMinimo <= 0)
            {
                return "Sin ficha Iderma";
            }

            var stock = StockMinimo > 0
                ? $" · mínimo {StockMinimo:0.##}"
                : "";
            return $"Existencia {Existencia:0.##} {Unidad}{stock}";
        }
    }
}
