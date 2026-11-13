using UniHome.Application.Favorites.Abstractions;
using UniHome.Domain.Favorites;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class InMemoryFavoriteRepository : IUnitFavoriteRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Favorite> _items = new(StringComparer.Ordinal);

    public Task AddAsync(
        Guid accountId,
        Guid unitId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _items.TryAdd($"{accountId:N}|{unitId:N}", new Favorite(accountId, unitId, now));
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(
        Guid accountId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _items.Remove($"{accountId:N}|{unitId:N}");
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(
        Guid accountId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_items.ContainsKey($"{accountId:N}|{unitId:N}"));
        }
    }

    public Task<IReadOnlySet<Guid>> GetFavoriteUnitIdsAsync(
        Guid accountId,
        IReadOnlyCollection<Guid> unitIds,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlySet<Guid> ids = _items.Values
                .Where(favorite => favorite.AccountId == accountId &&
                                   unitIds.Contains(favorite.UnitId))
                .Select(favorite => favorite.UnitId)
                .ToHashSet();
            return Task.FromResult(ids);
        }
    }

    public Task<FavoritePage> ListAsync(
        Guid accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var all = _items.Values
                .Where(favorite => favorite.AccountId == accountId)
                .OrderByDescending(favorite => favorite.CreatedAt)
                .ToArray();
            var items = all
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToArray();
            return Task.FromResult(new FavoritePage(items, all.LongLength));
        }
    }
}
