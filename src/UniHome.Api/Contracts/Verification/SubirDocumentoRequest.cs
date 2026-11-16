using Microsoft.AspNetCore.Http;

namespace UniHome.Api.Contracts.Verification;

public sealed class SubirDocumentoRequest
{
    public IFormFile? Archivo { get; set; }

    /// <summary>Nombre original opcional; si falta se usa el nombre del multipart.</summary>
    public string? NombreArchivoOriginal { get; set; }
}
