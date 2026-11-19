using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Units;

namespace UniHome.Application.OccupancyLinks;

/// <summary>
/// EP-M09 (HU-093/HU-094/HU-095/HU-049): recalcula la disponibilidad de una
/// unidad después de un cambio de ocupación. Mantenimiento es un estado manual:
/// mientras esté activo, los vínculos no lo sobrescriben.
/// </summary>
public sealed class UnitAvailabilityRecalculator
{
    private readonly IUnitRepository _units;
    private readonly IOccupancyLinkRepository _links;

    public UnitAvailabilityRecalculator(IUnitRepository units, IOccupancyLinkRepository links)
    {
        _units = units;
        _links = links;
    }

    /// <param name="ignoreMaintenanceGuard">
    /// true solo cuando el llamador está saliendo explícitamente de mantenimiento
    /// (HU-093); en ese caso el estado en memoria todavía es Mantenimiento y debe
    /// recalcularse de todas formas.
    /// </param>
    public async Task RecomputeAsync(
        Unit unit,
        CancellationToken cancellationToken = default,
        bool ignoreMaintenanceGuard = false)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (!ignoreMaintenanceGuard && unit.Availability == UnitAvailability.Mantenimiento)
        {
            return;
        }

        var activeCount = await _links
            .CountActiveAsync(unit.Id, cancellationToken)
            .ConfigureAwait(false);
        var next = activeCount >= unit.Places
            ? UnitAvailability.Ocupado
            : UnitAvailability.Disponible;

        if (next == unit.Availability)
        {
            return;
        }

        unit.SetAvailability(next);
        await _units
            .UpdateAvailabilityAsync(unit.Id, unit.PropertyId, next, cancellationToken)
            .ConfigureAwait(false);
    }
}
