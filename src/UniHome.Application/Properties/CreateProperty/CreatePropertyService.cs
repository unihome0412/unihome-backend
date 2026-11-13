using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Profiles;
using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Properties.Exceptions;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Properties;

namespace UniHome.Application.Properties.CreateProperty;

/// <summary>
/// EP-M05-01 (HU-029/HU-030): crea una propiedad en Borrador para el titular
/// autenticado. La edad se revalida en servidor y la universidad debe estar activa.
/// Si faltan coordenadas se usa la ubicación opcional del catálogo de universidades.
/// </summary>
public sealed class CreatePropertyService
{
    private readonly IPropertyRepository _properties;
    private readonly IUniversityRepository _universities;
    private readonly PublishingEligibilityService _eligibility;
    private readonly ISystemClock _clock;

    public CreatePropertyService(
        IPropertyRepository properties,
        IUniversityRepository universities,
        PublishingEligibilityService eligibility,
        ISystemClock clock)
    {
        _properties = properties;
        _universities = universities;
        _eligibility = eligibility;
        _clock = clock;
    }

    public async Task<OperationResult<PropertyCreationView>> CreateAsync(
        Guid ownerAccountId,
        CreatePropertyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await _eligibility.IsEligibleAsync(ownerAccountId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<PropertyCreationView>.Forbidden();
        }

        var university = await _universities
            .FindByIdAsync(command.UniversidadId, cancellationToken)
            .ConfigureAwait(false);

        if (university is null || !university.IsActive)
        {
            return OperationResult<PropertyCreationView>.ValidationFailed(
                "universidadId",
                "La universidad no existe o está inactiva.");
        }

        var latitude = command.Latitud;
        var longitude = command.Longitud;
        var suggested = false;

        if (latitude is null || longitude is null)
        {
            var location = await _universities
                .GetLocationAsync(command.UniversidadId, cancellationToken)
                .ConfigureAwait(false);

            if (location is null || !location.IsValid)
            {
                return OperationResult<PropertyCreationView>.ValidationFailed(
                    "latitud",
                    "Proporcione latitud y longitud; la universidad no tiene ubicación sugerida disponible.");
            }

            latitude = location.Latitude;
            longitude = location.Longitude;
            suggested = true;
        }

        Property property;

        try
        {
            property = Property.Create(
                ownerAccountId,
                command.Nombre,
                command.Direccion,
                command.Colonia,
                command.TipoPropiedad,
                command.UniversidadId,
                latitude,
                longitude,
                command.Piso,
                command.NumeroUnidad,
                _clock.UtcNow);
        }
        catch (ArgumentException exception)
        {
            return OperationResult<PropertyCreationView>.ValidationFailed(
                "propiedad",
                exception.Message);
        }

        try
        {
            await _properties.AddAsync(property, cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicatePropertyException)
        {
            return OperationResult<PropertyCreationView>.Conflict();
        }

        return OperationResult<PropertyCreationView>.Success(
            new PropertyCreationView(property, suggested));
    }
}
