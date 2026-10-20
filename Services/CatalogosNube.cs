using System.Text.Json;
using Google.Cloud.Firestore;
using Iderma.Core.Services;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class CatalogosNube
{
    private const string Coleccion = "desplegables";
    private const string Cursor = "desplegables";
    private const string CursorBajas = "desplegables-bajas";

    private static readonly SemaphoreSlim Puerta = new(1, 1);
    private static FirestoreDb? _base;
    private static FirestoreChangeListener? _escucha;
    private static FirestoreChangeListener? _escuchaBajas;
    private static int _generacion;
    private static int _publicaciones;
    private static int _alineado;
    private static int _iniciado;
    private static int _nubeConocida;
    private static bool _diferencial;
    private static bool _bajasInicial;

    public static void Iniciar()
    {
        if (Interlocked.Exchange(ref _iniciado, 1) == 1)
        {
            return;
        }

        Catalogos.PublicacionPedida += Publicar;
        _ = ConectarAsync();
    }

    public static void Publicar()
    {
        var copia = Catalogos.Exportar();
        Interlocked.Increment(ref _publicaciones);
        Interlocked.Increment(ref _generacion);
        _ = EscribirAsync(copia);
    }

    private static async Task ConectarAsync()
    {
        try
        {
            await AsegurarBaseAsync();
            if (_base is null || _escucha is not null)
            {
                return;
            }

            var guardado = CursorNube.Cargar(Cursor);
            Timestamp desde;
            if (guardado.HasValue)
            {
                desde = guardado.Value;
                _diferencial = true;
                Volatile.Write(ref _nubeConocida, 1);
            }
            else if (!Catalogos.ArchivoExistiaAlAbrir)
            {
                var snap = await _base.Collection(Coleccion).GetSnapshotAsync();
                Aplicar(snap);
                desde = snap.ReadTime;
                CursorNube.Guardar(Cursor, desde);
                _diferencial = true;
                Volatile.Write(ref _nubeConocida, 1);
                GuardarIds(Catalogos.Exportar().Keys);
            }
            else
            {
                desde = CursorNube.Epoca;
            }

            var desdeBajas = CursorNube.Cargar(CursorBajas) ?? desde;
            _escucha = _base.Collection(Coleccion)
                .WhereGreaterThan("Actualizado", desde)
                .OrderBy("Actualizado")
                .Listen(Recibir);
            _escuchaBajas = _base.Collection(Coleccion + "-bajas")
                .WhereGreaterThan("Actualizado", desdeBajas)
                .OrderBy("Actualizado")
                .Listen(RecibirBajas);
        }
        catch
        {
        }
    }

    private static void Recibir(QuerySnapshot snapshot)
    {
        try
        {
            if (_diferencial)
            {
                AplicarDelta(snapshot);
            }
            else
            {
                Aplicar(snapshot);
                _diferencial = true;
            }

            CursorNube.GuardarSiAvanza(Cursor, snapshot.ReadTime);
            Volatile.Write(ref _nubeConocida, 1);
            GuardarIds(Catalogos.Exportar().Keys);
        }
        catch
        {
        }
    }

    private static void RecibirBajas(QuerySnapshot snapshot)
    {
        try
        {
            var ids = (_bajasInicial ? snapshot.Changes.Select(cambio => cambio.Document) : snapshot.Documents)
                .Select(doc => doc.Id)
                .ToList();
            _bajasInicial = true;
            if (ids.Count > 0)
            {
                var local = Catalogos.Exportar();
                var quito = false;
                foreach (var id in ids)
                {
                    quito |= local.Remove(id);
                }

                if (quito)
                {
                    Catalogos.AplicarDesdeNube(local);
                    GuardarIds(local.Keys);
                }
            }

            CursorNube.GuardarSiAvanza(CursorBajas, snapshot.ReadTime);
        }
        catch
        {
        }
    }

    private static void AplicarDelta(QuerySnapshot snapshot)
    {
        if (Volatile.Read(ref _publicaciones) > 0)
        {
            Volatile.Write(ref _alineado, 1);
            return;
        }

        var local = Catalogos.Exportar();
        var toco = false;
        foreach (var cambio in snapshot.Changes)
        {
            if (cambio.ChangeType == DocumentChange.Type.Removed || !cambio.Document.Exists)
            {
                toco |= local.Remove(cambio.Document.Id);
                continue;
            }

            local[cambio.Document.Id] = Valores(cambio.Document);
            toco = true;
        }

        if (toco)
        {
            Catalogos.AplicarDesdeNube(local);
        }
    }

    private static void Aplicar(QuerySnapshot snapshot)
    {
        if (Volatile.Read(ref _publicaciones) > 0)
        {
            Volatile.Write(ref _alineado, 1);
            return;
        }

        var generacion = Volatile.Read(ref _generacion);
        var listas = Leer(snapshot);
        if (generacion != Volatile.Read(ref _generacion) || Volatile.Read(ref _publicaciones) > 0)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _alineado, 1, 0) == 0)
        {
            if (listas.Count == 0)
            {
                if (Catalogos.ArchivoExistiaAlAbrir && Catalogos.DifiereDePredeterminadas())
                {
                    Publicar();
                }

                return;
            }

            if (Catalogos.ArchivoExistiaAlAbrir)
            {
                var local = Catalogos.Exportar();
                var faltaban = false;
                foreach (var (clave, valores) in local)
                {
                    if (valores.Count == 0 || listas.ContainsKey(clave))
                    {
                        continue;
                    }

                    listas[clave] = valores;
                    faltaban = true;
                }

                if (faltaban)
                {
                    Catalogos.AplicarDesdeNube(listas);
                    Publicar();
                    return;
                }
            }

            Catalogos.AplicarDesdeNube(listas);
            return;
        }

        if (generacion != Volatile.Read(ref _generacion))
        {
            return;
        }

        Catalogos.AplicarDesdeNube(listas);
    }

    private static async Task EscribirAsync(Dictionary<string, List<string>> listas)
    {
        try
        {
            await AsegurarBaseAsync();
            if (_base is null)
            {
                return;
            }

            await Puerta.WaitAsync();
            try
            {
                var coleccion = _base.Collection(Coleccion);
                var bajas = _base.Collection(Coleccion + "-bajas");
                var ids = listas.Keys.ToHashSet(StringComparer.Ordinal);
                var conocidos = CargarIds();
                var alineada = Volatile.Read(ref _nubeConocida) == 1 || CursorNube.Existe(Cursor) || conocidos.Count > 0;
                if (!alineada)
                {
                    var existentes = await coleccion.GetSnapshotAsync();
                    foreach (var documento in existentes.Documents)
                    {
                        conocidos.Add(documento.Id);
                    }
                }

                var lote = _base.StartBatch();
                var operaciones = 0;

                foreach (var id in conocidos)
                {
                    if (ids.Contains(id))
                    {
                        continue;
                    }

                    lote.Delete(coleccion.Document(id));
                    lote.Set(bajas.Document(id), new Dictionary<string, object>
                    {
                        ["Actualizado"] = FieldValue.ServerTimestamp
                    });
                    operaciones += 2;
                    if (operaciones >= 400)
                    {
                        await lote.CommitAsync();
                        lote = _base.StartBatch();
                        operaciones = 0;
                    }
                }

                foreach (var (clave, valores) in listas)
                {
                    lote.Set(coleccion.Document(clave), Datos(valores));
                    operaciones++;
                    if (operaciones >= 400)
                    {
                        await lote.CommitAsync();
                        lote = _base.StartBatch();
                        operaciones = 0;
                    }
                }

                if (operaciones > 0)
                {
                    await lote.CommitAsync();
                }

                GuardarIds(ids);
                Volatile.Write(ref _nubeConocida, 1);
            }
            finally
            {
                Puerta.Release();
            }
        }
        catch
        {
        }
        finally
        {
            Interlocked.Decrement(ref _publicaciones);
        }
    }

    private static async Task AsegurarBaseAsync()
    {
        if (_base is not null)
        {
            return;
        }

        await Puerta.WaitAsync();
        try
        {
            if (_base is not null)
            {
                return;
            }

            _base = await ConexionFirebase.AbrirAsync();
        }
        finally
        {
            Puerta.Release();
        }
    }

    private static Dictionary<string, List<string>> Leer(QuerySnapshot snapshot)
    {
        var listas = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var documento in snapshot.Documents)
        {
            listas[documento.Id] = Valores(documento);
        }

        return listas;
    }

    private static List<string> Valores(DocumentSnapshot documento)
    {
        if (!documento.ContainsField("Valores"))
        {
            return [];
        }

        var valor = documento.GetValue<object>("Valores");
        if (valor is not IEnumerable<object> objetos)
        {
            return [];
        }

        return objetos
            .Select(o => o?.ToString()?.Trim() ?? string.Empty)
            .Where(texto => texto.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, object> Datos(List<string> valores) => new()
    {
        ["Valores"] = valores,
        ["Actualizado"] = FieldValue.ServerTimestamp
    };

    private static HashSet<string> CargarIds()
    {
        var ruta = RutaIds();
        if (!File.Exists(ruta))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            var ids = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(ruta)) ?? [];
            return ids.ToHashSet(StringComparer.Ordinal);
        }
        catch
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static void GuardarIds(IEnumerable<string> ids)
    {
        var ruta = RutaIds();
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta, JsonSerializer.Serialize(ids.OrderBy(id => id, StringComparer.Ordinal).ToList()));
    }

    private static string RutaIds() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IdermaCapilar",
            "nube-ids-desplegables.json");

}
