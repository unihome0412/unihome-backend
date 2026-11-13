using UniHome.Domain.Properties;

namespace UniHome.Application.Properties.CreateProperty;

public sealed record CreatePropertyCommand(
    string? Nombre,
    string? Direccion,
    string? Colonia,
    PropertyType TipoPropiedad,
    Guid UniversidadId,
    double? Latitud,
    double? Longitud,
    string? Piso,
    string? NumeroUnidad);
