using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Configuration;
using UniHome.Api.Contracts.Admin;
using UniHome.Application.Common;
using UniHome.Application.Identity.Admin;

namespace UniHome.Api.Controllers;

/// <summary>EP-M13 (HU-072/073/074): administración de cuentas de usuario.</summary>
[ApiController]
[Route("api/v1/admin/cuentas")]
[Authorize(Policy = AdminAccessOptions.PolicyName)]
public sealed class AdminCuentasController : AuthenticatedControllerBase
{
    private const long MaxRequestBytes = 8 * 1024;

    private readonly SearchAccountsService _search;
    private readonly SetAccountStatusService _setStatus;
    private readonly EditAccountService _edit;

    public AdminCuentasController(
        SearchAccountsService search,
        SetAccountStatusService setStatus,
        EditAccountService edit)
    {
        _search = search;
        _setStatus = setStatus;
        _edit = edit;
    }

    /// <summary>EP-M13-02 (HU-073): busca cuentas por nombre o correo, filtra por categoría y pagina.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminCuentaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Search(
        [FromQuery] BuscarCuentasQuery query,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return CreateValidationResult(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["filtro"] = ["El valor de filtro no es válido."]
                });
        }

        var result = await _search
            .SearchAsync(
                query.Buscar,
                query.Filtro ?? AccountSearchFilter.Todos,
                query.Page,
                query.PageSize,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.Status != OperationStatus.Success)
        {
            return ToActionResult(
                result,
                _ => StatusCode(StatusCodes.Status500InternalServerError),
                notFoundDetail: "No se encontraron cuentas.",
                forbiddenDetail: "La cuenta no tiene privilegio administrativo.",
                conflictDetail: "No se pudo completar la búsqueda.");
        }

        var page = result.Value!;
        Response.Headers["X-Total-Count"] = page.Total.ToString(CultureInfo.InvariantCulture);
        Response.Headers["X-Page"] = query.Page.ToString(CultureInfo.InvariantCulture);
        Response.Headers["X-Page-Size"] = query.PageSize.ToString(CultureInfo.InvariantCulture);

        return Ok(page.Items.Select(AdminCuentaResponse.FromItem).ToArray());
    }

    /// <summary>EP-M13-01 (HU-072): suspende o reactiva la cuenta de un usuario.</summary>
    [HttpPut("{accountId:guid}/estado")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(AdminCuentaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetStatus(
        Guid accountId,
        [FromBody] CambiarEstadoCuentaRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        var result = await _setStatus
            .SetStatusAsync(accountId, request.Suspendida, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(AdminCuentaResponse.FromDomain(value)),
            notFoundDetail: "La cuenta no existe.",
            forbiddenDetail: "La cuenta no tiene privilegio administrativo.",
            conflictDetail: "No se pudo completar la operación.");
    }

    /// <summary>EP-M13-03 (HU-074): edita el nombre y el correo de una cuenta.</summary>
    [HttpPut("{accountId:guid}")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(AdminCuentaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Edit(
        Guid accountId,
        [FromBody] EditarCuentaRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is > MaxRequestBytes)
        {
            return CreateProblemResult(
                StatusCodes.Status413PayloadTooLarge,
                "La solicitud excede el tamaño permitido.");
        }

        var result = await _edit
            .EditAsync(accountId, request.NombreCompleto, request.Correo, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(
            result,
            value => Ok(AdminCuentaResponse.FromDomain(value)),
            notFoundDetail: "La cuenta no existe.",
            forbiddenDetail: "La cuenta no tiene privilegio administrativo.",
            conflictDetail: "No se pudo completar la operación.");
    }
}
