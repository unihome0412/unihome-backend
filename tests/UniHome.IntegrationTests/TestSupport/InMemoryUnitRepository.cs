using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Units;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class InMemoryUnitRepository : IUnitRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Unit> _units = [];

    public IReadOnlyList<Unit> All
    {
        get
        {
            lock (_gate)
            {
                return _units.Values.ToArray();
            }
        }
    }

    public Task AddAsync(Unit unit, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _units[unit.Id] = unit;
        }

        return Task.CompletedTask;
    }

    public Task<Unit?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _units.TryGetValue(id, out var unit);
            return Task.FromResult(unit);
        }
    }

    public Task<Unit?> FindByPhotoStorageKeyAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_units.Values.SingleOrDefault(unit =>
                unit.Photos.Any(photo => photo.StorageKey == storageKey)));
        }
    }

    public Task<bool> UpdateServicesAsync(
        Guid id,
        Guid propertyId,
        IReadOnlyList<ServiceCharge> services,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_units.TryGetValue(id, out var unit) || unit.PropertyId != propertyId)
            {
                return Task.FromResult(false);
            }

            unit.ConfigureServices(services);
            return Task.FromResult(true);
        }
    }

    public Task<bool> UpdateZoneRulesAsync(
        Guid id,
        Guid propertyId,
        string? zoneInfo,
        string? rules,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_units.TryGetValue(id, out var unit) || unit.PropertyId != propertyId)
            {
                return Task.FromResult(false);
            }

            unit.ConfigureZoneRules(zoneInfo, rules);
            return Task.FromResult(true);
        }
    }

    public Task<bool> TryAppendPhotosAsync(
        Guid id,
        Guid propertyId,
        int expectedPhotoCount,
        IReadOnlyList<UnitPhoto> photos,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_units.TryGetValue(id, out var unit) ||
                unit.PropertyId != propertyId ||
                unit.Photos.Count != expectedPhotoCount)
            {
                return Task.FromResult(false);
            }

            unit.AppendPhotos(photos);
            return Task.FromResult(true);
        }
    }
}
