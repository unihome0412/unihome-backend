using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Exceptions;
using UniHome.Domain.Accounts;

namespace UniHome.Application.Identity.RegisterAccount;

/// <summary>
/// Caso de uso de registro (HU-001/HU-012). No asigna roles: la cuenta nace como
/// usuario de UniHome y las capacidades se resuelven después por vínculos/autorización.
/// </summary>
public sealed class RegisterAccountService
{
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 128;

    private readonly IAccountRepository _accounts;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISystemClock _clock;
    private readonly IVerificationCodeGenerator _codeGenerator;
    private readonly IVerificationCodeProtector _codeProtector;
    private readonly IVerificationEmailSender _emailSender;

    public RegisterAccountService(
        IAccountRepository accounts,
        IPasswordHasher passwordHasher,
        ISystemClock clock,
        IVerificationCodeGenerator codeGenerator,
        IVerificationCodeProtector codeProtector,
        IVerificationEmailSender emailSender)
    {
        _accounts = accounts;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _codeGenerator = codeGenerator;
        _codeProtector = codeProtector;
        _emailSender = emailSender;
    }

    public async Task<RegisterAccountResult> RegisterAsync(
        RegisterAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = _clock.UtcNow;
        var (email, errors) = Validate(command, now);

        if (email is null || errors.Count > 0)
        {
            return RegisterAccountResult.Invalid(errors);
        }

        // Se calcula el hash antes de escribir para que un correo duplicado no
        // produzca una respuesta sensiblemente más rápida que un registro nuevo.
        var passwordHash = _passwordHasher.HashPassword(command.Password!);
        var rawCode = _codeGenerator.Generate();
        var protectedCode = _codeProtector.Protect(rawCode);

        var account = Account.Register(
            Guid.NewGuid(),
            command.FullName!,
            email,
            command.BirthDate!.Value,
            command.Gender!.Value,
            passwordHash,
            new PrivacyAcceptance(command.PrivacyVersion!, now),
            now);

        account.IssueEmailVerification(protectedCode, now);

        try
        {
            await _accounts
                .AddAsync(account, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DuplicateEmailException)
        {
            return RegisterAccountResult.DuplicateEmail();
        }

        var verificationEmailSent = false;

        try
        {
            verificationEmailSent = await _emailSender
                .SendAsync(email, rawCode, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // La cuenta y el verificador ya quedaron persistidos. No se registra
            // el código ni el destino; el reenvío queda como tarea pendiente de M01.
            verificationEmailSent = false;
        }

        return RegisterAccountResult.Success(account.Id, email.Value, verificationEmailSent);
    }

    private static (EmailAddress? Email, IReadOnlyDictionary<string, string[]> Errors) Validate(
        RegisterAccountCommand command,
        DateTimeOffset now)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var messages))
            {
                messages = [];
                errors[field] = messages;
            }

            messages.Add(message);
        }

        var fullName = command.FullName?.Trim();

        if (string.IsNullOrEmpty(fullName))
        {
            Add("nombreCompleto", "El nombre completo es obligatorio.");
        }
        else if (fullName.Length is < Account.MinFullNameLength or > Account.MaxFullNameLength ||
                 fullName.Any(char.IsControl))
        {
            Add(
                "nombreCompleto",
                $"El nombre completo debe tener entre {Account.MinFullNameLength} y {Account.MaxFullNameLength} caracteres válidos.");
        }

        EmailAddress? email = null;

        if (string.IsNullOrWhiteSpace(command.Email))
        {
            Add("correo", "El correo electrónico es obligatorio.");
        }
        else
        {
            try
            {
                email = EmailAddress.Create(command.Email);
            }
            catch (ArgumentException)
            {
                Add("correo", "El correo electrónico no tiene un formato válido.");
            }
        }

        if (command.BirthDate is null || command.BirthDate == default)
        {
            Add("fechaNacimiento", "La fecha de nacimiento es obligatoria.");
        }
        else if (command.BirthDate > DateOnly.FromDateTime(now.UtcDateTime))
        {
            Add("fechaNacimiento", "La fecha de nacimiento debe ser una fecha pasada válida.");
        }

        if (command.Gender is null || !Enum.IsDefined(command.Gender.Value))
        {
            Add("genero", "El género es obligatorio.");
        }

        if (string.IsNullOrEmpty(command.Password))
        {
            Add("contrasena", "La contraseña es obligatoria.");
        }
        else if (command.Password.Length < MinPasswordLength)
        {
            Add("contrasena", $"La contraseña debe tener al menos {MinPasswordLength} caracteres.");
        }
        else if (command.Password.Length > MaxPasswordLength)
        {
            Add("contrasena", $"La contraseña no debe superar {MaxPasswordLength} caracteres.");
        }

        if (string.IsNullOrEmpty(command.ConfirmPassword))
        {
            Add("confirmarContrasena", "La confirmación de contraseña es obligatoria.");
        }
        else if (!string.Equals(command.Password, command.ConfirmPassword, StringComparison.Ordinal))
        {
            Add("confirmarContrasena", "Las contraseñas no coinciden.");
        }

        if (!command.AcceptedPrivacy)
        {
            Add("aceptoAvisoPrivacidad", "Debe aceptar el aviso de privacidad.");
        }

        if (string.IsNullOrWhiteSpace(command.PrivacyVersion))
        {
            Add("versionAvisoPrivacidad", "La versión del aviso de privacidad es obligatoria.");
        }
        else
        {
            var privacyVersion = command.PrivacyVersion.Trim();

            if (privacyVersion.Length > PrivacyAcceptance.MaxVersionLength ||
                privacyVersion.Any(char.IsControl))
            {
                Add(
                    "versionAvisoPrivacidad",
                    $"La versión del aviso de privacidad no debe superar {PrivacyAcceptance.MaxVersionLength} caracteres válidos.");
            }
        }

        var convertedErrors = errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToArray(),
            StringComparer.Ordinal);

        return (email, convertedErrors);
    }
}
