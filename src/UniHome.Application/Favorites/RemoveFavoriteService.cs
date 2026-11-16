using UniHome.Application.Common;
using UniHome.Application.Favorites.Abstractions;

namespace UniHome.Application.Favorites;

/// <summary>EP-M07-02 (HU-085): baja idempotente de favorito.</summary>
public sealed class RemoveFavoriteService
{
    private readonly IUnitFavoriteRepository _favorites;

    public RemoveFavoriteService(IUnitFavoriteRepository favorites)
    {
        _favorites = favorites;
    }

    public async Task<OperationResult<Guid>> RemoveAsync(
        Guid accountId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        await _favorites
            .RemoveAsync(accountId, unitId, cancellationToken)
            .ConfigureAwait(false);
        return OperationResult<Guid>.Success(unitId);
    }
}
