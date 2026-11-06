namespace UniHome.Domain.Accounts;

/// <summary>
/// Agregado raíz de cuenta (HU-001, HU-012, ARQ-003). No contiene roles:
/// una cuenta es única y las capacidades se derivan de sus vínculos y autorizaciones.
/// </summary>
public sealed class Account
{
    public const int MinFullNameLength = 2;
    public const int MaxFullNameLength = 120;

    private readonly List<DateTimeOffset> _verificationResendAttempts;

    private Account(
        Guid id,
        string fullName,
        EmailAddress email,
        DateOnly birthDate,
        Gender gender,
        string passwordHash,
        PrivacyAcceptance privacyAcceptance,
        AccountStatus status,
        bool isEmailConfirmed,
        DateTimeOffset createdAt,
        DateTimeOffset? emailConfirmedAt,
        VerificationCode? emailVerification,
        IEnumerable<DateTimeOffset> verificationResendAttempts,
        DateTimeOffset? deletedAt)
    {
        Id = id;
        FullName = fullName;
        Email = email;
        BirthDate = birthDate;
        Gender = gender;
        PasswordHash = passwordHash;
        PrivacyAcceptance = privacyAcceptance;
        Status = status;
        IsEmailConfirmed = isEmailConfirmed;
        CreatedAt = createdAt;
        EmailConfirmedAt = emailConfirmedAt;
        EmailVerification = emailVerification;
        _verificationResendAttempts = [.. verificationResendAttempts];
        DeletedAt = deletedAt;
    }

    public Guid Id { get; }

    public string FullName { get; }

    public EmailAddress Email { get; }

    public DateOnly BirthDate { get; }

    public Gender Gender { get; }

    public string PasswordHash { get; }

    public PrivacyAcceptance PrivacyAcceptance { get; }

    public AccountStatus Status { get; private set; }

    public bool IsEmailConfirmed { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? EmailConfirmedAt { get; private set; }

    public VerificationCode? EmailVerification { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public IReadOnlyList<DateTimeOffset> VerificationResendAttempts =>
        _verificationResendAttempts.AsReadOnly();

    public static Account Register(
        Guid id,
        string fullName,
        EmailAddress email,
        DateOnly birthDate,
        Gender gender,
        string passwordHash,
        PrivacyAcceptance privacyAcceptance,
        DateTimeOffset now)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El identificador de la cuenta es obligatorio.", nameof(id));
        }

        if (email is null)
        {
            throw new ArgumentNullException(nameof(email));
        }

        if (privacyAcceptance is null)
        {
            throw new ArgumentNullException(nameof(privacyAcceptance));
        }

        var normalizedName = NormalizeFullName(fullName);

        if (birthDate == default || birthDate > DateOnly.FromDateTime(now.UtcDateTime))
        {
            throw new ArgumentException("La fecha de nacimiento debe ser una fecha pasada válida.", nameof(birthDate));
        }

        if (!Enum.IsDefined(gender))
        {
            throw new ArgumentException("El género indicado no es válido.", nameof(gender));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("El hash de contraseña es obligatorio.", nameof(passwordHash));
        }

        return new Account(
            id,
            normalizedName,
            email,
            birthDate,
            gender,
            passwordHash,
            privacyAcceptance,
            AccountStatus.Active,
            isEmailConfirmed: false,
            now,
            emailConfirmedAt: null,
            emailVerification: null,
            verificationResendAttempts: [],
            deletedAt: null);
    }

    public static Account Restore(
        Guid id,
        string fullName,
        EmailAddress email,
        DateOnly birthDate,
        Gender gender,
        string passwordHash,
        PrivacyAcceptance privacyAcceptance,
        AccountStatus status,
        bool isEmailConfirmed,
        DateTimeOffset createdAt,
        DateTimeOffset? emailConfirmedAt,
        VerificationCode? emailVerification,
        IEnumerable<DateTimeOffset> verificationResendAttempts,
        DateTimeOffset? deletedAt = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El identificador de la cuenta es obligatorio.", nameof(id));
        }

        if (email is null)
        {
            throw new ArgumentNullException(nameof(email));
        }

        if (privacyAcceptance is null)
        {
            throw new ArgumentNullException(nameof(privacyAcceptance));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentException("El estado de la cuenta no es válido.", nameof(status));
        }

        if (!Enum.IsDefined(gender))
        {
            throw new ArgumentException("El género indicado no es válido.", nameof(gender));
        }

        if (isEmailConfirmed != emailConfirmedAt.HasValue)
        {
            throw new ArgumentException("La confirmación de correo es inconsistente.", nameof(isEmailConfirmed));
        }

        return new Account(
            id,
            NormalizeFullName(fullName),
            email,
            birthDate,
            gender,
            passwordHash,
            privacyAcceptance,
            status,
            isEmailConfirmed,
            createdAt,
            emailConfirmedAt,
            emailVerification,
            verificationResendAttempts,
            deletedAt);
    }

    /// <summary>
    /// Emite el primer verificador de correo. Se guarda únicamente su hash/HMAC.
    /// </summary>
    public void IssueEmailVerification(string codeHash, DateTimeOffset now)
    {
        if (IsEmailConfirmed)
        {
            throw new InvalidOperationException("La cuenta ya tiene el correo confirmado.");
        }

        if (EmailVerification is not null)
        {
            throw new InvalidOperationException("Ya existe un verificador emitido.");
        }

        EmailVerification = VerificationCode.Issue(codeHash, now);
    }

    /// <summary>
    /// Indica si procede un reenvío dentro de la ventana y el límite configurados.
    /// La verificación por IP corresponde a Application/Infrastructure.
    /// </summary>
    public bool CanResendEmailVerification(DateTimeOffset now)
    {
        if (IsEmailConfirmed || EmailVerification is null || EmailVerification.IsConsumed)
        {
            return false;
        }

        PruneResendAttempts(now);
        return _verificationResendAttempts.Count < VerificationCode.MaxResendsPerWindow;
    }

    public void ResendEmailVerification(string codeHash, DateTimeOffset now)
    {
        if (!CanResendEmailVerification(now))
        {
            throw new InvalidOperationException("No es posible reenviar el código en este momento.");
        }

        _verificationResendAttempts.Add(now);
        EmailVerification = VerificationCode.Issue(codeHash, now);
    }

    public EmailConfirmationResult ConfirmEmail(string protectedCode, DateTimeOffset now)
    {
        if (IsEmailConfirmed)
        {
            return EmailConfirmationResult.AlreadyConfirmed;
        }

        if (EmailVerification is null)
        {
            return EmailConfirmationResult.NotIssued;
        }

        var validation = EmailVerification.Validate(protectedCode, now);

        if (validation != CodeValidationResult.Valid)
        {
            if (validation == CodeValidationResult.Incorrect)
            {
                // Límite "por cuenta" de EP-M01-02: cada intento incorrecto cuenta,
                // incluso si todavía no alcanza el máximo (ver MaxConfirmationAttempts).
                EmailVerification.RegisterFailedAttempt();
            }

            return validation switch
            {
                CodeValidationResult.Expired => EmailConfirmationResult.ExpiredCode,
                CodeValidationResult.AlreadyUsed => EmailConfirmationResult.AlreadyUsed,
                _ => EmailConfirmationResult.IncorrectCode
            };
        }

        EmailVerification.Consume(now);
        IsEmailConfirmed = true;
        EmailConfirmedAt = now;

        return EmailConfirmationResult.Confirmed;
    }

    /// <summary>
    /// HU-096: borrado lógico. No elimina el documento; las sesiones activas se
    /// revocan por separado (Application/Infrastructure), no aquí.
    /// </summary>
    public void Delete(DateTimeOffset now)
    {
        if (Status == AccountStatus.Deleted)
        {
            throw new InvalidOperationException("La cuenta ya está eliminada.");
        }

        Status = AccountStatus.Deleted;
        DeletedAt = now;
    }

    private void PruneResendAttempts(DateTimeOffset now)
    {
        var threshold = now - VerificationCode.ResendWindow;
        _verificationResendAttempts.RemoveAll(attempt => attempt <= threshold);
    }

    private static string NormalizeFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("El nombre completo es obligatorio.", nameof(fullName));
        }

        var normalized = fullName.Trim();

        if (normalized.Length is < MinFullNameLength or > MaxFullNameLength ||
            normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"El nombre completo debe tener entre {MinFullNameLength} y {MaxFullNameLength} caracteres válidos.",
                nameof(fullName));
        }

        return normalized;
    }
}
