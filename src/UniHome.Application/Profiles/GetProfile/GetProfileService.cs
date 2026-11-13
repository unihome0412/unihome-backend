using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.Application.Profiles.GetProfile;

/// <summary>
/// EP-M02-01 (HU-002/HU-008): perfil propio con los datos de M01 y los
/// indicadores mínimos de navegación. M05/M06/M09 aún no existen; por eso la
/// etiqueta es UsuarioUniHome y el vínculo con unidad es falso hasta que esos
/// módulos aporten sus consultas. No se persisten etiquetas derivadas.
/// </summary>
public sealed class GetProfileService
{
    private readonly IAccountRepository _accounts;
    private readonly ISystemClock _clock;

    public GetProfileService(IAccountRepository accounts, ISystemClock clock)
    {
        _accounts = accounts;
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

        return new ProfileView(
            account,
            AccountLabel.UsuarioUniHome,
            IsLinkedToUnit: false,
            CanPublish: AgeEligibility.IsAdult(account.BirthDate, _clock.UtcNow));
    }
}
