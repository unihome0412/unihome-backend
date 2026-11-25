using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniHome.Api.Contracts.Summary;
using UniHome.Application.Summary;

namespace UniHome.Api.Controllers;

/// <summary>EP-M05/M10/M11 (HU-034): pantalla de Resumen del titular.</summary>
[ApiController]
public sealed class SummaryController : AuthenticatedControllerBase
{
    private readonly GetMySummaryService _getSummary;

    public SummaryController(GetMySummaryService getSummary)
    {
        _getSummary = getSummary;
    }

    [HttpGet("/api/v1/mi-resumen")]
    [Authorize]
    [ProducesResponseType(typeof(ResumenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return CreateProblemResult(StatusCodes.Status401Unauthorized, "La sesión no es válida.");
        }

        Response.Headers.CacheControl = "no-store";

        var view = await _getSummary.GetAsync(accountId, cancellationToken).ConfigureAwait(false);

        return Ok(ResumenResponse.FromView(view));
    }
}
