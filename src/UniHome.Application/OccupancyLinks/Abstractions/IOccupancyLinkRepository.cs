using UniHome.Domain.OccupancyLinks;

namespace UniHome.Application.OccupancyLinks.Abstractions;

public interface IOccupancyLinkRepository
{
    Task<OccupancyLink?> FindAsync(
        Guid tenantAccountId,
        Guid unitId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Activa o reactiva el único documento del par inquilino/unidad solo si no
    /// está activo. La transición es atómica en el almacenamiento; dos
    /// solicitudes simultáneas no pueden activar dos veces el mismo vínculo.
    /// Devuelve true únicamente a la solicitud que ganó la transición.
    /// </summary>
    Task<bool> TryActivateAsync(OccupancyLink link, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finaliza un vínculo solo si sigue activo. Devuelve true únicamente a la
    /// solicitud que ganó la transición; el resto no debe liberar cupo.
    /// </summary>
    Task<bool> TryEndActiveAsync(
        Guid tenantAccountId,
        Guid unitId,
        OccupancyEndReason reason,
        DateTimeOffset endedAt,
        CancellationToken cancellationToken = default);

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
