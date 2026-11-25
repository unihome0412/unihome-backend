using UniHome.Application.Rentals.Abstractions;
using UniHome.Domain.Rentals;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class InMemoryRentPeriodRepository : IRentPeriodRepository
{
    private readonly object _gate = new();
    private readonly List<RentPeriod> _periods = [];

    public Task<RentPeriod?> FindAsync(
        Guid unitId,
        string periodo,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_periods.SingleOrDefault(
                period => period.UnitId == unitId && period.Periodo == periodo));
        }
    }

    public Task<RentPeriod?> FindMostRecentBeforeAsync(
        Guid unitId,
        string periodo,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var previous = _periods
                .Where(period => period.UnitId == unitId &&
                                  RentPeriod.ComparePeriodo(period.Periodo, periodo) < 0)
                .OrderByDescending(period => period.Periodo, StringComparer.Ordinal)
                .FirstOrDefault();
            return Task.FromResult(previous);
        }
    }

    public Task<IReadOnlyList<RentPeriod>> ListByUnitAsync(
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<RentPeriod> items = _periods
                .Where(period => period.UnitId == unitId)
                .OrderByDescending(period => period.Periodo, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(items);
        }
    }

    public Task SaveAsync(RentPeriod period, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _periods.RemoveAll(item => item.Id == period.Id);
            _periods.Add(period);
        }

        return Task.CompletedTask;
    }
}
