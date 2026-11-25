using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Notifications.Abstractions;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.Reparaciones.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Notifications;
using UniHome.Domain.Reparaciones;

namespace UniHome.Application.Reparaciones;

/// <summary>
/// EP-M11-03 (HU-065): el propietario cambia el estado de un reporte de
/// reparación de su unidad. HU-066: notifica a todos los demás con acceso a
/// la unidad (el propietario y los inquilinos con vínculo activo), nunca a
/// quien hizo el cambio. A diferencia del recordatorio de renta (HU-059), la
/// notificación se genera aquí mismo, en el momento del cambio, no al
/// consultar <c>GET /notificaciones</c>.
/// </summary>
public sealed class ChangeReparacionStatusService
{
    private readonly IUnitRepository _units;
    private readonly IOccupancyLinkRepository _links;
    private readonly IReparacionRepository _reparaciones;
    private readonly INotificationRepository _notifications;
    private readonly ISystemClock _clock;

    public ChangeReparacionStatusService(
        IUnitRepository units,
        IOccupancyLinkRepository links,
        IReparacionRepository reparaciones,
        INotificationRepository notifications,
        ISystemClock clock)
    {
        _units = units;
        _links = links;
        _reparaciones = reparaciones;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<OperationResult<Reparacion>> ChangeStatusAsync(
        Guid ownerAccountId,
        Guid unitId,
        Guid reparacionId,
        ReparacionEstado nuevoEstado,
        CancellationToken cancellationToken = default)
    {
        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<Reparacion>.NotFound();
        }

        if (unit.OwnerAccountId != ownerAccountId)
        {
            return OperationResult<Reparacion>.Forbidden();
        }

        var reparacion = await _reparaciones
            .FindByIdAsync(reparacionId, cancellationToken)
            .ConfigureAwait(false);

        if (reparacion is null || reparacion.UnitId != unitId)
        {
            return OperationResult<Reparacion>.NotFound();
        }

        var now = _clock.UtcNow;
        bool changed;

        try
        {
            changed = reparacion.ChangeStatus(nuevoEstado, now);
        }
        catch (ArgumentException exception)
        {
            return OperationResult<Reparacion>.ValidationFailed("estado", exception.Message);
        }

        if (!changed)
        {
            return OperationResult<Reparacion>.Success(reparacion);
        }

        await _reparaciones.SaveAsync(reparacion, cancellationToken).ConfigureAwait(false);
        await NotifyUnitParticipantsAsync(unit.OwnerAccountId, unitId, reparacion, ownerAccountId, now, cancellationToken)
            .ConfigureAwait(false);

        return OperationResult<Reparacion>.Success(reparacion);
    }

    private async Task NotifyUnitParticipantsAsync(
        Guid unitOwnerAccountId,
        Guid unitId,
        Reparacion reparacion,
        Guid actorAccountId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var recipients = new HashSet<Guid> { unitOwnerAccountId };
        var activeLinks = await _links.ListActiveByUnitAsync(unitId, cancellationToken).ConfigureAwait(false);

        foreach (var link in activeLinks)
        {
            recipients.Add(link.TenantAccountId);
        }

        recipients.Remove(actorAccountId);

        var message = $"El reporte \"{reparacion.Title}\" cambió a estado {reparacion.Status}.";

        foreach (var recipientAccountId in recipients)
        {
            var notification = Notification.CreateReparacionStatusUpdate(
                recipientAccountId, unitId, reparacion.Id, message, now);
            await _notifications.SaveAsync(notification, cancellationToken).ConfigureAwait(false);
        }
    }
}
