using UniHome.Application.Reparaciones.Abstractions;
using UniHome.Domain.Reparaciones;

namespace UniHome.UnitTests.TestSupport;

internal sealed class FakeReparacionRepository : IReparacionRepository
{
    public List<Reparacion> Items { get; } = [];

    public Exception? AddException { get; set; }

    public Task AddAsync(Reparacion reparacion, CancellationToken cancellationToken = default)
    {
        if (AddException is not null)
        {
            throw AddException;
        }

        Items.Add(reparacion);
        return Task.CompletedTask;
    }

    public Task<Reparacion?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.SingleOrDefault(item => item.Id == id));

    public Task<IReadOnlyList<Reparacion>> ListByUnitAsync(
        Guid unitId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Reparacion>>(Items
            .Where(item => item.UnitId == unitId)
            .OrderByDescending(item => item.CreatedAt)
            .ToArray());

    public Task SaveAsync(Reparacion reparacion, CancellationToken cancellationToken = default)
    {
        Items.RemoveAll(item => item.Id == reparacion.Id);
        Items.Add(reparacion);
        return Task.CompletedTask;
    }
}
