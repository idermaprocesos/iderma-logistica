using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;

namespace IdermaFichas.Services;

internal static class BasesFirebase
{
    public const string Proyecto = "iderma-db-2026";
    public const string Vencimientos = "vencimientos";
    public const string Registro = "registro";
    public const string Precios = "historial-precios";
    public const string Ordenes = "ordenes-compra";
    public const string Proveedores = "proveedores";
    public const string Merma = "merma";
    public const string Mantenimiento = "mantenimiento";

    public static readonly string[] Ids = [Vencimientos, Registro, Precios, Ordenes, Proveedores, Merma, Mantenimiento];

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(40) };
    private static string? _ubicacion;

    public static async Task<bool> AsegurarUnaAsync(GoogleCredential credencial, string id, Action<string> reportar)
    {
        try
        {
            if (await ExisteAsync(credencial, id))
            {
                return true;
            }

            reportar("Creando la base " + id + "…");
            await CrearAsync(credencial, id, await UbicacionInventarioAsync(credencial));
            return true;
        }
        catch (Exception ex)
        {
            var permiso = ex.Message.Contains("PERMISSION_DENIED", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("permission", StringComparison.OrdinalIgnoreCase);
            reportar(permiso ? id + ": sin permiso para crearla" : id + ": " + Resumen(ex.Message));
            return false;
        }
    }

    private static async Task<string> UbicacionInventarioAsync(GoogleCredential credencial)
    {
        if (_ubicacion is not null)
        {
            return _ubicacion;
        }

        var (estado, cuerpo) = await EnviarAsync(
            credencial,
            HttpMethod.Get,
            $"https://firestore.googleapis.com/v1/projects/{Proyecto}/databases/(default)",
            null);
        if (estado is < 200 or >= 300)
        {
            throw new InvalidOperationException("No se pudo leer la base del inventario. " + Resumen(cuerpo));
        }

        using var doc = JsonDocument.Parse(cuerpo);
        if (!doc.RootElement.TryGetProperty("locationId", out var ubicacion) || ubicacion.GetString() is not string texto || texto.Length == 0)
        {
            throw new InvalidOperationException("La base del inventario no indica su ubicación.");
        }

        _ubicacion = texto;
        return texto;
    }

    private static async Task<bool> ExisteAsync(GoogleCredential credencial, string id)
    {
        var (estado, cuerpo) = await EnviarAsync(
            credencial,
            HttpMethod.Get,
            $"https://firestore.googleapis.com/v1/projects/{Proyecto}/databases/{id}",
            null);
        if (estado == 200)
        {
            return true;
        }

        if (estado == 404)
        {
            return false;
        }

        throw new InvalidOperationException(Resumen(cuerpo));
    }

    private static async Task CrearAsync(GoogleCredential credencial, string id, string ubicacion)
    {
        var cuerpo = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "FIRESTORE_NATIVE",
            ["locationId"] = ubicacion,
            ["deleteProtectionState"] = "DELETE_PROTECTION_DISABLED",
            ["pointInTimeRecoveryEnablement"] = "POINT_IN_TIME_RECOVERY_DISABLED"
        });
        var (estado, respuesta) = await EnviarAsync(
            credencial,
            HttpMethod.Post,
            $"https://firestore.googleapis.com/v1/projects/{Proyecto}/databases?databaseId={id}",
            cuerpo);
        if (estado == 409)
        {
            return;
        }

        if (estado is < 200 or >= 300)
        {
            throw new InvalidOperationException(Resumen(respuesta));
        }

        using var doc = JsonDocument.Parse(respuesta);
        if (doc.RootElement.TryGetProperty("error", out var error))
        {
            var mensaje = error.TryGetProperty("message", out var texto) ? texto.GetString() : respuesta;
            if (mensaje?.Contains("already exists", StringComparison.OrdinalIgnoreCase) == true)
            {
                return;
            }

            throw new InvalidOperationException(Resumen(mensaje ?? respuesta));
        }

        if (doc.RootElement.TryGetProperty("done", out var hecho) && hecho.ValueKind == JsonValueKind.True)
        {
            return;
        }

        if (!doc.RootElement.TryGetProperty("name", out var nombre) || nombre.GetString() is not string operacion || operacion.Length == 0)
        {
            return;
        }

        await EsperarOperacionAsync(credencial, operacion);
    }

    private static async Task EsperarOperacionAsync(GoogleCredential credencial, string nombre)
    {
        var url = "https://firestore.googleapis.com/v1/" + nombre;
        for (var intento = 0; intento < 90; intento++)
        {
            var (estado, cuerpo) = await EnviarAsync(credencial, HttpMethod.Get, url, null);
            if (estado is < 200 or >= 300)
            {
                throw new InvalidOperationException(Resumen(cuerpo));
            }

            using var doc = JsonDocument.Parse(cuerpo);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                var mensaje = error.TryGetProperty("message", out var texto) ? texto.GetString() : cuerpo;
                throw new InvalidOperationException(Resumen(mensaje ?? cuerpo));
            }

            if (doc.RootElement.TryGetProperty("done", out var hecho) && hecho.ValueKind == JsonValueKind.True)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new InvalidOperationException("sigue creándose. Ábrela de nuevo en unos minutos.");
    }

    private static async Task<(int Estado, string Cuerpo)> EnviarAsync(
        GoogleCredential credencial,
        HttpMethod metodo,
        string url,
        string? cuerpo)
    {
        var token = await ((ITokenAccess)credencial).GetAccessTokenForRequestAsync();
        using var solicitud = new HttpRequestMessage(metodo, url);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (cuerpo is not null)
        {
            solicitud.Content = new StringContent(cuerpo, Encoding.UTF8, "application/json");
        }

        using var respuesta = await Http.SendAsync(solicitud);
        var texto = await respuesta.Content.ReadAsStringAsync();
        return ((int)respuesta.StatusCode, texto);
    }

    private static string Resumen(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return "sin detalle";
        }

        var limpio = texto.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return limpio.Length <= 180 ? limpio : limpio[..180];
    }
}
