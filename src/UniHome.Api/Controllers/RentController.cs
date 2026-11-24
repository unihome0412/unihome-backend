using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Rentals;
using UniHome.Application.Rentals.Configure;
using UniHome.Application.Rentals.Split;
using UniHome.Domain.Rentals;

namespace UniHome.Api.Controllers;

/// <summary>
/// EP-M10 (HU-050/051/052/053/054): configuración y división informativa de la
/// renta de una unidad por periodo. UniHome no procesa pagos (HU-090).
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class RentController : AuthenticatedControllerBase
{
    private const long MaxRequestBytes = 8 * 1024;

    private readonly ConfigureRentPeriodService _configure;
    private readonly SplitRentPeriodService _split;

    public RentController(ConfigureRentPeriodService configure, SplitRentPeriodService split)
    {
        _configure = configure;
        _split = split;
    }

    /// <summary>EP-M10-01 (HU-050/051/052): define renta total, conceptos y día de corte.</summary>
    [HttpPut("unidades/{unitId:guid}/renta/{periodo}")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(RentaPeriodoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Configure(
        Guid unitId,
        string periodo,
        [FromBody] ConfigurarRentaRequest request,
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
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var concepts = request.Conceptos?
            .Select(input => new RentConcept(input.Nombre, input.EsFijo))
            .ToArray();

        var result = await _configure
            .ConfigureAsync(
                accountId,
                unitId,
                periodo,
                request.RentaTotalMxn,
                request.DiaCorte,
                concepts,
                cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(RentaPeriodoResponse.FromDomain(value)),
            notFoundDetail: "La unidad no existe.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "No se pudo completar la operación.");
    }

    /// <summary>EP-M10-02 (HU-053/HU-054): divide la renta del periodo entre los inquilinos activos.</summary>
    [HttpPut("unidades/{unitId:guid}/renta/{periodo}/division")]
    [Authorize]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(RentaPeriodoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Split(
        Guid unitId,
        string periodo,
        [FromBody] DividirRentaRequest request,
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
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var shares = request.Cuotas?
            .Select(input => new TenantShare(input.InquilinoAccountId, input.MontoMxn))
            .ToArray();

        var result = await _split
            .SplitAsync(accountId, unitId, periodo, request.Metodo, shares, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(RentaPeriodoResponse.FromDomain(value)),
            notFoundDetail: "La renta de este periodo no está configurada.",
            forbiddenDetail: "La cuenta no está autorizada para esta operación.",
            conflictDetail: "No se pudo completar la operación.");
    }
}
