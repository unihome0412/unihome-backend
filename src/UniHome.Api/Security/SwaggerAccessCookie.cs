using Microsoft.Extensions.Options;
using UniHome.Api.Configuration;

namespace UniHome.Api.Security;

/// <summary>
/// Emite y elimina la cookie de documentación con una definición única para el
/// middleware, el canje Bearer y el formulario del navegador.
/// </summary>
public sealed class SwaggerAccessCookie
{
    private readonly SwaggerAccessTicketService _tickets;
    private readonly SwaggerAccessOptions _options;

    public SwaggerAccessCookie(
        SwaggerAccessTicketService tickets,
        IOptions<SwaggerAccessOptions> options)
    {
        _tickets = tickets;
        _options = options.Value;
    }

    public void Append(HttpResponse response, Guid accountId)
    {
        response.Headers.CacheControl = "no-store";
        response.Cookies.Append(
            SwaggerAccessOptions.CookieName,
            _tickets.Issue(accountId),
            CreateOptions(_options.Lifetime));
    }

    public void Delete(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store";
        response.Cookies.Delete(
            SwaggerAccessOptions.CookieName,
            CreateOptions(TimeSpan.Zero));
    }

    private static CookieOptions CreateOptions(TimeSpan lifetime) =>
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
