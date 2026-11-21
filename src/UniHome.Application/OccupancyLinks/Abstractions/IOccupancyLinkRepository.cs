using UniHome.Domain.OccupancyLinks;

namespace UniHome.Application.OccupancyLinks.Abstractions;

public interface IOccupancyLinkRepository
{
    Task<OccupancyLink?> FindAsync(
        Guid tenantAccountId,
        Guid unitId,
        CancellationToken cancellationToken = default);

    /// <summary>Inserta o reemplaza el único documento del par inquilino/unidad.</summary>
    Task SaveAsync(OccupancyLink link, CancellationToken cancellationToken = default);

    Task<bool> HasActiveLinkAsync(Guid tenantAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// EP-M09-06 (HU-094): inquilinos activos de una unidad propia, para que el
    /// propietario obtenga el <c>tenantAccountId</c> antes de dar de baja a
    /// alguien. No se usa para contar cupo: esa garantía la da
    /// <c>IUnitRepository.TryReserveSlotAsync</c> sobre la propia unidad.
    /// </summary>
    Task<IReadOnlyList<OccupancyLink>> ListActiveByUnitAsync(
        Guid unitId,
        CancellationToken cancellationToken = default);
}
