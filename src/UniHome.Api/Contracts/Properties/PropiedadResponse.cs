using UniHome.Application.Properties.CreateProperty;
using UniHome.Domain.Properties;

namespace UniHome.Api.Contracts.Properties;

public sealed record PropiedadResponse(
    Guid Id,
    string Nombre,
    string Direccion,
    string Colonia,
    PropertyType TipoPropiedad,
    Guid UniversidadId,
    double? Latitud,
    double? Longitud,
    string? Piso,
    string? NumeroUnidad,
    PublicationState EstadoPublicacion,
    DateTimeOffset FechaAlta,
    bool UbicacionSugerida)
{
    public static PropiedadResponse FromView(PropertyCreationView view) =>
        new(
            view.Property.Id,
            view.Property.Name,
            view.Property.Address,
            view.Property.Colonia,
            view.Property.PropertyType,
            view.Property.UniversityId,
            view.Property.Latitude,
            view.Property.Longitude,
            view.Property.Floor,
            view.Property.UnitNumber,
            view.Property.PublicationState,
            view.Property.CreatedAt,
            view.LocationSuggested);
}
