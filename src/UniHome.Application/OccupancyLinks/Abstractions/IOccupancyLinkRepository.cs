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

    Task<int> CountActiveAsync(Guid unitId, CancellationToken cancellationToken = default);

    Task<bool> HasActiveLinkAsync(Guid tenantAccountId, CancellationToken cancellationToken = default);
}
