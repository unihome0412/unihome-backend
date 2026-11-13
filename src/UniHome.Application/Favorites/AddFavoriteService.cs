using UniHome.Application.Common;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Favorites.Abstractions;
using UniHome.Application.Identity.Abstractions;

namespace UniHome.Application.Favorites;

/// <summary>EP-M07-01 (HU-085): alta idempotente de favorito.</summary>
public sealed class AddFavoriteService
{
    private readonly IUnitRepository _units;
    private readonly IUnitFavoriteRepository _favorites;
    private readonly ISystemClock _clock;

    public AddFavoriteService(
        IUnitRepository units,
        IUnitFavoriteRepository favorites,
        ISystemClock clock)
    {
        _units = units;
        _favorites = favorites;
        _clock = clock;
    }

    public async Task<OperationResult<Guid>> AddAsync(
        Guid accountId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<Guid>.NotFound();
        }

        await _favorites
            .AddAsync(accountId, unitId, _clock.UtcNow, cancellationToken)
            .ConfigureAwait(false);
        return OperationResult<Guid>.Success(unitId);
    }
}
