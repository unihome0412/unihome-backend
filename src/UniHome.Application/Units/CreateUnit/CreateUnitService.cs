using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Profiles;
using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Units;

namespace UniHome.Application.Units.CreateUnit;

/// <summary>
/// EP-M05-02 (HU-031/HU-078/HU-079): crea una unidad en Borrador dentro de una
/// propiedad del titular. La plantilla solo rellena campos omitidos; el cliente
/// puede sobrescribirla y el servidor vuelve a validar todo.
/// </summary>
public sealed class CreateUnitService
{
    private readonly IUnitRepository _units;
    private readonly IPropertyRepository _properties;
    private readonly PublishingEligibilityService _eligibility;
    private readonly ISystemClock _clock;

    public CreateUnitService(
        IUnitRepository units,
        IPropertyRepository properties,
        PublishingEligibilityService eligibility,
        ISystemClock clock)
    {
        _units = units;
        _properties = properties;
        _eligibility = eligibility;
        _clock = clock;
    }

    public async Task<OperationResult<Unit>> CreateAsync(
        Guid ownerAccountId,
        Guid propertyId,
        CreateUnitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await _eligibility.IsEligibleAsync(ownerAccountId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<Unit>.Forbidden();
        }

        var property = await _properties
            .FindByIdAsync(propertyId, cancellationToken)
            .ConfigureAwait(false);

        if (property is null)
        {
            return OperationResult<Unit>.NotFound();
        }

        if (property.OwnerAccountId != ownerAccountId)
        {
            return OperationResult<Unit>.Forbidden();
        }

        var template = command.PlantillaBase;
        var defaults = template.HasValue ? UnitTemplates.Get(template.Value) : null;
        var type = command.TipoUnidad ?? defaults?.Type;
        var occupancy = command.Ocupacion ?? defaults?.Occupancy;
        var places = command.Lugares ?? defaults?.Places;
        var forWhom = command.ParaQuien ?? defaults?.ForWhom;
        var bathroom = command.Bano ?? defaults?.Bathroom;
        var furniture = command.Mobiliario ?? defaults?.Furniture;

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (type is null || !Enum.IsDefined(type.Value))
        {
            errors["tipoUnidad"] = ["El tipo de unidad es obligatorio."];
        }

        if (occupancy is null || !Enum.IsDefined(occupancy.Value))
        {
            errors["ocupacion"] = ["La ocupación es obligatoria."];
        }

        if (forWhom is null || !Enum.IsDefined(forWhom.Value))
        {
            errors["paraQuien"] = ["El público objetivo es obligatorio."];
        }

        if (bathroom is null || !Enum.IsDefined(bathroom.Value))
        {
            errors["bano"] = ["El tipo de baño es obligatorio."];
        }

        if (furniture is null || !Enum.IsDefined(furniture.Value))
        {
            errors["mobiliario"] = ["El mobiliario es obligatorio."];
        }

        if (command.PrecioMensualPorLugarMxn is null or <= 0)
        {
            errors["precioMensualPorLugarMxn"] = ["El precio debe ser mayor que cero."];
        }

        if (command.TamanoM2 is null or <= 0)
        {
            errors["tamanoM2"] = ["El tamaño debe ser mayor que cero."];
        }

        if (occupancy == Occupancy.Compartida && places is null)
        {
            errors["lugares"] = ["Indique el número de lugares para una unidad compartida."];
        }

        if (errors.Count > 0)
        {
            return OperationResult<Unit>.ValidationFailed(errors);
        }

        try
        {
            var unit = Unit.Create(
                property.Id,
                property.OwnerAccountId,
                property.UniversityId,
                type!.Value,
                occupancy!.Value,
                places ?? 1,
                command.PrecioMensualPorLugarMxn!.Value,
                command.TamanoM2!.Value,
                forWhom!.Value,
                bathroom!.Value,
                furniture!.Value,
                command.Muebles,
                template,
                _clock.UtcNow);

            await _units.AddAsync(unit, cancellationToken).ConfigureAwait(false);
            return OperationResult<Unit>.Success(unit);
        }
        catch (ArgumentException exception)
        {
            return OperationResult<Unit>.ValidationFailed("unidad", exception.Message);
        }
    }
}
