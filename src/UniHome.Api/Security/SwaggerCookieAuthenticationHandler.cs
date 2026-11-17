using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using UniHome.Api.Configuration;
using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.Api.Security;

/// <summary>
/// Autentica la cookie de documentación solo en las rutas de Swagger/OpenAPI.
/// Vuelve a comprobar que la cuenta siga activa y confirmada (BE-SEC-008) y la
/// policy administrativa revalida la allowlist. La cookie no autentica /api/*.
/// </summary>
public sealed class SwaggerCookieAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly SwaggerAccessTicketService _tickets;
    private readonly IAccountRepository _accounts;

    public SwaggerCookieAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        SwaggerAccessTicketService tickets,
        IAccountRepository accounts)
        : base(options, logger, encoder)
    {
        _tickets = tickets;
        _accounts = accounts;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(SwaggerAccessOptions.CookieName, out var ticket) ||
            !_tickets.TryValidate(ticket, out var accountId))
        {
            return AuthenticateResult.NoResult();
        }

        var account = await _accounts
            .FindByIdAsync(accountId, Context.RequestAborted)
            .ConfigureAwait(false);

        if (account is null ||
            account.Status != AccountStatus.Active ||
            !account.IsEmailConfirmed)
        {
            return AuthenticateResult.Fail("La cuenta no está habilitada.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim("sub", account.Id.ToString("N", CultureInfo.InvariantCulture)),
                new Claim("auth_method", "swagger_cookie")
            ],
            Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        return AuthenticateResult.Success(
            new AuthenticationTicket(principal, properties, Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.CacheControl = "no-store";
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.Headers.CacheControl = "no-store";
        return Task.CompletedTask;
    }
}
