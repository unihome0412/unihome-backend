using System.Text.Json.Serialization;

namespace UniHome.Api.Contracts.Universities;

/// <summary>EP-M03-05: activación o desactivación lógica (HU-077).</summary>
public sealed record CambiarEstadoUniversidadRequest(
    [property: JsonRequired] bool Activa);
