using UniHome.Application.Identity.Abstractions;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.Application.Identity.DeleteAccount;

/// <summary>
/// EP-M01-09 (HU-096). Decisión aprobada: exige Bearer, contraseña y
/// <c>confirmacionPerdidaInformacion = true</c>; borrado lógico
/// (<see cref="AccountStatus.Deleted"/>) y revocación de todas las sesiones de la
/// cuenta en todos los dispositivos (a diferencia de logout).
/// Rechaza con 409 si la cuenta tiene un vínculo de ocupación activo como
/// inquilino (M09) o si alguna de sus propiedades todavía tiene inquilinos
/// activos: en ambos casos hay otra persona cuya ocupación depende de esta
/// cuenta, y borrarla dejaría ese vínculo colgado. Un administrador (ARQ-004)
/// no puede eliminar su propia cuenta por esta vía.
/// </summary>
public sealed class DeleteAccountService
{
    private const string DeletionReason = "account_deleted";

    private readonly IAccountRepository _accounts;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshSessionRepository _sessions;
    private readonly IOccupancyLinkRepository _occupancyLinks;
    private readonly IUnitRepository _units;
    private readonly IAdminAccessChecker _adminAccess;
    private readonly ISystemClock _clock;

    public DeleteAccountService(
        IAccountRepository accounts,
        IPasswordHasher passwordHasher,
        IRefreshSessionRepository sessions,
        IOccupancyLinkRepository occupancyLinks,
        IUnitRepository units,
        IAdminAccessChecker adminAccess,
        ISystemClock clock)
    {
        _accounts = accounts;
        _passwordHasher = passwordHasher;
        _sessions = sessions;
        _occupancyLinks = occupancyLinks;
        _units = units;
        _adminAccess = adminAccess;
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

        if (_adminAccess.IsAdmin(account.Id))
        {
            return DeleteAccountResult.AdminCannotSelfDelete();
        }

        if (await _occupancyLinks.HasActiveLinkAsync(account.Id, cancellationToken).ConfigureAwait(false))
        {
            return DeleteAccountResult.ActiveOccupancyLinks();
        }

        var ownedUnits = await _units.ListByOwnerAsync(account.Id, cancellationToken).ConfigureAwait(false);

        foreach (var unit in ownedUnits)
        {
            var activeTenants = await _occupancyLinks
                .ListActiveByUnitAsync(unit.Id, cancellationToken)
                .ConfigureAwait(false);

            if (activeTenants.Count > 0)
            {
                return DeleteAccountResult.PropertiesWithTenants();
            }
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
