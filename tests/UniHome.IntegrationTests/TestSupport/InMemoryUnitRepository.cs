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

    public Task<Unit?> FindFirstOwnedAsync(
        Guid ownerAccountId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var unit = _units.Values
                .Where(candidate => candidate.OwnerAccountId == ownerAccountId)
                .OrderBy(candidate => candidate.CreatedAt)
                .ThenBy(candidate => candidate.Id)
                .FirstOrDefault();

            return Task.FromResult(unit);
        }
    }

    public Task<bool> TrySubmitForReviewAsync(
        Guid id,
        Guid ownerAccountId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_units.TryGetValue(id, out var unit) ||
                unit.OwnerAccountId != ownerAccountId ||
                unit.PublicationState == PublicationState.Aprobada)
            {
                return Task.FromResult(false);
            }

            unit.SubmitForReview();
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<Unit>> ListByOwnerAsync(
        Guid ownerAccountId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<Unit> items = _units.Values
                .Where(unit => unit.OwnerAccountId == ownerAccountId)
                .OrderBy(unit => unit.CreatedAt)
                .ThenBy(unit => unit.Id)
                .ToArray();
            return Task.FromResult(items);
        }
    }

    public Task<bool> TryTransitionPublicationStateAsync(
        Guid id,
        Guid ownerAccountId,
        PublicationState expected,
        PublicationState next,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_units.TryGetValue(id, out var unit) ||
                unit.OwnerAccountId != ownerAccountId)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(unit.TryApplyPublicationTransition(expected, next));
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

    public Task<bool> UpdateAvailabilityAsync(
        Guid id,
        Guid propertyId,
        UnitAvailability availability,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_units.TryGetValue(id, out var unit) || unit.PropertyId != propertyId)
            {
                return Task.FromResult(false);
            }

            unit.SetAvailability(availability);
            return Task.FromResult(true);
        }
    }

    public Task<int?> TryReserveSlotAsync(
        Guid id,
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_units.TryGetValue(id, out var unit) ||
                unit.PropertyId != propertyId ||
                unit.OccupiedPlaces >= unit.Places)
            {
                return Task.FromResult<int?>(null);
            }

            unit.SetOccupiedPlaces(unit.OccupiedPlaces + 1);
            return Task.FromResult<int?>(unit.OccupiedPlaces);
        }
    }

    public Task<int> ReleaseSlotAsync(
        Guid id,
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_units.TryGetValue(id, out var unit) ||
                unit.PropertyId != propertyId ||
                unit.OccupiedPlaces <= 0)
            {
                return Task.FromResult(_units.TryGetValue(id, out var current) ? current.OccupiedPlaces : 0);
            }

            unit.SetOccupiedPlaces(unit.OccupiedPlaces - 1);
            return Task.FromResult(unit.OccupiedPlaces);
        }
    }
}
