using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Properties;
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

    public Task<IReadOnlyList<Unit>> FindByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<Unit> items = _units
                .Where(pair => ids.Contains(pair.Key))
                .Select(pair => pair.Value)
                .ToArray();
            return Task.FromResult(items);
        }
    }

    public Task<IReadOnlyList<Unit>> ListPublicCandidatesAsync(
        Guid universityId,
        decimal? maxPriceMxn,
        bool onlyIndividual,
        ForWhom? forWhom,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<Unit> items = _units.Values
                .Where(unit => unit.UniversityId == universityId)
                .Where(unit => unit.PublicationState == PublicationState.Aprobada)
                .Where(unit => unit.Availability == UnitAvailability.Disponible)
                .Where(unit => maxPriceMxn is null || unit.PricePerPlace <= maxPriceMxn)
                .Where(unit => !onlyIndividual || unit.Occupancy == Occupancy.Individual)
                .Where(unit => forWhom is null || unit.ForWhom == forWhom)
                .OrderBy(unit => unit.PricePerPlace)
                .ThenBy(unit => unit.Id)
                .ToArray();
            return Task.FromResult(items);
        }
    }

    public void Replace(Unit unit)
    {
        lock (_gate)
        {
            _units[unit.Id] = unit;
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
