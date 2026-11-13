using UniHome.Domain.Favorites;

namespace UniHome.Application.Favorites.Abstractions;

public sealed record FavoritePage(IReadOnlyList<Favorite> Items, long Total);

public interface IUnitFavoriteRepository
{
    /// <summary>Alta idempotente; no duplica por cuenta/unidad.</summary>
    Task AddAsync(
        Guid accountId,
        Guid unitId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>Baja idempotente; no falla si el favorito no existía.</summary>
    Task RemoveAsync(
        Guid accountId,
        Guid unitId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid accountId,
        Guid unitId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetFavoriteUnitIdsAsync(
        Guid accountId,
        IReadOnlyCollection<Guid> unitIds,
        CancellationToken cancellationToken = default);

    Task<FavoritePage> ListAsync(
        Guid accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
