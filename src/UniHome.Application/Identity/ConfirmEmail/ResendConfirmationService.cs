using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.Application.Identity.ConfirmEmail;

/// <summary>
/// EP-M01-03 (HU-003). Decisión aprobada: responde 202 sin cuerpo exista o no el
/// correo, para no revelar cuentas. El límite de reenvíos "por cuenta" lo aplica
/// <see cref="Account.CanResendEmailVerification"/> (ya definido por el dominio:
/// <see cref="VerificationCode.MaxResendsPerWindow"/> por <see cref="VerificationCode.ResendWindow"/>).
/// El límite "por IP" con 429 se aplica en la capa Api (rate limiter), no aquí.
/// </summary>
public sealed class ResendConfirmationService
{
    private readonly IAccountRepository _accounts;
    private readonly IVerificationCodeGenerator _codeGenerator;
    private readonly IVerificationCodeProtector _codeProtector;
    private readonly IVerificationEmailSender _emailSender;
    private readonly ISystemClock _clock;

    public ResendConfirmationService(
        IAccountRepository accounts,
        IVerificationCodeGenerator codeGenerator,
        IVerificationCodeProtector codeProtector,
        IVerificationEmailSender emailSender,
        ISystemClock clock)
    {
        _accounts = accounts;
        _codeGenerator = codeGenerator;
        _codeProtector = codeProtector;
        _emailSender = emailSender;
        _clock = clock;
    }

    public async Task<ResendConfirmationResult> ResendAsync(
        ResendConfirmationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        EmailAddress email;

        if (string.IsNullOrWhiteSpace(command.Email))
        {
            return ResendConfirmationResult.ValidationFailed(new Dictionary<string, string[]>(
                StringComparer.Ordinal)
            {
                ["correo"] = ["El correo electrónico es obligatorio."]
            });
        }

        try
        {
            email = EmailAddress.Create(command.Email);
        }
        catch (ArgumentException)
        {
            return ResendConfirmationResult.ValidationFailed(new Dictionary<string, string[]>(
                StringComparer.Ordinal)
            {
                ["correo"] = ["El correo electrónico no tiene un formato válido."]
            });
        }

        var account = await _accounts
            .FindByEmailAsync(email, cancellationToken)
            .ConfigureAwait(false);

        var now = _clock.UtcNow;

        if (account is null || !account.CanResendEmailVerification(now))
        {
            // No se revela si la cuenta existe, ya está confirmada o agotó el
            // límite de reenvíos: en todos los casos la respuesta es idéntica.
            return ResendConfirmationResult.Accepted();
        }

        var rawCode = _codeGenerator.Generate();
        var protectedCode = _codeProtector.Protect(rawCode);
        account.ResendEmailVerification(protectedCode, now);

        await _accounts.UpdateAsync(account, cancellationToken).ConfigureAwait(false);

        try
        {
            await _emailSender
                .SendAsync(email, rawCode, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // El verificador ya quedó persistido; un fallo de envío no debe
            // revelar nada distinto al llamador ni registrar el código/correo.
        }

        return ResendConfirmationResult.Accepted();
    }
}
