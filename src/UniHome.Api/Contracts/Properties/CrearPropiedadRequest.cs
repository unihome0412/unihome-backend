using System.Text.Json.Serialization;
using UniHome.Domain.Properties;

namespace UniHome.Api.Contracts.Properties;

public sealed record CrearPropiedadRequest(
    string? Nombre,
    string? Direccion,
    string? Colonia,
    [property: JsonRequired] PropertyType TipoPropiedad,
    [property: JsonRequired] Guid UniversidadId,
    double? Latitud,
    double? Longitud,
    string? Piso,
    string? NumeroUnidad);
