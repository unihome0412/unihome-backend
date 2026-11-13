using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Publications;
using UniHome.Application.Common;
using UniHome.Application.Publications.AcceptTerms;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1/publicaciones")]
public sealed class PublicationsController : AuthenticatedControllerBase
{
    private const long MaxRequestBytes = 8 * 1024;

    private readonly AcceptTermsService _acceptTerms;

    public PublicationsController(AcceptTermsService acceptTerms)
    {
        _acceptTerms = acceptTerms;
    }

    /// <summary>
    /// EP-M05-07 (HU-091): registra la aceptación verificable de la versión de
    /// términos. No envía a revisión ni crea expedientes de M06.
    /// </summary>
    [HttpPost("primera/terminos")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(AceptarTerminosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> AcceptTerms(
        [FromBody] AceptarTerminosRequest request,
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

        var result = await _acceptTerms
            .AcceptAsync(
                accountId,
                new AcceptTermsCommand(request.VersionTerminos),
                cancellationToken)
            .ConfigureAwait(false);

        if (result.Status == OperationStatus.Conflict)
        {
            return CreateProblemResult(
                StatusCodes.Status422UnprocessableEntity,
                "Los términos de esta versión ya fueron aceptados.");
        }

        return ToActionResult(
            result,
            value => Ok(new AceptarTerminosResponse(
                Aceptada: true,
                value.Version,
                value.AcceptedAt)),
            notFoundDetail: "El recurso no existe.",
            forbiddenDetail: "Para publicar un espacio necesitas tener 18 años o más.",
            conflictDetail: "Los términos de esta versión ya fueron aceptados.");
    }
}
