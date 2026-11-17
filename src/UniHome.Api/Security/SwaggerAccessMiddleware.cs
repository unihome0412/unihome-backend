using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using UniHome.Api.Configuration;

namespace UniHome.Api.Security;

/// <summary>
/// Protege los recursos estáticos de Swagger UI, que no pasan por endpoint
/// routing, redirige al formulario de acceso cuando no hay sesión y emite la
/// cookie documental cuando la petición ya se autenticó con Bearer. El
/// documento OpenAPI conserva su propia metadata de autorización.
/// </summary>
public sealed class SwaggerAccessMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAuthorizationService _authorization;
    private readonly SwaggerAccessCookie _cookie;

    public SwaggerAccessMiddleware(
        RequestDelegate next,
        IAuthorizationService authorization,
        SwaggerAccessCookie cookie)
    {
        _next = next;
        _authorization = authorization;
        _cookie = cookie;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;

        // El formulario de acceso es el único recurso público bajo /swagger.
        if (SwaggerAccessOptions.IsLoginPath(path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var isSwaggerUi = path.StartsWithSegments(
            "/swagger",
            StringComparison.OrdinalIgnoreCase);
        var isOpenApi = path.StartsWithSegments(
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
                else if (HttpMethods.IsGet(context.Request.Method) ||
                         HttpMethods.IsHead(context.Request.Method))
                {
                    context.Response.Redirect(SwaggerAccessOptions.LoginPath);
                }
                else
                {
                    await context.ChallengeAsync().ConfigureAwait(false);
                }

                return;
            }
        }

        // Un administrador o un lector documental que llega con Bearer puede
        // canjearlo una sola vez por la cookie HttpOnly; así el navegador puede
        // cargar los recursos sin exponer el JWT en la URL ni en el HTML.
        if (context.Request.Headers.Authorization.ToString().StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase) &&
            !context.Request.Cookies.ContainsKey(SwaggerAccessOptions.CookieName) &&
            Guid.TryParseExact(
                context.User.FindFirst("sub")?.Value,
                "N",
                out var accountId))
        {
            _cookie.Append(context.Response, accountId);
        }

        await _next(context).ConfigureAwait(false);
    }
}
