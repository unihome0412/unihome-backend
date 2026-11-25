using UniHome.Application.Reparaciones.Abstractions;
using UniHome.Domain.Reparaciones;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class InMemoryReparacionRepository : IReparacionRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Reparacion> _items = [];

    public Task AddAsync(Reparacion reparacion, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _items[reparacion.Id] = reparacion;
        }

        return Task.CompletedTask;
    }

    public Task<Reparacion?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _items.TryGetValue(id, out var reparacion);
            return Task.FromResult(reparacion);
        }
    }

    public Task<IReadOnlyList<Reparacion>> ListByUnitAsync(
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<Reparacion> items = _items.Values
                .Where(item => item.UnitId == unitId)
                .OrderByDescending(item => item.CreatedAt)
                .ToArray();
            return Task.FromResult(items);
        }
    }

    public Task SaveAsync(Reparacion reparacion, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _items[reparacion.Id] = reparacion;
        }

        return Task.CompletedTask;
    }
}
