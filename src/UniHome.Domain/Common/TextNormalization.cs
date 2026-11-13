using System.Globalization;
using System.Text;

namespace UniHome.Domain.Common;

/// <summary>
/// Normalización estable para búsquedas y claves de unicidad: minúsculas, sin
/// acentos y con espacios colapsados. No altera el valor visible original.
/// </summary>
public static class TextNormalization
{
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                normalized.Append(char.ToLowerInvariant(character));
            }
        }

        return CollapseWhitespace(normalized.ToString());
    }

    public static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
