using System.Text.Json.Serialization;

namespace UniHome.Api.Contracts.Profiles;

/// <summary>EP-M02/M03 (HU-087): mismo contrato desde el perfil o desde el mapa.</summary>
public sealed record CambiarMiUniversidadRequest(
    [property: JsonRequired] Guid UniversidadId);
