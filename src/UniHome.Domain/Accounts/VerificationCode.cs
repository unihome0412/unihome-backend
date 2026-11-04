using System.Security.Cryptography;

namespace UniHome.Domain.Accounts;

/// <summary>
/// Verificador de un solo uso para la confirmación de correo (HU-003).
/// Solo conserva el hash/HMAC del código; el código de 6 dígitos nunca vive en el dominio.
/// </summary>
public sealed class VerificationCode
{
    public const int Digits = 6;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ResendWindow = TimeSpan.FromMinutes(1);
    public const int MaxResendsPerWindow = 3;

    private VerificationCode(
        string codeHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? consumedAt)
    {
        if (string.IsNullOrWhiteSpace(codeHash))
        {
            throw new ArgumentException("El verificador es obligatorio.", nameof(codeHash));
        }

        CodeHash = codeHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        ConsumedAt = consumedAt;
    }

    public string CodeHash { get; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public bool IsConsumed => ConsumedAt is not null;

    public static VerificationCode Issue(string codeHash, DateTimeOffset issuedAt) =>
        new(codeHash, issuedAt, issuedAt.Add(Lifetime), consumedAt: null);

    public static VerificationCode Restore(
        string codeHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? consumedAt) =>
        new(codeHash, issuedAt, expiresAt, consumedAt);

    /// <summary>
    /// Comprueba vigencia, uso previo y coincidencia del hash en tiempo constante.
    /// No consume el código; la confirmación exitosa llama a <see cref="Consume"/>.
    /// </summary>
    public CodeValidationResult Validate(string protectedCode, DateTimeOffset now)
    {
        if (IsConsumed)
        {
            return CodeValidationResult.AlreadyUsed;
        }

        if (now >= ExpiresAt)
        {
            return CodeValidationResult.Expired;
        }

        return Matches(protectedCode)
            ? CodeValidationResult.Valid
            : CodeValidationResult.Incorrect;
    }

    public void Consume(DateTimeOffset now)
    {
        if (IsConsumed)
        {
            throw new InvalidOperationException("El verificador ya fue utilizado.");
        }

        ConsumedAt = now;
    }

    private bool Matches(string protectedCode)
    {
        if (string.IsNullOrWhiteSpace(protectedCode))
        {
            return false;
        }

        try
        {
            var expected = Convert.FromBase64String(CodeHash);
            var actual = Convert.FromBase64String(protectedCode);

            return expected.Length == actual.Length &&
                   CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
