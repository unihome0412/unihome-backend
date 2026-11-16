using System.Text;
using System.Text.RegularExpressions;
using UniHome.Domain.Verification;

namespace UniHome.Infrastructure.Analysis;

/// <summary>
/// Extrae un nombre legible de un texto OCR sin inventar valores. Primero busca
/// etiquetas explícitas (INE, propietario, contribuyente) y solo después aplica
/// una heurística conservadora sobre líneas con aspecto de nombre.
/// </summary>
internal static partial class DocumentNameExtractor
{
    private static readonly string[] StopWords =
    [
        "INSTITUTO", "NACIONAL", "ELECTORAL", "CREDENCIAL", "VOTAR", "CLAVE", "SECCION",
        "DOMICILIO", "ESTADO", "MUNICIPIO", "LOCALIDAD", "PREDIAL", "CATASTRO", "RECIBO",
        "TESORERIA", "GOBIERNO", "SECRETARIA", "FINANZAS", "TOTAL", "FECHA", "CUENTA",
        "PERIODO", "IMPORTE", "PAGO", "BANCO", "SUCURSAL", "TELEFONO", "CORREO"
    ];

    public static string? Extract(string? text, TipoDocumentoVerificacion documentType)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var lines = text
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeLine)
            .Where(line => line.Length > 0)
            .Take(80)
            .ToArray();

        if (lines.Length == 0)
        {
            return null;
        }

        var labeled = documentType == TipoDocumentoVerificacion.Ine
            ? ExtractIne(lines)
            : ExtractOwner(lines);

        return labeled ?? ExtractFallback(lines);
    }

    private static string? ExtractIne(IReadOnlyList<string> lines)
    {
        var paternal = ExtractLabeledValue(lines, "APELLIDO PATERNO", "PRIMER APELLIDO");
        var maternal = ExtractLabeledValue(lines, "APELLIDO MATERNO", "SEGUNDO APELLIDO");
        var names = ExtractLabeledValue(lines, "NOMBRE(S)", "NOMBRES", "NOMBRE");

        if (paternal is not null || maternal is not null)
        {
            var parts = new[] { paternal, maternal, names }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!)
                .ToArray();

            if (parts.Length > 0)
            {
                return JoinName(parts);
            }
        }

        return names;
    }

    private static string? ExtractOwner(IReadOnlyList<string> lines) =>
        ExtractLabeledValue(
            lines,
            "NOMBRE DEL PROPIETARIO",
            "NOMBRE DEL CONTRIBUYENTE",
            "NOMBRE DEL TITULAR",
            "PROPIETARIO",
            "CONTRIBUYENTE",
            "TITULAR",
            "A NOMBRE DE",
            "NOMBRE");

    private static string? ExtractLabeledValue(
        IReadOnlyList<string> lines,
        params string[] labels)
    {
        foreach (var label in labels)
        {
            var pattern = LabelRegex(label);

            for (var index = 0; index < lines.Count; index++)
            {
                var match = pattern.Match(lines[index]);

                if (!match.Success)
                {
                    continue;
                }

                var inline = CleanName(match.Groups["value"].Value, allowSingleWord: true);

                if (inline is not null)
                {
                    return inline;
                }

                for (var next = index + 1; next < Math.Min(lines.Count, index + 4); next++)
                {
                    if (LooksLikeLabel(lines[next]))
                    {
                        break;
                    }

                    var candidate = CleanName(lines[next], allowSingleWord: true);

                    if (candidate is not null)
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }

    private static string? ExtractFallback(IReadOnlyList<string> lines)
    {
        foreach (var line in lines.Take(25))
        {
            var candidate = CleanName(line);

            if (candidate is not null &&
                !StopWords.Any(word => ContainsWord(line, word)))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? CleanName(string? value, bool allowSingleWord = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var candidate = Regex.Replace(value, @"\s+", " ").Trim(' ', ':', '-', '.', ',', ';');
        candidate = candidate.Replace("  ", " ", StringComparison.Ordinal).Trim();

        if (candidate.Length is < 3 or > 80 ||
            candidate.Any(char.IsDigit) ||
            !candidate.All(character =>
                char.IsLetter(character) ||
                character is ' ' or '\'' or '-' or '.'))
        {
            return null;
        }

        var words = candidate
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Any(char.IsLetter))
            .ToArray();
        var minimumWords = allowSingleWord ? 1 : 2;

        return words.Length >= minimumWords && words.Length <= 6
            ? string.Join(' ', words)
            : null;
    }

    private static string JoinName(IEnumerable<string> parts) =>
        Regex.Replace(string.Join(' ', parts), @"\s+", " ").Trim();

    private static string NormalizeLine(string line)
    {
        var builder = new StringBuilder(line.Length);

        foreach (var character in line)
        {
            if (char.IsControl(character))
            {
                builder.Append(' ');
            }
            else
            {
                builder.Append(character);
            }
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }

    private static bool LooksLikeLabel(string line) =>
        line.Contains(':', StringComparison.Ordinal) ||
        Regex.IsMatch(
            line,
            @"^(NOMBRE|APELLIDO|PROPIETARIO|CONTRIBUYENTE|TITULAR|DOMICILIO|CLAVE|FECHA)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool ContainsWord(string line, string word) =>
        Regex.IsMatch(
            line,
            $@"\b{Regex.Escape(word)}\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static Regex LabelRegex(string label) =>
        new(
            $@"^\s*{Regex.Escape(label)}\s*(?:\([^)]*\))?\s*[:\-]?\s*(?<value>.*)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
