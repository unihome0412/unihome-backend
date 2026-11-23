using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Units;

namespace UniHome.Application.OccupancyLinks;

/// <summary>
/// EP-M09 (HU-093/HU-094/HU-095/HU-049): recalcula la disponibilidad de una
/// unidad a partir de <c>lugaresOcupados</c>, el contador atómico mantenido por
/// <c>IUnitRepository.TryReserveSlotAsync</c>/<c>ReleaseSlotAsync</c>.
/// Mantenimiento es un estado manual: mientras esté activo, los cambios de
/// ocupación no lo sobreescriben.
/// </summary>
public sealed class UnitAvailabilityRecalculator
{
    private readonly IUnitRepository _units;

    public UnitAvailabilityRecalculator(IUnitRepository units)
    {
        _units = units;
    }

    /// <param name="occupiedPlaces">
    /// Valor ya conocido por el llamador (devuelto por el CAS de reserva o de
    /// liberación); no se vuelve a consultar aquí para no introducir una
    /// segunda lectura fuera de la operación atómica.
    /// </param>
    /// <param name="ignoreMaintenanceGuard">
    /// true solo cuando el llamador está saliendo explícitamente de mantenimiento
    /// (HU-093); en ese caso el estado en memoria todavía es Mantenimiento y debe
    /// recalcularse de todas formas.
    /// </param>
    public async Task RecomputeAsync(
        Unit unit,
        int occupiedPlaces,
        bool ignoreMaintenanceGuard = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (!ignoreMaintenanceGuard && unit.Availability == UnitAvailability.Mantenimiento)
        {
            return;
        }

        var next = occupiedPlaces >= unit.Places
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
