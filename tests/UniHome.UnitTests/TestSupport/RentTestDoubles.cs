using UniHome.Application.Rentals.Abstractions;
using UniHome.Domain.Rentals;

namespace UniHome.UnitTests.TestSupport;

internal sealed class FakeRentPeriodRepository : IRentPeriodRepository
{
    public List<RentPeriod> Items { get; } = [];

    public Task<RentPeriod?> FindAsync(
        Guid unitId,
        string periodo,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.SingleOrDefault(
            period => period.UnitId == unitId && period.Periodo == periodo));

    public Task<RentPeriod?> FindMostRecentBeforeAsync(
        Guid unitId,
        string periodo,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items
            .Where(period => period.UnitId == unitId &&
                              RentPeriod.ComparePeriodo(period.Periodo, periodo) < 0)
            .OrderByDescending(period => period.Periodo, StringComparer.Ordinal)
            .FirstOrDefault());

    public Task SaveAsync(RentPeriod period, CancellationToken cancellationToken = default)
    {
        Items.RemoveAll(item => item.Id == period.Id);
        Items.Add(period);
        return Task.CompletedTask;
    }
}
