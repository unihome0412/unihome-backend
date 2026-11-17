namespace UniHome.Domain.Verification;

/// <summary>
/// Señales automáticas orientativas. Nunca decide ni transiciona el expediente;
/// solo alimenta la etiqueta "Sospechoso" que revisa un administrador (HU-020,
/// HU-022). Las señales no contienen contenido documental ni datos personales.
/// </summary>
public sealed class IndicadorRiesgo
{
    public const int MaxSignals = 12;
    public const int MaxSignalLength = 200;

    private readonly List<string> _signals;

    private IndicadorRiesgo(IEnumerable<string> signals)
    {
        _signals = [.. signals];
    }

    public static IndicadorRiesgo Empty { get; } = new([]);

    public IReadOnlyList<string> Signals => _signals.AsReadOnly();

    public bool IsSuspicious => _signals.Count > 0;

    public static IndicadorRiesgo Create(IEnumerable<string>? signals)
    {
        var normalized = (signals ?? [])
            .Where(signal => !string.IsNullOrWhiteSpace(signal))
            .Select(NormalizeSignal)
            .Where(signal => signal.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Take(MaxSignals)
            .ToArray();

        return normalized.Length == 0 ? Empty : new IndicadorRiesgo(normalized);
    }

    private static string NormalizeSignal(string signal)
    {
        var normalized = signal.Trim();

        if (normalized.Length > MaxSignalLength)
        {
            normalized = normalized[..MaxSignalLength];
        }

        return normalized.Any(char.IsControl)
            ? new string(normalized.Where(character => !char.IsControl(character)).ToArray())
            : normalized;
    }
}
