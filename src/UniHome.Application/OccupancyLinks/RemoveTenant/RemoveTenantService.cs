using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.Profiles;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.OccupancyLinks;

namespace UniHome.Application.OccupancyLinks.RemoveTenant;

/// <summary>
/// EP-M09-04 (HU-094): da de baja a un inquilino de una unidad propia, con
/// confirmación explícita del propietario. Libera un lugar; la disponibilidad se
/// recalcula salvo que la unidad esté en mantenimiento.
/// </summary>
public sealed class RemoveTenantService
{
    private readonly IUnitRepository _units;
    private readonly IOccupancyLinkRepository _links;
    private readonly UnitAvailabilityRecalculator _recalculator;
    private readonly PublishingEligibilityService _eligibility;
    private readonly ISystemClock _clock;

    public RemoveTenantService(
        IUnitRepository units,
        IOccupancyLinkRepository links,
        UnitAvailabilityRecalculator recalculator,
        PublishingEligibilityService eligibility,
        ISystemClock clock)
    {
        _units = units;
        _links = links;
        _recalculator = recalculator;
        _eligibility = eligibility;
        _clock = clock;
    }

    public async Task<OperationResult<Guid>> RemoveAsync(
        Guid ownerAccountId,
        Guid unitId,
        Guid tenantAccountId,
        bool confirmacion,
        CancellationToken cancellationToken = default)
    {
        if (!confirmacion)
        {
            return OperationResult<Guid>.ValidationFailed(
                "confirmacion",
                "Debe confirmar la baja del inquilino.");
        }

        if (!await _eligibility.IsEligibleAsync(ownerAccountId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<Guid>.Forbidden();
        }

        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<Guid>.NotFound();
        }

        if (unit.OwnerAccountId != ownerAccountId)
        {
            return OperationResult<Guid>.Forbidden();
        }

        // Misma transición atómica que la salida del inquilino: solo una de las
        // solicitudes concurrentes puede finalizar el vínculo y liberar cupo.
        var ended = await _links
            .TryEndActiveAsync(
                tenantAccountId,
                unitId,
                OccupancyEndReason.BajaPorPropietario,
                _clock.UtcNow,
                cancellationToken)
            .ConfigureAwait(false);

        if (!ended)
        {
            return OperationResult<Guid>.NotFound();
        }

        var remaining = await _units
            .ReleaseSlotAsync(unit.Id, unit.PropertyId, cancellationToken)
            .ConfigureAwait(false);
        await _recalculator
            .RecomputeAsync(unit, remaining, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return OperationResult<Guid>.Success(tenantAccountId);
    }
}
