using UniHome.Domain.Reparaciones;

namespace UniHome.Application.Reparaciones.Abstractions;

public interface IReparacionRepository
{
    Task AddAsync(Reparacion reparacion, CancellationToken cancellationToken = default);

    Task<Reparacion?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Reportes de la unidad, más recientes primero.</summary>
    Task<IReadOnlyList<Reparacion>> ListByUnitAsync(
        Guid unitId,
        CancellationToken cancellationToken = default);

    /// <summary>Reemplaza el documento completo; usado por HU-065 para persistir el cambio de estado.</summary>
    Task SaveAsync(Reparacion reparacion, CancellationToken cancellationToken = default);
}
