using UniHome.Domain.Common;

namespace UniHome.Domain.Publications;

/// <summary>
/// Aceptación verificable de una versión de términos para la primera publicación
/// (HU-091). No sustituye el envío a revisión de Sprint 4.
/// </summary>
public sealed class TermsAcceptance
{
    public const int MaxVersionLength = 50;

    private TermsAcceptance(
        Guid id,
        Guid accountId,
        string version,
        DateTimeOffset acceptedAt)
    {
        Id = id;
        AccountId = accountId;
        Version = version;
        AcceptedAt = acceptedAt;
    }

    public Guid Id { get; }
    public Guid AccountId { get; }
    public string Version { get; }
    public DateTimeOffset AcceptedAt { get; }

    public static TermsAcceptance Create(
        Guid accountId,
        string? version,
        DateTimeOffset now)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("La cuenta es obligatoria.", nameof(accountId));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("La versión de los términos es obligatoria.", nameof(version));
        }

        var normalized = TextNormalization.CollapseWhitespace(version);

        if (normalized.Length > MaxVersionLength)
        {
            throw new ArgumentException(
                $"La versión no debe superar {MaxVersionLength} caracteres.",
                nameof(version));
        }

        return new TermsAcceptance(Guid.NewGuid(), accountId, normalized, now);
    }

    public static TermsAcceptance Restore(
        Guid id,
        Guid accountId,
        string version,
        DateTimeOffset acceptedAt) =>
        new(id, accountId, version, acceptedAt);
}
