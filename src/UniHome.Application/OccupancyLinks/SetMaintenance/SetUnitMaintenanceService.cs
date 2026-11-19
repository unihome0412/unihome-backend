using UniHome.Application.Common;
using UniHome.Application.Profiles;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Units;

namespace UniHome.Application.OccupancyLinks.SetMaintenance;

/// <summary>
/// EP-M09-03 (HU-093): marca o desmarca el mantenimiento de una unidad propia.
/// Mantenimiento es un estado manual que gana sobre el cálculo automático; al
/// desactivarlo se recalcula Disponible/Ocupado según los vínculos activos.
/// </summary>
public sealed class SetUnitMaintenanceService
{
    private readonly IUnitRepository _units;
    private readonly UnitAvailabilityRecalculator _recalculator;
    private readonly PublishingEligibilityService _eligibility;

    public SetUnitMaintenanceService(
        IUnitRepository units,
        UnitAvailabilityRecalculator recalculator,
        PublishingEligibilityService eligibility)
    {
        _units = units;
        _recalculator = recalculator;
        _eligibility = eligibility;
    }

    public async Task<OperationResult<Unit>> SetAsync(
        Guid ownerAccountId,
        Guid unitId,
        bool enMantenimiento,
        CancellationToken cancellationToken = default)
    {
        if (!await _eligibility.IsEligibleAsync(ownerAccountId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<Unit>.Forbidden();
        }

        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<Unit>.NotFound();
        }

        if (unit.OwnerAccountId != ownerAccountId)
        {
            return OperationResult<Unit>.Forbidden();
        }

        if (enMantenimiento)
        {
            if (unit.Availability != UnitAvailability.Mantenimiento)
            {
                unit.SetAvailability(UnitAvailability.Mantenimiento);
                var updated = await _units
                    .UpdateAvailabilityAsync(
                        unit.Id,
                        unit.PropertyId,
                        UnitAvailability.Mantenimiento,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!updated)
                {
                    return OperationResult<Unit>.NotFound();
                }
            }
        }
        else if (unit.Availability == UnitAvailability.Mantenimiento)
        {
            // Sale de mantenimiento: se recalcula desde los vínculos activos.
            // ignoreMaintenanceGuard=true porque el estado en memoria todavía es
            // Mantenimiento y, sin el flag, el recalculador no haría nada.
            await _recalculator
                .RecomputeAsync(unit, cancellationToken, ignoreMaintenanceGuard: true)
                .ConfigureAwait(false);
        }

        return OperationResult<Unit>.Success(unit);
    }
}
