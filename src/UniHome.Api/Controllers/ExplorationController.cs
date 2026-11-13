using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Exploration;
using UniHome.Application.Common;
using UniHome.Application.Exploration;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class ExplorationController : AuthenticatedControllerBase
{
    private readonly ExploreUnitsService _explore;
    private readonly GetUnitCardService _card;
    private readonly GetUnitDetailService _detail;

    public ExplorationController(
        ExploreUnitsService explore,
        GetUnitCardService card,
        GetUnitDetailService detail)
    {
        _explore = explore;
        _card = card;
        _detail = detail;
    }

    /// <summary>
    /// EP-M04-01 (HU-037/HU-039..043/HU-084): mapa público de unidades
    /// Aprobadas y Disponibles. La distancia se calcula con Haversine.
    /// </summary>
    [HttpGet("exploracion/mapa")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<UnidadMapaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Map(
        [FromQuery] ExploracionQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _explore
            .MapAsync(query.ToApplicationQuery(), cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(value.Select(UnidadMapaResponse.FromView).ToArray()),
            notFoundDetail: "La universidad no existe o está inactiva.",
            forbiddenDetail: "La operación no está permitida.",
            conflictDetail: "La consulta entra en conflicto con otra operación.");
    }

    /// <summary>
    /// EP-M04-02 (HU-037..043/HU-084): lista paginada pública. El total viaja en
    /// X-Total-Count y los filtros son combinables con AND lógico.
    /// </summary>
    [HttpGet("exploracion/lista")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<UnidadTarjetaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> List(
        [FromQuery] ExploracionQuery query,
        CancellationToken cancellationToken)
    {
        Guid? accountId = TryGetAccountId(out var authenticatedId)
            ? authenticatedId
            : null;
        var result = await _explore
            .ListAsync(query.ToApplicationQuery(), accountId, cancellationToken)
            .ConfigureAwait(false);

        if (result.Status != OperationStatus.Success)
        {
            return ToActionResult(
                result,
                _ => StatusCode(StatusCodes.Status500InternalServerError),
                notFoundDetail: "La universidad no existe o está inactiva.",
                forbiddenDetail: "La operación no está permitida.",
                conflictDetail: "La consulta entra en conflicto con otra operación.");
        }

        Response.Headers["X-Total-Count"] = result.Value!.Total.ToString(
            CultureInfo.InvariantCulture);
        Response.Headers["X-Page"] = query.Page.ToString(CultureInfo.InvariantCulture);
        Response.Headers["X-Page-Size"] = query.PageSize.ToString(CultureInfo.InvariantCulture);

        return Ok(result.Value.Items
            .Select(UnidadTarjetaResponse.FromView)
            .ToArray());
    }

    /// <summary>
    /// EP-M04-03 (HU-038/HU-046/HU-047): tarjeta pública. Si hay Bearer válido,
    /// incluye esFavorito; sin sesión devuelve null.
    /// </summary>
    [HttpGet("unidades/{unitId:guid}/tarjeta")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UnidadTarjetaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Card(
        Guid unitId,
        CancellationToken cancellationToken)
    {
        Guid? accountId = TryGetAccountId(out var authenticatedId)
            ? authenticatedId
            : null;
        var result = await _card
            .GetAsync(unitId, accountId, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(UnidadTarjetaResponse.FromView(value)),
            notFoundDetail: "La unidad no está disponible públicamente.",
            forbiddenDetail: "La operación no está permitida.",
            conflictDetail: "La consulta entra en conflicto con otra operación.");
    }

    /// <summary>
    /// EP-M04-04 (HU-044/HU-045): ficha completa con sesión. No incluye correo
    /// ni teléfono del arrendador.
    /// </summary>
    [HttpGet("unidades/{unitId:guid}/ficha")]
    [Authorize]
    [ProducesResponseType(typeof(UnidadFichaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Detail(
        Guid unitId,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var result = await _detail
            .GetAsync(unitId, accountId, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(UnidadFichaResponse.FromView(value)),
            notFoundDetail: "La unidad no está disponible públicamente.",
            forbiddenDetail: "La sesión no es válida.",
            conflictDetail: "La consulta entra en conflicto con otra operación.");
    }
}

internal static class ExplorationQueryExtensions
{
    public static UniHome.Application.Exploration.ExplorationQuery ToApplicationQuery(
        this ExploracionQuery query) =>
        new(
            query.UniversidadId ?? Guid.Empty,
            query.DistanciaMaxKm,
            query.PrecioMaxMxn,
            query.SoloIndividual,
            query.ParaQuien,
            query.Page,
            query.PageSize);
}
