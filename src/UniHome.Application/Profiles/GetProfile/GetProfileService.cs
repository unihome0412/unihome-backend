using UniHome.Application.Identity.Abstractions;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Accounts;
using UniHome.Domain.Properties;

namespace UniHome.Application.Profiles.GetProfile;

/// <summary>
/// EP-M02-01 (HU-002/HU-008/HU-009): perfil propio con datos de M01 y los
/// indicadores de navegación calculados a partir de las publicaciones reales.
/// La etiqueta y el vínculo con unidad son derivados; no se persisten.
/// </summary>
public sealed class GetProfileService
{
    private readonly IAccountRepository _accounts;
    private readonly IUnitRepository _units;
    private readonly IOccupancyLinkRepository _occupancyLinks;
    private readonly ISystemClock _clock;

    public GetProfileService(
        IAccountRepository accounts,
        IUnitRepository units,
        IOccupancyLinkRepository occupancyLinks,
        ISystemClock clock)
    {
        _accounts = accounts;
        _units = units;
        _occupancyLinks = occupancyLinks;
        _clock = clock;
    }

    public async Task<ProfileView?> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _accounts
            .FindByIdAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        if (account is null ||
            account.Status != AccountStatus.Active ||
            !account.IsEmailConfirmed)
        {
            return null;
        }

        var units = await _units
            .ListByOwnerAsync(accountId, cancellationToken)
            .ConfigureAwait(false);
        var label = units.Any(unit => unit.PublicationState == PublicationState.Aprobada)
            ? AccountLabel.ConEspaciosPublicados
            : units.Any(unit => unit.PublicationState == PublicationState.EnRevision)
                ? AccountLabel.PublicacionEnRevision
                : AccountLabel.UsuarioUniHome;
        var isLinkedToUnit = await _occupancyLinks
            .HasActiveLinkAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        return new ProfileView(
            account,
            label,
            IsLinkedToUnit: isLinkedToUnit,
            CanPublish: units.Count == 0 &&
                        AgeEligibility.IsAdult(account.BirthDate, _clock.UtcNow),
            FirstPublicationState: units.FirstOrDefault()?.PublicationState);
    }
}
