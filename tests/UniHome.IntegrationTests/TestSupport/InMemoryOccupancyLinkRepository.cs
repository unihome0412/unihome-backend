using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Domain.OccupancyLinks;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class InMemoryOccupancyLinkRepository : IOccupancyLinkRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<(Guid Tenant, Guid Unit), OccupancyLink> _links = [];

    public Task<OccupancyLink?> FindAsync(
        Guid tenantAccountId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _links.TryGetValue((tenantAccountId, unitId), out var link);
            return Task.FromResult(link);
        }
    }

    public Task SaveAsync(OccupancyLink link, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _links[(link.TenantAccountId, link.UnitId)] = link;
        }

        return Task.CompletedTask;
    }

    public Task<int> CountActiveAsync(Guid unitId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_links.Values.Count(
                link => link.UnitId == unitId && link.Status == OccupancyStatus.Activo));
        }
    }

    public Task<bool> HasActiveLinkAsync(
        Guid tenantAccountId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_links.Values.Any(
                link => link.TenantAccountId == tenantAccountId &&
                        link.Status == OccupancyStatus.Activo));
        }
    }
}
