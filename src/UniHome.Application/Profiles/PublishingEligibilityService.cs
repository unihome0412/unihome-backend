using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.Application.Profiles;

/// <summary>
/// Revalida en servidor que la cuenta esté habilitada y sea mayor de edad antes de
/// crear o modificar recursos de publicación. La edad nunca se toma del request.
/// </summary>
public sealed class PublishingEligibilityService
{
    private readonly IAccountRepository _accounts;
    private readonly ISystemClock _clock;

    public PublishingEligibilityService(IAccountRepository accounts, ISystemClock clock)
    {
        _accounts = accounts;
        _clock = clock;
    }

    public async Task<bool> IsEligibleAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _accounts
            .FindByIdAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        return account is not null &&
               account.Status == AccountStatus.Active &&
               account.IsEmailConfirmed &&
               AgeEligibility.IsAdult(account.BirthDate, _clock.UtcNow);
    }
}
