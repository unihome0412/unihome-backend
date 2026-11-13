using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Favorites;
using UniHome.Application.Common;
using UniHome.Application.Favorites;

namespace UniHome.Api.Controllers;

[ApiController]
[Route("api/v1/favoritos")]
public sealed class FavoritesController : AuthenticatedControllerBase
{
    private readonly AddFavoriteService _add;
    private readonly RemoveFavoriteService _remove;
    private readonly ListFavoritesService _list;

    public FavoritesController(
        AddFavoriteService add,
        RemoveFavoriteService remove,
        ListFavoritesService list)
    {
        _add = add;
        _remove = remove;
        _list = list;
    }

    /// <summary>EP-M07-01 (HU-085): alta idempotente.</summary>
    [HttpPut("{unitId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Add(
        Guid unitId,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var result = await _add
            .AddAsync(accountId, unitId, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            _ => NoContent(),
            notFoundDetail: "La unidad no existe.",
            forbiddenDetail: "La operación no está permitida.",
            conflictDetail: "La operación entra en conflicto con otra.");
    }

    /// <summary>EP-M07-02 (HU-085): baja idempotente, 204 aunque no existiera.</summary>
    [HttpDelete("{unitId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Remove(
        Guid unitId,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var result = await _remove
            .RemoveAsync(accountId, unitId, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            _ => NoContent(),
            notFoundDetail: "La unidad no existe.",
            forbiddenDetail: "La operación no está permitida.",
            conflictDetail: "La operación entra en conflicto con otra.");
    }

    /// <summary>EP-M07-03 (HU-085): listado propio paginado.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<FavoritoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = ListFavoritesService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "La sesión no es válida.");
        }

        var result = await _list
            .ListAsync(accountId, page, pageSize, cancellationToken)
            .ConfigureAwait(false);

        if (result.Status != OperationStatus.Success)
        {
            return ToActionResult(
                result,
                _ => StatusCode(StatusCodes.Status500InternalServerError),
                notFoundDetail: "El recurso no existe.",
                forbiddenDetail: "La operación no está permitida.",
                conflictDetail: "La operación entra en conflicto con otra.");
        }

        Response.Headers["X-Total-Count"] = result.Value!.Total.ToString(
            CultureInfo.InvariantCulture);
        Response.Headers["X-Page"] = page.ToString(CultureInfo.InvariantCulture);
        Response.Headers["X-Page-Size"] = pageSize.ToString(CultureInfo.InvariantCulture);

        return Ok(result.Value.Items.Select(FavoritoResponse.FromView).ToArray());
    }
}
