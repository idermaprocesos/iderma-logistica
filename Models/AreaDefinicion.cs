using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdermaFichas.Models;

public sealed class AreaDefinicion
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Prefijo { get; set; } = "GEN";
    public string Glifo { get; set; } = "\uE8F1";
    public AreaOperativa Plantilla { get; set; } = AreaOperativa.Clinica;
    public int Orden { get; set; }
}

public sealed class AreaIdJsonConverter : JsonConverter<Guid?>
{
    public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numero))
        {
            return AreasOperativas.DesdeEnumAntiguo(numero);
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("El área de la ficha no tiene un formato reconocido.");
        }

        var texto = reader.GetString();
        if (string.IsNullOrWhiteSpace(texto) ||
            texto.Equals("ninguna", StringComparison.OrdinalIgnoreCase) ||
            texto.Equals("sinarea", StringComparison.OrdinalIgnoreCase) ||
            texto.Equals("sin área", StringComparison.OrdinalIgnoreCase) ||
            texto.Equals("sin area", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Guid.TryParse(texto, out var id))
        {
            return id;
        }

        return AreasOperativas.ResolverId(texto);
    }

    public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToString("D"));
    }
}
