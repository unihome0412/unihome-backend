using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UniHome.Api.Configuration;
using UniHome.Api.Security;

namespace UniHome.Api.Controllers;

/// <summary>
/// Canjea un Bearer JWT autorizado (administrador o lector documental) por la
/// cookie HttpOnly de corta vida. Es la vía para Android/Postman; el navegador
/// usa el formulario de /swagger/login. No emite tokens ni acepta credenciales.
/// </summary>
[ApiController]
[Route("api/v1/auth/swagger-session")]
[Authorize(Policy = SwaggerAccessOptions.PolicyName)]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class SwaggerSessionController : ControllerBase
{
    private readonly SwaggerAccessCookie _cookie;
    private readonly ApiOptions _apiOptions;

    public SwaggerSessionController(
        SwaggerAccessCookie cookie,
        IOptions<ApiOptions> apiOptions)
    {
        _cookie = cookie;
        _apiOptions = apiOptions.Value;
    }

    [HttpPost]
    [RequestSizeLimit(1024)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult CreateSession()
    {
        if (!_apiOptions.EnableSwagger)
        {
            return NotFound();
        }

        var subject = User.FindFirst("sub")?.Value;

        if (!Guid.TryParseExact(subject, "N", out var accountId))
        {
            return Unauthorized();
        }

        Response.Headers.CacheControl = "no-store";
        _cookie.Append(Response, accountId);
        return NoContent();
    }

    [HttpDelete]
    [RequestSizeLimit(1024)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DeleteSession()
    {
        if (!_apiOptions.EnableSwagger)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        _cookie.Delete(Response);
        return NoContent();
    }
}
