using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.OccupancyLinks;

namespace UniHome.Application.OccupancyLinks.LeaveUnit;

/// <summary>
/// EP-M09-05 (HU-095): el propio inquilino deja la unidad, con confirmación
/// explícita. Libera un lugar; la disponibilidad se recalcula salvo que la
/// unidad esté en mantenimiento.
/// </summary>
public sealed class LeaveUnitService
{
    private readonly IUnitRepository _units;
    private readonly IOccupancyLinkRepository _links;
    private readonly UnitAvailabilityRecalculator _recalculator;
    private readonly ISystemClock _clock;

    public LeaveUnitService(
        IUnitRepository units,
        IOccupancyLinkRepository links,
        UnitAvailabilityRecalculator recalculator,
        ISystemClock clock)
    {
        _units = units;
        _links = links;
        _recalculator = recalculator;
        _clock = clock;
    }

    public async Task<OperationResult<Guid>> LeaveAsync(
        Guid tenantAccountId,
        Guid unitId,
        bool confirmacion,
        CancellationToken cancellationToken = default)
    {
        if (!confirmacion)
        {
            return OperationResult<Guid>.ValidationFailed(
                "confirmacion",
                "Debe confirmar que dejará la unidad.");
        }

        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<Guid>.NotFound();
        }

        var link = await _links
            .FindAsync(tenantAccountId, unitId, cancellationToken)
            .ConfigureAwait(false);

        if (link is null || link.Status != OccupancyStatus.Activo)
        {
            return OperationResult<Guid>.NotFound();
        }

        link.End(OccupancyEndReason.AbandonoInquilino, _clock.UtcNow);
        await _links.SaveAsync(link, cancellationToken).ConfigureAwait(false);

        var remaining = await _units
            .ReleaseSlotAsync(unit.Id, unit.PropertyId, cancellationToken)
            .ConfigureAwait(false);
        await _recalculator
            .RecomputeAsync(unit, remaining, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return OperationResult<Guid>.Success(unitId);
    }
}
