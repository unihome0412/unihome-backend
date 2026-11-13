namespace UniHome.Api.Contracts.Universities;

/// <summary>EP-M03-04: edición del nombre y campus (HU-077).</summary>
public sealed record EditarUniversidadRequest(string? NombreYCampus);
