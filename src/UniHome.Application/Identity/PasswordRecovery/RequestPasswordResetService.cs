using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.Application.Identity.PasswordRecovery;

/// <summary>
/// EP-M01-06 (HU-006). Solicita una recuperación y responde siempre de forma
/// genérica, exista o no la cuenta. Solo emite token para cuentas activas con el
/// correo confirmado; el token se guarda como hash y se invalida la solicitud previa.
/// El límite por IP se aplica en Api y no se mezcla con esta regla de negocio.
/// </summary>
public sealed class RequestPasswordResetService
{
    private readonly IAccountRepository _accounts;
    private readonly IPasswordResetRepository _resets;
    private readonly IPasswordResetTokenGenerator _tokenGenerator;
    private readonly IPasswordResetEmailSender _emailSender;
    private readonly ISystemClock _clock;

    public RequestPasswordResetService(
        IAccountRepository accounts,
        IPasswordResetRepository resets,
        IPasswordResetTokenGenerator tokenGenerator,
        IPasswordResetEmailSender emailSender,
        ISystemClock clock)
    {
        _accounts = accounts;
        _resets = resets;
        _tokenGenerator = tokenGenerator;
        _emailSender = emailSender;
        _clock = clock;
    }

    public async Task<RequestPasswordResetResult> RequestAsync(
        RequestPasswordResetCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var (email, errors) = Validate(command);

        if (email is null || errors.Count > 0)
        {
            return RequestPasswordResetResult.ValidationFailed(errors);
        }

        var account = await _accounts
            .FindByEmailAsync(email, cancellationToken)
            .ConfigureAwait(false);

        if (account is null ||
            account.Status != AccountStatus.Active ||
            !account.IsEmailConfirmed)
        {
            // La respuesta no distingue entre inexistente, suspendida, eliminada
            // o no confirmada. Tampoco se envía correo.
            return RequestPasswordResetResult.Accepted();
        }

        var now = _clock.UtcNow;
        var generated = _tokenGenerator.Generate();
        var token = PasswordResetToken.Issue(
            generated.Id,
            account.Id,
            generated.Hash,
            now);

        await _resets
            .InvalidateAllForAccountAsync(account.Id, now, cancellationToken)
            .ConfigureAwait(false);

        await _resets
            .AddAsync(token, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await _emailSender
                .SendAsync(email, generated.Value, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // El token ya quedó persistido; un fallo de envío no debe revelar
            // nada distinto al llamador ni registrar el token o el correo.
        }

        return RequestPasswordResetResult.Accepted();
    }

    private static (EmailAddress? Email, IReadOnlyDictionary<string, string[]> Errors) Validate(
        RequestPasswordResetCommand command)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(command.Email))
        {
            errors["correo"] = ["El correo electrónico es obligatorio."];
            return (null, errors);
        }

        try
        {
            return (EmailAddress.Create(command.Email), errors);
        }
        catch (ArgumentException)
        {
            errors["correo"] = ["El correo electrónico no tiene un formato válido."];
            return (null, errors);
        }
    }
}
