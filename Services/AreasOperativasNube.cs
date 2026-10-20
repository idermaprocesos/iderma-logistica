using System.Text.Json;
using Google.Cloud.Firestore;
using Iderma.Core.Services;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class AreasOperativasNube
{
    private const string Coleccion = "areas-operativas";
    private const string Cursor = "areas-operativas";
    private const string CursorBajas = "areas-operativas-bajas";

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

        AreasOperativas.PublicacionPedida += Publicar;
        _ = ConectarAsync();
    }

    public static void Publicar()
    {
        var copia = AreasOperativas.Todas.ToList();
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
            else if (!AreasOperativas.ArchivoExistiaAlAbrir)
            {
                var snap = await _base.Collection(Coleccion).GetSnapshotAsync();
                Aplicar(snap);
                desde = snap.ReadTime;
                CursorNube.Guardar(Cursor, desde);
                _diferencial = true;
                Volatile.Write(ref _nubeConocida, 1);
                GuardarIds(AreasOperativas.Todas.Select(area => area.Id.ToString("D")));
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
            GuardarIds(AreasOperativas.Todas.Select(area => area.Id.ToString("D")));
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
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _bajasInicial = true;
            if (ids.Count > 0)
            {
                var areas = AreasOperativas.Todas.Where(area => !ids.Contains(area.Id.ToString("D"))).ToList();
                if (areas.Count != AreasOperativas.Todas.Count)
                {
                    AreasOperativas.ReemplazarDesdeNube(areas);
                    GuardarIds(areas.Select(area => area.Id.ToString("D")));
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

        var areas = AreasOperativas.Todas.ToList();
        var toco = false;
        foreach (var cambio in snapshot.Changes)
        {
            if (cambio.ChangeType == DocumentChange.Type.Removed || !cambio.Document.Exists)
            {
                var antes = areas.Count;
                areas.RemoveAll(area => area.Id.ToString("D").Equals(cambio.Document.Id, StringComparison.OrdinalIgnoreCase));
                toco |= areas.Count != antes;
                continue;
            }

            if (LeerArea(cambio.Document) is not AreaDefinicion area)
            {
                continue;
            }

            var indice = areas.FindIndex(item => item.Id == area.Id);
            if (indice >= 0)
            {
                areas[indice] = area;
            }
            else
            {
                areas.Add(area);
            }

            toco = true;
        }

        if (toco)
        {
            AreasOperativas.ReemplazarDesdeNube(areas);
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
        var areas = Leer(snapshot);
        if (generacion != Volatile.Read(ref _generacion) || Volatile.Read(ref _publicaciones) > 0)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _alineado, 1, 0) == 0)
        {
            if (areas.Count == 0)
            {
                if (AreasOperativas.ArchivoExistiaAlAbrir && AreasOperativas.DifiereDePredeterminadas())
                {
                    Publicar();
                }

                return;
            }

            if (AreasOperativas.ArchivoExistiaAlAbrir)
            {
                var ids = areas.Select(a => a.Id).ToHashSet();
                var soloLocal = AreasOperativas.Todas.Where(a => !ids.Contains(a.Id)).ToList();
                if (soloLocal.Count > 0)
                {
                    areas.AddRange(soloLocal);
                    AreasOperativas.ReemplazarDesdeNube(areas);
                    Publicar();
                    return;
                }
            }

            AreasOperativas.ReemplazarDesdeNube(areas);
            return;
        }

        if (generacion != Volatile.Read(ref _generacion))
        {
            return;
        }

        AreasOperativas.ReemplazarDesdeNube(areas);
    }

    private static async Task EscribirAsync(List<AreaDefinicion> areas)
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
                var ids = areas.Select(a => a.Id.ToString("D")).ToHashSet(StringComparer.OrdinalIgnoreCase);
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

                foreach (var area in areas)
                {
                    lote.Set(coleccion.Document(area.Id.ToString("D")), Datos(area));
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

    private static List<AreaDefinicion> Leer(QuerySnapshot snapshot)
    {
        var areas = new List<AreaDefinicion>();
        foreach (var documento in snapshot.Documents)
        {
            if (LeerArea(documento) is AreaDefinicion area)
            {
                areas.Add(area);
            }
        }

        return areas;
    }

    private static AreaDefinicion? LeerArea(DocumentSnapshot documento)
    {
        if (!Guid.TryParse(documento.Id, out var id))
        {
            return null;
        }

        var plantillaTexto = Texto(documento, "Plantilla");
        if (!Enum.TryParse<AreaOperativa>(plantillaTexto, out var plantilla))
        {
            plantilla = AreaOperativa.Clinica;
        }

        return new AreaDefinicion
        {
            Id = id,
            Nombre = Texto(documento, "Nombre"),
            Descripcion = Texto(documento, "Descripcion"),
            Prefijo = Texto(documento, "Prefijo"),
            Glifo = GlifosWinUi.Caracter(Texto(documento, "Glifo")),
            Plantilla = plantilla,
            Orden = Entero(documento, "Orden")
        };
    }

    private static Dictionary<string, object> Datos(AreaDefinicion area) => new()
    {
        ["Nombre"] = area.Nombre,
        ["Descripcion"] = area.Descripcion,
        ["Prefijo"] = area.Prefijo,
        ["Glifo"] = GlifosWinUi.CodigoXaml(area.Glifo),
        ["Plantilla"] = area.Plantilla.ToString(),
        ["Orden"] = area.Orden,
        ["Actualizado"] = FieldValue.ServerTimestamp
    };

    private static string Texto(DocumentSnapshot documento, string campo) =>
        documento.ContainsField(campo) && documento.GetValue<object>(campo) is string texto ? texto : string.Empty;

    private static int Entero(DocumentSnapshot documento, string campo)
    {
        if (!documento.ContainsField(campo))
        {
            return 0;
        }

        var valor = documento.GetValue<object>(campo);
        return valor switch
        {
            int numero => numero,
            long largo => (int)largo,
            _ => 0
        };
    }

    private static HashSet<string> CargarIds()
    {
        var ruta = RutaIds();
        if (!File.Exists(ruta))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var ids = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(ruta)) ?? [];
            return ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void GuardarIds(IEnumerable<string> ids)
    {
        var ruta = RutaIds();
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        File.WriteAllText(ruta, JsonSerializer.Serialize(ids.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList()));
    }

    private static string RutaIds() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IdermaCapilar",
            "nube-ids-areas-operativas.json");

}
