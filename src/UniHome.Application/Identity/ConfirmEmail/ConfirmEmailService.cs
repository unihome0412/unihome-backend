using System.Text.RegularExpressions;
using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.Application.Identity.ConfirmEmail;

/// <summary>
/// EP-M01-02 (HU-003). Decisión aprobada: recibe correo y código en el cuerpo,
/// sin token de sesión de registro (ese contrato no está definido en la spec).
/// Correo inexistente, código sin emitir, código incorrecto y código ya usado
/// responden igual que un código incorrecto; un código vencido o con intentos
/// agotados responde distinto (410) porque exige un reenvío, no por existencia.
/// </summary>
public sealed class ConfirmEmailService
{
    private static readonly Regex CodePattern = new(
        $"^[0-9]{{{VerificationCode.Digits}}}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IAccountRepository _accounts;
    private readonly IVerificationCodeProtector _codeProtector;
    private readonly ISystemClock _clock;

    public ConfirmEmailService(
        IAccountRepository accounts,
        IVerificationCodeProtector codeProtector,
        ISystemClock clock)
    {
        _accounts = accounts;
        _codeProtector = codeProtector;
        _clock = clock;
    }

    public async Task<ConfirmEmailResult> ConfirmAsync(
        ConfirmEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var (email, errors) = Validate(command);

        if (email is null || errors.Count > 0)
        {
            return ConfirmEmailResult.ValidationFailed(errors);
        }

        var account = await _accounts
            .FindByEmailAsync(email, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            // Mismo resultado que un código incorrecto: no revela si la cuenta existe.
            return ConfirmEmailResult.IncorrectOrUnknown();
        }

        var now = _clock.UtcNow;
        var protectedCode = _codeProtector.Protect(command.Code!);
        var result = account.ConfirmEmail(protectedCode, now);

        switch (result)
        {
            case EmailConfirmationResult.AlreadyConfirmed:
                // Idempotente: no hay mutación que persistir.
                return ConfirmEmailResult.AlreadyConfirmed();

            case EmailConfirmationResult.NotIssued:
                // Nada que persistir: no existe verificador para esta cuenta.
                return ConfirmEmailResult.IncorrectOrUnknown();

            case EmailConfirmationResult.Confirmed:
                await _accounts.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
                return ConfirmEmailResult.Confirmed();

            case EmailConfirmationResult.ExpiredCode:
                await _accounts.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
                return ConfirmEmailResult.ExpiredCode();

            default:
                // IncorrectCode / AlreadyUsed: se persiste el intento fallido
                // registrado por Account.ConfirmEmail (límite por cuenta).
                await _accounts.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
                return ConfirmEmailResult.IncorrectOrUnknown();
        }
    }

    private static (EmailAddress? Email, IReadOnlyDictionary<string, string[]> Errors) Validate(
        ConfirmEmailCommand command)
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

        if (string.IsNullOrWhiteSpace(command.Code))
        {
            Add("codigo", "El código de confirmación es obligatorio.");
        }
        else if (!CodePattern.IsMatch(command.Code))
        {
            Add("codigo", $"El código debe tener exactamente {VerificationCode.Digits} dígitos.");
        }

        var convertedErrors = errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToArray(),
            StringComparer.Ordinal);

        return (email, convertedErrors);
    }
}
