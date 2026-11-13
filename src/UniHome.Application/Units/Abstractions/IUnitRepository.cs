using UniHome.Domain.Units;

namespace UniHome.Application.Units.Abstractions;

public interface IUnitRepository
{
    Task AddAsync(Unit unit, CancellationToken cancellationToken = default);

    Task<Unit?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Unit?> FindByPhotoStorageKeyAsync(
        string storageKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Unit?>(null);

    Task<bool> UpdateServicesAsync(
        Guid id,
        Guid propertyId,
        IReadOnlyList<ServiceCharge> services,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateZoneRulesAsync(
        Guid id,
        Guid propertyId,
        string? zoneInfo,
        string? rules,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Añade fotos solo si el número actual coincide con el esperado. Evita que
    /// dos cargas concurrentes superen el máximo de 10.
    /// </summary>
    Task<bool> TryAppendPhotosAsync(
        Guid id,
        Guid propertyId,
        int expectedPhotoCount,
        IReadOnlyList<UnitPhoto> photos,
        CancellationToken cancellationToken = default);
}
