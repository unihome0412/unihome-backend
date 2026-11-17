using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using UniHome.Api.Configuration;

namespace UniHome.Api.Security;

/// <summary>
/// Protege los recursos estáticos de Swagger UI, que no pasan por endpoint
/// routing, y emite la cookie de documentación cuando la petición ya se
/// autenticó con un Bearer administrativo. El documento OpenAPI conserva su
/// propia metadata de autorización mediante <c>RequireAuthorization</c>.
/// </summary>
public sealed class SwaggerAccessMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAuthorizationService _authorization;

    public SwaggerAccessMiddleware(
        RequestDelegate next,
        IAuthorizationService authorization)
    {
        _next = next;
        _authorization = authorization;
    }

    public async Task InvokeAsync(
        HttpContext context,
        SwaggerAccessTicketService tickets,
        IOptions<SwaggerAccessOptions> accessOptions)
    {
        var lifetime = accessOptions.Value.Lifetime;
        var isSwaggerUi = context.Request.Path.StartsWithSegments(
            "/swagger",
            StringComparison.OrdinalIgnoreCase);
        var isOpenApi = context.Request.Path.StartsWithSegments(
            "/openapi",
            StringComparison.OrdinalIgnoreCase);

        if (!isSwaggerUi && !isOpenApi)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (isSwaggerUi)
        {
            var result = await _authorization
                .AuthorizeAsync(context.User, SwaggerAccessOptions.PolicyName)
                .ConfigureAwait(false);

            if (!result.Succeeded)
            {
                context.Response.Headers.CacheControl = "no-store";

                if (context.User.Identity?.IsAuthenticated == true)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                }
                else
                {
                    await context.ChallengeAsync().ConfigureAwait(false);
                }

                return;
            }
        }

        // Un administrador que llega con Bearer puede canjearlo una sola vez por
        // la cookie HttpOnly; así el navegador puede cargar los recursos sin
        // exponer el JWT en la URL ni en el HTML.
        if (context.Request.Headers.Authorization.ToString().StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase) &&
            !context.Request.Cookies.ContainsKey(SwaggerAccessOptions.CookieName) &&
            Guid.TryParseExact(
                context.User.FindFirst("sub")?.Value,
                "N",
                out var accountId))
        {
            context.Response.Cookies.Append(
                SwaggerAccessOptions.CookieName,
                tickets.Issue(accountId),
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Path = "/",
                    IsEssential = true,
                    MaxAge = lifetime,
                    Expires = DateTimeOffset.UtcNow.Add(lifetime)
                });
        }

        await _next(context).ConfigureAwait(false);
    }
}
