using UniHome.Domain.Units;

namespace UniHome.Application.Units.Abstractions;

public interface IUnitRepository
{
    Task AddAsync(Unit unit, CancellationToken cancellationToken = default);

    Task<Unit?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Primera publicación del titular: unidad más antigua por fecha e id.</summary>
    Task<Unit?> FindFirstOwnedAsync(
        Guid ownerAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>Todas las unidades del titular, ordenadas por fecha de creación.</summary>
    Task<IReadOnlyList<Unit>> ListByOwnerAsync(
        Guid ownerAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transición administrativa de publicación con CAS sobre el estado esperado.
    /// Solo se usa desde revisión a aprobada/rechazada; devuelve false si el estado
    /// o el titular no coinciden.
    /// </summary>
    Task<bool> TryTransitionPublicationStateAsync(
        Guid id,
        Guid ownerAccountId,
        Domain.Properties.PublicationState expected,
        Domain.Properties.PublicationState next,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lleva una unidad a En revisión si pertenece al titular y no está aprobada.
    /// Es idempotente si ya estaba En revisión. Devuelve false si no cumple.
    /// </summary>
    Task<bool> TrySubmitForReviewAsync(
        Guid id,
        Guid ownerAccountId,
        CancellationToken cancellationToken = default);

    Task<Unit?> FindByPhotoStorageKeyAsync(
        string storageKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Unit?>(null);

    Task<IReadOnlyList<Unit>> FindByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Candidatos públicos de M04: publicación Aprobada y disponibilidad
    /// Disponible (los documentos anteriores a M09 sin campo se consideran
    /// disponibles). Precio/ocupación/paraQuién se filtran en MongoDB.
    /// </summary>
    Task<IReadOnlyList<Unit>> ListPublicCandidatesAsync(
        Guid universityId,
        decimal? maxPriceMxn,
        bool onlyIndividual,
        ForWhom? forWhom,
        CancellationToken cancellationToken = default);
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

    /// <summary>
    /// EP-M09 (HU-093/HU-094/HU-095): persiste la disponibilidad recalculada por
    /// los servicios de ocupación (mantenimiento manual o conteo de vínculos
    /// activos frente a los lugares de la unidad).
    /// </summary>
    Task<bool> UpdateAvailabilityAsync(
        Guid id,
        Guid propertyId,
        UnitAvailability availability,
        CancellationToken cancellationToken = default);
}
