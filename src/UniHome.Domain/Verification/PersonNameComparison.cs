using System.Globalization;
using System.Text;

namespace UniHome.Domain.Verification;

/// <summary>
/// Comparación orientativa de nombres OCR (HU-019). Normaliza mayúsculas,
/// acentos, puntuación y orden de tokens; nunca decide por sí sola ni expone los
/// nombres en las señales de riesgo.
/// </summary>
public static class PersonNameComparison
{
    private static readonly HashSet<string> StopWords =
        new(StringComparer.Ordinal)
        {
            "DE", "DEL", "LA", "LAS", "LOS", "Y", "MC", "VAN", "DA", "DER"
        };

    public static ResultadoComparacionNombres Compare(string? first, string? second)
    {
        var firstTokens = Tokenize(first);
        var secondTokens = Tokenize(second);

        if (firstTokens.Count < 2 || secondTokens.Count < 2)
        {
            return ResultadoComparacionNombres.NoDisponible;
        }

        var firstSet = firstTokens.ToHashSet(StringComparer.Ordinal);
        var secondSet = secondTokens.ToHashSet(StringComparer.Ordinal);

        if (firstSet.SetEquals(secondSet))
        {
            return ResultadoComparacionNombres.CoincidenciaOrientativa;
        }

        var intersection = firstSet.Intersect(secondSet, StringComparer.Ordinal).Count();

        if (intersection < 2)
        {
            return ResultadoComparacionNombres.DiferenciaDetectada;
        }

        var minimum = Math.Min(firstSet.Count, secondSet.Count);

        if (intersection >= minimum)
        {
            return ResultadoComparacionNombres.CoincidenciaOrientativa;
        }

        var union = firstSet.Union(secondSet, StringComparer.Ordinal).Count();
        var jaccard = union == 0 ? 0 : (double)intersection / union;

        return jaccard >= 0.6
            ? ResultadoComparacionNombres.CoincidenciaOrientativa
            : ResultadoComparacionNombres.DiferenciaDetectada;
    }

    public static IReadOnlyList<string> Tokenize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var normalized = RemoveDiacritics(value)
            .ToUpperInvariant()
            .Select(character => char.IsLetter(character) ? character : ' ')
            .ToArray();
        var tokens = new string(normalized)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 1 && !StopWords.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return tokens;
    }

    public static string Normalize(string? value) =>
        string.Join(' ', Tokenize(value));

    private static string RemoveDiacritics(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
