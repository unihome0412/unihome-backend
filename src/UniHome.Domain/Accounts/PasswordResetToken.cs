using System.Security.Cryptography;
using System.Text;

namespace UniHome.Domain.Accounts;

/// <summary>
/// Solicitud de recuperación de contraseña (HU-006). Solo conserva el hash del
/// token de un solo uso; el token en claro únicamente se entrega al remitente de
/// correo y nunca vive en el dominio ni en MongoDB.
/// </summary>
public sealed class PasswordResetToken
{
    /// <summary>Vigencia aprobada por la spec de HU-006: 30 minutos.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Límite provisional de intentos por token. El token es de alta entropía;
    /// este límite añade defensa en profundidad y fuerza una nueva solicitud.
    /// </summary>
    public const int MaxAttempts = 5;

    private PasswordResetToken(
        Guid id,
        Guid accountId,
        string tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? consumedAt,
        int failedAttempts)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El identificador del token es obligatorio.", nameof(id));
        }

        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("La cuenta del token es obligatoria.", nameof(accountId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("El hash del token es obligatorio.", nameof(tokenHash));
        }

        if (expiresAt <= issuedAt)
        {
            throw new ArgumentException(
                "La expiración del token debe ser posterior a su emisión.",
                nameof(expiresAt));
        }

        if (failedAttempts < 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(failedAttempts);
        }

        Id = id;
        AccountId = accountId;
        TokenHash = tokenHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        ConsumedAt = consumedAt;
        FailedAttempts = failedAttempts;
    }

    public Guid Id { get; }

    public Guid AccountId { get; }

    public string TokenHash { get; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public int FailedAttempts { get; private set; }

    public bool IsConsumed => ConsumedAt is not null;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool HasExceededAttempts => FailedAttempts >= MaxAttempts;

    public static PasswordResetToken Issue(
        Guid id,
        Guid accountId,
        string tokenHash,
        DateTimeOffset issuedAt) =>
        new(
            id,
            accountId,
            tokenHash,
            issuedAt,
            issuedAt.Add(Lifetime),
            consumedAt: null,
            failedAttempts: 0);

    public static PasswordResetToken Restore(
        Guid id,
        Guid accountId,
        string tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? consumedAt,
        int failedAttempts = 0) =>
        new(
            id,
            accountId,
            tokenHash,
            issuedAt,
            expiresAt,
            consumedAt,
            failedAttempts);

    /// <summary>
    /// Comprueba vigencia, consumo previo, intentos agotados y coincidencia del
    /// hash en tiempo constante. No consume el token ni incrementa intentos.
    /// </summary>
    public PasswordResetValidation Validate(string tokenHash, DateTimeOffset now)
    {
        if (IsConsumed)
        {
            return PasswordResetValidation.AlreadyUsed;
        }

        if (IsExpired(now) || HasExceededAttempts)
        {
            return PasswordResetValidation.Expired;
        }

        return Matches(tokenHash)
            ? PasswordResetValidation.Valid
            : PasswordResetValidation.Invalid;
    }

    public void Consume(DateTimeOffset now)
    {
        if (IsConsumed)
        {
            throw new InvalidOperationException("El token de recuperación ya fue utilizado.");
        }

        ConsumedAt = now;
    }

    public void RegisterFailedAttempt() => FailedAttempts++;

    private bool Matches(string tokenHash)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return false;
        }

        var expected = Encoding.ASCII.GetBytes(TokenHash);
        var actual = Encoding.ASCII.GetBytes(tokenHash);

        return expected.Length == actual.Length &&
               CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}

public enum PasswordResetValidation
{
    Valid,
    Invalid,
    Expired,
    AlreadyUsed
}
