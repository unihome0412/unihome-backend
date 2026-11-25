using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.Application.Identity.Admin;

/// <summary>
/// EP-M13-01 (HU-072): el administrador suspende o reactiva la cuenta de un
/// usuario. Suspender revoca todas sus sesiones activas, igual que la
/// eliminación de cuenta (HU-096): una cuenta suspendida no debe seguir
/// operando con un token ya emitido.
/// </summary>
public sealed class SetAccountStatusService
{
    private const string SuspensionReason = "account_suspended_by_admin";

    private readonly IAccountRepository _accounts;
    private readonly IRefreshSessionRepository _sessions;
    private readonly ISystemClock _clock;

    public SetAccountStatusService(
        IAccountRepository accounts,
        IRefreshSessionRepository sessions,
        ISystemClock clock)
    {
        _accounts = accounts;
        _sessions = sessions;
        _clock = clock;
    }

    public async Task<OperationResult<Account>> SetStatusAsync(
        Guid targetAccountId,
        bool suspender,
        CancellationToken cancellationToken = default)
    {
        var account = await _accounts.FindByIdAsync(targetAccountId, cancellationToken).ConfigureAwait(false);

        if (account is null)
        {
            return OperationResult<Account>.NotFound();
        }

        try
        {
            if (suspender)
            {
                account.Suspend();
            }
            else
            {
                account.Reactivate();
            }
        }
        catch (InvalidOperationException exception)
        {
            return OperationResult<Account>.ValidationFailed("estado", exception.Message);
        }

        await _accounts.UpdateAsync(account, cancellationToken).ConfigureAwait(false);

        if (suspender)
        {
            await _sessions
                .RevokeAllForAccountAsync(account.Id, _clock.UtcNow, SuspensionReason, cancellationToken)
                .ConfigureAwait(false);
        }

        return OperationResult<Account>.Success(account);
    }
}
