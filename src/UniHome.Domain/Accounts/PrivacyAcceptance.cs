namespace UniHome.Domain.Accounts;

/// <summary>
/// Evidencia del consentimiento del aviso de privacidad exigido por HU-012:
/// se conserva la versión mostrada y la fecha de aceptación.
/// </summary>
public sealed class PrivacyAcceptance
{
    public const int MaxVersionLength = 50;

    public PrivacyAcceptance(string version, DateTimeOffset acceptedAt)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("La versión del aviso de privacidad es obligatoria.", nameof(version));
        }

        var normalized = version.Trim();

        if (normalized.Length > MaxVersionLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"La versión del aviso de privacidad no debe superar {MaxVersionLength} caracteres.",
                nameof(version));
        }

        Version = normalized;
        AcceptedAt = acceptedAt;
    }

    public string Version { get; }

    public DateTimeOffset AcceptedAt { get; }
}
