using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.Application.Profiles;

/// <summary>
/// EP-M02/M03 (HU-087): fija la universidad actual de la cuenta, elegida
/// desde el perfil o desde el mapa (el backend expone un único endpoint;
/// ambos puntos de entrada del cliente lo invocan igual). Solo acepta una
/// universidad activa del catálogo: una inactiva ya no debería ofrecerse
/// como destino nuevo, aunque siga vigente para cuentas que ya la tenían
/// fijada desde antes.
/// </summary>
public sealed class ChangeMyUniversityService
{
    private readonly IAccountRepository _accounts;
    private readonly IUniversityRepository _universities;

    public ChangeMyUniversityService(IAccountRepository accounts, IUniversityRepository universities)
    {
        _accounts = accounts;
        _universities = universities;
    }

    public async Task<OperationResult<Account>> ChangeAsync(
        Guid accountId,
        Guid universityId,
        CancellationToken cancellationToken = default)
    {
        var account = await _accounts.FindByIdAsync(accountId, cancellationToken).ConfigureAwait(false);

        if (account is null || account.Status != AccountStatus.Active)
        {
            return OperationResult<Account>.NotFound();
        }

        var university = await _universities
            .FindByIdAsync(universityId, cancellationToken)
            .ConfigureAwait(false);

        if (university is null || !university.IsActive)
        {
            return OperationResult<Account>.ValidationFailed(
                "universidadId", "La universidad indicada no existe o no está activa.");
        }

        try
        {
            account.SetCurrentUniversity(universityId);
        }
        catch (ArgumentException exception)
        {
            return OperationResult<Account>.ValidationFailed("universidadId", exception.Message);
        }

        await _accounts.UpdateAsync(account, cancellationToken).ConfigureAwait(false);

        return OperationResult<Account>.Success(account);
    }
}
