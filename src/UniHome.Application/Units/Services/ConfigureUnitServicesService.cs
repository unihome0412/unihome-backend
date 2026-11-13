using UniHome.Application.Common;
using UniHome.Application.Profiles;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Units;

namespace UniHome.Application.Units.Services;

/// <summary>
/// EP-M05-04 (HU-081): reemplaza el catálogo de servicios de una unidad propia.
/// Un servicio Aparte exige monto y no se suma a la renta base.
/// </summary>
public sealed class ConfigureUnitServicesService
{
    private readonly IUnitRepository _units;
    private readonly PublishingEligibilityService _eligibility;

    public ConfigureUnitServicesService(
        IUnitRepository units,
        PublishingEligibilityService eligibility)
    {
        _units = units;
        _eligibility = eligibility;
    }

    public async Task<OperationResult<Unit>> ConfigureAsync(
        Guid ownerAccountId,
        Guid unitId,
        ConfigureUnitServicesCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

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

        if (command.Servicios is null)
        {
            return OperationResult<Unit>.ValidationFailed(
                "servicios",
                "La lista de servicios es obligatoria.");
        }

        try
        {
            var charges = command.Servicios
                .Select(service => ServiceCharge.Create(
                    service.Nombre,
                    service.Modalidad,
                    service.MontoMensualMxn))
                .ToArray();
            unit.ConfigureServices(charges);
        }
        catch (ArgumentException exception)
        {
            return OperationResult<Unit>.ValidationFailed("servicios", exception.Message);
        }

        var updated = await _units
            .UpdateServicesAsync(unit.Id, unit.PropertyId, unit.Services, cancellationToken)
            .ConfigureAwait(false);

        return updated
            ? OperationResult<Unit>.Success(unit)
            : OperationResult<Unit>.NotFound();
    }
}
