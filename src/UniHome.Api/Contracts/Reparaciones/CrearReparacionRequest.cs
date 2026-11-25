using Microsoft.AspNetCore.Http;

namespace UniHome.Api.Contracts.Reparaciones;

/// <summary>EP-M11-01 (HU-062/063): multipart/form-data; la foto es opcional.</summary>
public sealed class CrearReparacionRequest
{
    public string? Titulo { get; set; }

    public string? Descripcion { get; set; }

    public IFormFile? Foto { get; set; }
}
