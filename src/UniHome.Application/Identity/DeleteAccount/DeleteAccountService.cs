using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.Application.Identity.DeleteAccount;

/// <summary>
/// EP-M01-09 (HU-096). Decisión aprobada: exige Bearer, contraseña y
/// <c>confirmacionPerdidaInformacion = true</c>; borrado lógico
/// (<see cref="AccountStatus.Deleted"/>) y revocación de todas las sesiones de la
/// cuenta en todos los dispositivos (a diferencia de logout).
/// La validación 409 de vínculos de propiedades/ocupación (M05/M09) y la política
/// de retención/anonimización (ADR-005) quedan pendientes: no se implementan aquí.
/// </summary>
public sealed class DeleteAccountService
{
    private const string DeletionReason = "account_deleted";

    private readonly IAccountRepository _accounts;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshSessionRepository _sessions;
    private readonly ISystemClock _clock;

    public DeleteAccountService(
        IAccountRepository accounts,
        IPasswordHasher passwordHasher,
        IRefreshSessionRepository sessions,
        ISystemClock clock)
    {
        _accounts = accounts;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<DeleteAccountResult> DeleteAsync(
        DeleteAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.AccountId == Guid.Empty)
        {
            return DeleteAccountResult.AccountNotFound();
        }

        if (!command.ConfirmedDataLoss)
        {
            return DeleteAccountResult.ConfirmationMissing();
        }

        var account = await _accounts
            .FindByIdAsync(command.AccountId, cancellationToken)
            .ConfigureAwait(false);

        if (account is null || account.Status == AccountStatus.Deleted)
        {
            return DeleteAccountResult.AccountNotFound();
        }

        if (string.IsNullOrEmpty(command.Password) ||
            !_passwordHasher.VerifyPassword(account.PasswordHash, command.Password))
        {
            return DeleteAccountResult.IncorrectPassword();
        }

        var now = _clock.UtcNow;
        account.Delete(now);

        await _accounts.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
        await _sessions
            .RevokeAllForAccountAsync(account.Id, now, DeletionReason, cancellationToken)
            .ConfigureAwait(false);

        return DeleteAccountResult.Deleted();
    }
}
