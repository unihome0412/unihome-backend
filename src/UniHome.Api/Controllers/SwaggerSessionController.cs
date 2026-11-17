using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UniHome.Api.Configuration;
using UniHome.Api.Security;

namespace UniHome.Api.Controllers;

/// <summary>
/// Canjea un Bearer JWT administrativo por una cookie HttpOnly de corta vida
/// para abrir Swagger UI en el navegador. No emite tokens, no acepta credenciales
/// y no se documenta en OpenAPI. La cookie solo autentica /swagger y /openapi;
/// las operaciones de la API siguen exigiendo Bearer.
/// </summary>
[ApiController]
[Route("api/v1/auth/swagger-session")]
[Authorize(Policy = AdminAccessOptions.PolicyName)]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class SwaggerSessionController : ControllerBase
{
    private readonly SwaggerAccessTicketService _tickets;
    private readonly SwaggerAccessOptions _accessOptions;
    private readonly ApiOptions _apiOptions;

    public SwaggerSessionController(
        SwaggerAccessTicketService tickets,
        IOptions<SwaggerAccessOptions> accessOptions,
        IOptions<ApiOptions> apiOptions)
    {
        _tickets = tickets;
        _accessOptions = accessOptions.Value;
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
        Response.Cookies.Append(
            SwaggerAccessOptions.CookieName,
            _tickets.Issue(accountId),
            CreateCookieOptions(_accessOptions.Lifetime));

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
        Response.Cookies.Delete(
            SwaggerAccessOptions.CookieName,
            CreateCookieOptions(TimeSpan.Zero));

        return NoContent();
    }

    private static CookieOptions CreateCookieOptions(TimeSpan lifetime) =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
            MaxAge = lifetime == TimeSpan.Zero ? null : lifetime,
            Expires = lifetime == TimeSpan.Zero
                ? DateTimeOffset.UnixEpoch
                : DateTimeOffset.UtcNow.Add(lifetime)
        };
}
