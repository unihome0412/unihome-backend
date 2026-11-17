using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Publications;
using UniHome.Application.Common;
using UniHome.Application.Publications.AcceptTerms;
using UniHome.Application.Publications.MyPublicationMap;
using UniHome.Application.Publications.SubmitFirstPublication;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1/publicaciones")]
public sealed class PublicationsController : AuthenticatedControllerBase
{
    private const long MaxRequestBytes = 8 * 1024;

    private readonly AcceptTermsService _acceptTerms;
    private readonly SubmitFirstPublicationService _submitFirstPublication;
    private readonly GetMyPublicationMapService _myPublicationMap;

    public PublicationsController(
        AcceptTermsService acceptTerms,
        SubmitFirstPublicationService submitFirstPublication,
        GetMyPublicationMapService myPublicationMap)
    {
        _acceptTerms = acceptTerms;
        _submitFirstPublication = submitFirstPublication;
        _myPublicationMap = myPublicationMap;
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

    /// <summary>
    /// EP-M05-08 (HU-013/HU-027): confirma la primera publicación en revisión.
    /// Exige términos, al menos una foto y ambos documentos M06. La unidad no se
    /// muestra en M04 hasta una decisión administrativa favorable.
    /// </summary>
    [HttpPost("primera/enviar")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(PublicacionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SubmitFirstPublication(CancellationToken cancellationToken)
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

        var result = await _submitFirstPublication
            .SubmitAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(PublicacionResponse.FromView(value)),
            notFoundDetail: "Primero registra tu primera publicación.",
            forbiddenDetail: "Para publicar un espacio necesitas tener 18 años o más.",
            conflictDetail: "La publicación no puede enviarse a revisión en su estado actual.",
            unprocessableDetail:
                "Completa los términos, al menos una foto y ambos documentos de verificación.");
    }

    /// <summary>
    /// EP-M05-10 (HU-028): pin privado de la primera publicación En revisión.
    /// Otros estudiantes y visitantes no pueden obtenerlo; M04 sigue oculto.
    /// </summary>
    [HttpGet("/api/v1/mi-publicacion/estado-mapa")]
    [Authorize]
    [ProducesResponseType(typeof(MiPublicacionMapaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetMyPublicationMap(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var result = await _myPublicationMap
            .GetAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(MiPublicacionMapaResponse.FromView(value)),
            notFoundDetail: "No tienes una primera publicación En revisión.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "No se pudo consultar la publicación.",
            unprocessableDetail: "La publicación no tiene una ubicación registrada.");
    }
}
