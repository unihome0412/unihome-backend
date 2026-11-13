using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Properties;
using UniHome.Application.Properties.CreateProperty;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1/propiedades")]
public sealed class PropertiesController : AuthenticatedControllerBase
{
    private const long MaxRequestBytes = 8 * 1024;

    private readonly CreatePropertyService _createProperty;

    public PropertiesController(CreatePropertyService createProperty)
    {
        _createProperty = createProperty;
    }

    /// <summary>
    /// EP-M05-01 (HU-029/HU-030/HU-014): registra una propiedad en Borrador para
    /// el titular autenticado y mayor de edad.
    /// </summary>
    [HttpPost]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(PropiedadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(
        [FromBody] CrearPropiedadRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var result = await _createProperty
            .CreateAsync(
                accountId,
                new CreatePropertyCommand(
                    request.Nombre,
                    request.Direccion,
                    request.Colonia,
                    request.TipoPropiedad,
                    request.UniversidadId,
                    request.Latitud,
                    request.Longitud,
                    request.Piso,
                    request.NumeroUnidad),
                cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => StatusCode(
                StatusCodes.Status201Created,
                PropiedadResponse.FromView(value)),
            notFoundDetail: "La universidad o el recurso no existe.",
            forbiddenDetail: "Para publicar un espacio necesitas tener 18 años o más.",
            conflictDetail: "Ya existe una propiedad con los mismos datos para esta cuenta.");
    }
}
