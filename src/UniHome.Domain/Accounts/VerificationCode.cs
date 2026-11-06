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

    /// <summary>
    /// Límite provisional de intentos incorrectos por verificador (defensa en
    /// profundidad "por cuenta" exigida por EP-M01-02; la spec no fija un valor).
    /// Al alcanzarlo, el verificador se trata como vencido para forzar un reenvío
    /// en vez de permitir reintentos indefinidos sobre el mismo código.
    /// </summary>
    public const int MaxConfirmationAttempts = 5;

    private VerificationCode(
        string codeHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? consumedAt,
        int failedAttempts)
    {
        if (string.IsNullOrWhiteSpace(codeHash))
        {
            throw new ArgumentException("El verificador es obligatorio.", nameof(codeHash));
        }

        if (failedAttempts < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(failedAttempts));
        }

        CodeHash = codeHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        ConsumedAt = consumedAt;
        FailedAttempts = failedAttempts;
    }

    public string CodeHash { get; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public int FailedAttempts { get; private set; }

    public bool IsConsumed => ConsumedAt is not null;

    public static VerificationCode Issue(string codeHash, DateTimeOffset issuedAt) =>
        new(codeHash, issuedAt, issuedAt.Add(Lifetime), consumedAt: null, failedAttempts: 0);

    public static VerificationCode Restore(
        string codeHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? consumedAt,
        int failedAttempts = 0) =>
        new(codeHash, issuedAt, expiresAt, consumedAt, failedAttempts);

    /// <summary>
    /// Comprueba vigencia, uso previo, intentos agotados y coincidencia del hash
    /// en tiempo constante. No consume el código ni registra el intento fallido;
    /// la confirmación exitosa llama a <see cref="Consume"/> y el llamador decide
    /// si invoca <see cref="RegisterFailedAttempt"/> ante un resultado incorrecto.
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

        if (FailedAttempts >= MaxConfirmationAttempts)
        {
            // Mismo tratamiento que un código vencido: no revela que el bloqueo
            // es por intentos y empuja a solicitar un reenvío.
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

    public void RegisterFailedAttempt() => FailedAttempts++;

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
