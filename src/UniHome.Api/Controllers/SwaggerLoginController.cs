using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using UniHome.Api.Authorization;
using UniHome.Api.Configuration;
using UniHome.Api.Security;
using UniHome.Application.Identity.Login;

namespace UniHome.Api.Controllers;

/// <summary>
/// Login y logout de la documentación desde el navegador. Reutiliza las reglas
/// de credenciales de LoginService, el permiso documental y la cookie HMAC
/// existente. Nunca devuelve un JWT al navegador.
/// </summary>
[ApiController]
[Route("swagger")]
[ApiExplorerSettings(IgnoreApi = true)]
// La exigencia de HTTPS se delega al filtro estándar de ASP.NET Core. No se
// usa Request.IsHttps como condición de autenticación o antiforgery.
[SwaggerRequireHttps]
public sealed class SwaggerLoginController : ControllerBase
{
    private const long MaxFormBytes = 8 * 1024;

    private readonly LoginService _login;
    private readonly DocumentationAccessEvaluator _documentationAccess;
    private readonly SwaggerAccessCookie _cookie;
    private readonly IAntiforgery _antiforgery;
    private readonly IAuthorizationService _authorization;
    private readonly ApiOptions _apiOptions;

    public SwaggerLoginController(
        LoginService login,
        DocumentationAccessEvaluator documentationAccess,
        SwaggerAccessCookie cookie,
        IAntiforgery antiforgery,
        IAuthorizationService authorization,
        IOptions<ApiOptions> apiOptions)
    {
        _login = login;
        _documentationAccess = documentationAccess;
        _cookie = cookie;
        _antiforgery = antiforgery;
        _authorization = authorization;
        _apiOptions = apiOptions.Value;
    }

    [HttpGet("login")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> LoginPage(
        [FromQuery] string? logout,
        CancellationToken cancellationToken)
    {
        if (!_apiOptions.EnableSwagger)
        {
            return NotFound();
        }

        // Si ya hay una cookie documental válida, no se muestra el formulario.
        if (User.Identity?.IsAuthenticated == true)
        {
            var authorized = await _authorization
                .AuthorizeAsync(User, SwaggerAccessOptions.PolicyName)
                .ConfigureAwait(false);

            if (authorized.Succeeded)
            {
                return Redirect("/swagger");
            }
        }

        var tokens = GetAntiforgeryTokens();
        return Html(SwaggerLoginPage.Login(
            tokens.FormFieldName,
            tokens.RequestToken,
            loggedOut: string.Equals(logout, "1", StringComparison.Ordinal)));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimitPolicies.Login)]
    [RequestSizeLimit(MaxFormBytes)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Login(
        CancellationToken cancellationToken)
    {
        if (!_apiOptions.EnableSwagger)
        {
            return NotFound();
        }

        if (!await ValidateAntiforgeryAsync().ConfigureAwait(false))
        {
            var tokens = GetAntiforgeryTokens();
            return Html(
                SwaggerLoginPage.Login(
                    tokens.FormFieldName,
                    tokens.RequestToken,
                    "No se pudo verificar el formulario. Intenta de nuevo."),
                StatusCodes.Status400BadRequest);
        }

        var form = Request.Form;
        var email = form["correo"].ToString();
        var password = form["contrasena"].ToString();

        var authentication = await _login
            .AuthenticateAsync(new LoginCommand(email, password), cancellationToken)
            .ConfigureAwait(false);

        if (!authentication.Succeeded ||
            authentication.AccountId is not Guid accountId ||
            !_documentationAccess.IsAllowed(accountId))
        {
            // Mismo mensaje para credenciales inválidas, cuenta no habilitada o
            // cuenta sin permiso documental. No se registra el correo.
            var tokens = GetAntiforgeryTokens();
            return Html(
                SwaggerLoginPage.Login(
                    tokens.FormFieldName,
                    tokens.RequestToken,
                    "No se pudo iniciar sesión con esas credenciales.",
                    email),
                StatusCodes.Status401Unauthorized);
        }

        _cookie.Append(Response, accountId);
        return Redirect("/swagger");
    }

    [HttpGet("logout")]
    [Authorize(Policy = SwaggerAccessOptions.PolicyName)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult LogoutPage()
    {
        if (!_apiOptions.EnableSwagger)
        {
            return NotFound();
        }

        var tokens = GetAntiforgeryTokens();
        return Html(SwaggerLoginPage.Logout(tokens.FormFieldName, tokens.RequestToken));
    }

    [HttpPost("logout")]
    [Authorize(Policy = SwaggerAccessOptions.PolicyName)]
    [RequestSizeLimit(MaxFormBytes)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Logout()
    {
        if (!_apiOptions.EnableSwagger)
        {
            return NotFound();
        }

        if (!await ValidateAntiforgeryAsync().ConfigureAwait(false))
        {
            var tokens = GetAntiforgeryTokens();
            return Html(
                SwaggerLoginPage.Logout(tokens.FormFieldName, tokens.RequestToken),
                StatusCodes.Status400BadRequest);
        }

        _cookie.Delete(Response);
        return Redirect("/swagger/login?logout=1");
    }

    private (string FormFieldName, string RequestToken) GetAntiforgeryTokens()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return (tokens.FormFieldName, tokens.RequestToken!);
    }

    private async Task<bool> ValidateAntiforgeryAsync()
    {
        if (!Request.HasFormContentType)
        {
            return false;
        }

        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext).ConfigureAwait(false);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private ContentResult Html(string html, int statusCode = StatusCodes.Status200OK) =>
        new()
        {
            Content = html,
            ContentType = "text/html; charset=utf-8",
            StatusCode = statusCode
        };
}
