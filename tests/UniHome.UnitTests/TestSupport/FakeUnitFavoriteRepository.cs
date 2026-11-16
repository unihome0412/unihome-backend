using UniHome.Application.Favorites.Abstractions;
using UniHome.Domain.Favorites;

namespace UniHome.UnitTests.TestSupport;

internal sealed class FakeUnitFavoriteRepository : IUnitFavoriteRepository
{
    public List<Favorite> Items { get; } = [];

    public Task AddAsync(
        Guid accountId,
        Guid unitId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (!Items.Any(item => item.AccountId == accountId && item.UnitId == unitId))
        {
            Items.Add(new Favorite(accountId, unitId, now));
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(
        Guid accountId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        Items.RemoveAll(item => item.AccountId == accountId && item.UnitId == unitId);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(
        Guid accountId,
        Guid unitId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(item =>
            item.AccountId == accountId && item.UnitId == unitId));

    public Task<IReadOnlySet<Guid>> GetFavoriteUnitIdsAsync(
        Guid accountId,
        IReadOnlyCollection<Guid> unitIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<Guid>>(Items
            .Where(item => item.AccountId == accountId && unitIds.Contains(item.UnitId))
            .Select(item => item.UnitId)
            .ToHashSet());

    public Task<FavoritePage> ListAsync(
        Guid accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var all = Items
            .Where(item => item.AccountId == accountId)
            .OrderByDescending(item => item.CreatedAt)
            .ToArray();
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        return Task.FromResult(new FavoritePage(items, all.LongLength));
    }
}
