using Microsoft.AspNetCore.Http;

namespace UniHome.Api.Contracts.Units;

public sealed class AgregarFotosRequest
{
    public List<IFormFile> Fotos { get; set; } = [];
}
