using System.Globalization;
using System.Reflection;
using System.Text;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

/// <summary>
/// Índice invertido por tokens. Evita recorrer las ~20 características de cada producto en cada tecla.
/// </summary>
public sealed class IndiceBusquedaFichas
{
    private static readonly PropertyInfo[] PropiedadesFicha =
        typeof(FichaTecnica).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    private static readonly HashSet<string> PropiedadesExcluidas = new(StringComparer.Ordinal)
    {
        nameof(FichaTecnica.Id),
        nameof(FichaTecnica.Caracteristicas),
        nameof(FichaTecnica.Partidas)
    };

    private readonly Dictionary<Guid, string> _texto = [];
    private readonly Dictionary<string, HashSet<Guid>> _tokens = new(StringComparer.Ordinal);
    private readonly List<string> _tokensOrdenados = [];
    private bool _tokensOrdenadosSucios = true;

    public void Reconstruir(IReadOnlyList<FichaTecnica> fichas)
    {
        _texto.Clear();
        _tokens.Clear();
        _tokensOrdenados.Clear();
        _tokensOrdenadosSucios = true;
        foreach (var ficha in fichas)
        {
            Indexar(ficha);
        }

        AsegurarTokensOrdenados();
    }

    public void Upsert(FichaTecnica ficha)
    {
        Quitar(ficha.Id);
        Indexar(ficha);
    }

    public void Quitar(Guid id)
    {
        if (!_texto.Remove(id, out var anterior))
        {
            return;
        }

        foreach (var token in ExtraerTokens(anterior))
        {
            if (_tokens.TryGetValue(token, out var conjunto))
            {
                conjunto.Remove(id);
                if (conjunto.Count == 0)
                {
                    _tokens.Remove(token);
                    _tokensOrdenadosSucios = true;
                }
            }
        }
    }

    public IEnumerable<FichaTecnica> Filtrar(IEnumerable<FichaTecnica> origen, string filtro)
    {
        if (string.IsNullOrWhiteSpace(filtro))
        {
            return origen;
        }

        var palabras = ExtraerTokens(Normalizar(filtro));
        if (palabras.Count == 0)
        {
            return origen;
        }

        HashSet<Guid>? coincidencias = null;
        foreach (var palabra in palabras)
        {
            var parcial = BuscarToken(palabra);
            if (coincidencias is null)
            {
                coincidencias = parcial;
            }
            else
            {
                coincidencias.IntersectWith(parcial);
            }

            if (coincidencias.Count == 0)
            {
                return [];
            }
        }

        return origen.Where(f => coincidencias!.Contains(f.Id));
    }

    public IReadOnlyList<Guid> FiltrarIds(string filtro, int maximo)
    {
        if (maximo <= 0 || string.IsNullOrWhiteSpace(filtro))
        {
            return [];
        }

        var palabras = ExtraerTokens(Normalizar(filtro));
        if (palabras.Count == 0)
        {
            return [];
        }

        HashSet<Guid>? coincidencias = null;
        foreach (var palabra in palabras)
        {
            var parcial = BuscarToken(palabra);
            if (coincidencias is null)
            {
                coincidencias = parcial;
            }
            else
            {
                coincidencias.IntersectWith(parcial);
            }

            if (coincidencias.Count == 0)
            {
                return [];
            }
        }

        var lista = new List<Guid>(Math.Min(maximo, coincidencias!.Count));
        foreach (var id in coincidencias)
        {
            lista.Add(id);
            if (lista.Count >= maximo)
            {
                break;
            }
        }

        return lista;
    }

    private HashSet<Guid> BuscarToken(string palabra)
    {
        var resultado = new HashSet<Guid>();
        if (_tokens.TryGetValue(palabra, out var exactos))
        {
            resultado.UnionWith(exactos);
        }

        if (palabra.Length < 2)
        {
            return resultado;
        }

        AsegurarTokensOrdenados();
        for (var i = LimiteInferior(palabra); i < _tokensOrdenados.Count; i++)
        {
            var token = _tokensOrdenados[i];
            if (!token.StartsWith(palabra, StringComparison.Ordinal))
            {
                break;
            }

            resultado.UnionWith(_tokens[token]);
        }

        return resultado;
    }

    private void AsegurarTokensOrdenados()
    {
        if (!_tokensOrdenadosSucios)
        {
            return;
        }

        _tokensOrdenados.Clear();
        _tokensOrdenados.AddRange(_tokens.Keys);
        _tokensOrdenados.Sort(StringComparer.Ordinal);
        _tokensOrdenadosSucios = false;
    }

    private int LimiteInferior(string prefijo)
    {
        var lo = 0;
        var hi = _tokensOrdenados.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (string.CompareOrdinal(_tokensOrdenados[mid], prefijo) < 0)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    private void Indexar(FichaTecnica ficha)
    {
        var texto = ConstruirTexto(ficha);
        _texto[ficha.Id] = texto;
        foreach (var token in ExtraerTokens(texto))
        {
            if (!_tokens.TryGetValue(token, out var conjunto))
            {
                conjunto = [];
                _tokens[token] = conjunto;
                _tokensOrdenadosSucios = true;
            }

            conjunto.Add(ficha.Id);
        }
    }

    public static string ConstruirTexto(FichaTecnica ficha)
    {
        var partes = new List<string>(64);

        foreach (var propiedad in PropiedadesFicha)
        {
            if (PropiedadesExcluidas.Contains(propiedad.Name) || !propiedad.CanRead)
            {
                continue;
            }

            var valor = propiedad.GetValue(ficha);
            switch (valor)
            {
                case string texto when !string.IsNullOrWhiteSpace(texto):
                    partes.Add(texto);
                    break;
                case bool bandera when bandera:
                    partes.Add(SepararPascal(propiedad.Name));
                    break;
                case double numero when Math.Abs(numero) > double.Epsilon:
                    partes.Add(numero.ToString("0.####", CultureInfo.InvariantCulture));
                    partes.Add(numero.ToString("0.####", CultureInfo.CurrentCulture));
                    break;
                case DateTimeOffset fecha:
                    partes.Add(fecha.ToString("dd/MM/yyyy"));
                    partes.Add(fecha.ToString("yyyy-MM-dd"));
                    break;
                case Guid:
                    partes.Add(ficha.AreaTitulo);
                    break;
            }
        }

        foreach (var partida in ficha.Partidas)
        {
            if (!string.IsNullOrWhiteSpace(partida.Lote))
            {
                partes.Add(partida.Lote);
            }

            if (partida.FechaCaducidad is DateTimeOffset caducidad)
            {
                partes.Add(caducidad.ToString("dd/MM/yyyy"));
                partes.Add(caducidad.ToString("yyyy-MM-dd"));
            }
        }

        foreach (var item in ficha.Caracteristicas)
        {
            if (!string.IsNullOrWhiteSpace(item.Nombre))
            {
                partes.Add(item.Nombre);
            }

            if (!string.IsNullOrWhiteSpace(item.Valor))
            {
                partes.Add(item.Valor);
            }
        }

        return Normalizar(string.Join(' ', partes.Where(p => !string.IsNullOrWhiteSpace(p))));
    }

    private static string SepararPascal(string nombre)
    {
        var builder = new StringBuilder(nombre.Length + 8);
        for (var i = 0; i < nombre.Length; i++)
        {
            var c = nombre[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(nombre[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    public static string Normalizar(string valor)
    {
        var form = valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(form.Length);
        foreach (var c in form)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static List<string> ExtraerTokens(string textoNormalizado)
    {
        if (string.IsNullOrEmpty(textoNormalizado))
        {
            return [];
        }

        return textoNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
