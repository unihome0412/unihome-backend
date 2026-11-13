namespace UniHome.Api.Contracts.Universities;

/// <summary>EP-M03-02: nombre y campus en un solo campo (HU-075).</summary>
public sealed record CrearUniversidadRequest(string? NombreYCampus);
