using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Options;

namespace UniHome.Api.Security;

/// <summary>
/// Emite HSTS cuando la petición llegó por HTTPS o cuando el ingress terminó
/// TLS y envió <c>X-Forwarded-Proto: https</c>. La cabecera reenviada se usa
/// únicamente para HSTS: no cambia el esquema, la IP, el rate limiting ni la
/// autorización, por lo que un valor falsificado no puede elevar privilegios.
/// Se conserva la política de 365 días, includeSubDomains y sin preload.
/// </summary>
public sealed class StrictTransportSecurityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HstsOptions _options;

    public StrictTransportSecurityMiddleware(
        RequestDelegate next,
        IOptions<HstsOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldEmit(context.Request))
        {
            context.Response.Headers.StrictTransportSecurity = BuildPolicy();
        }

        await _next(context).ConfigureAwait(false);
    }

    private bool ShouldEmit(HttpRequest request)
    {
        if (!request.IsHttps && !HasForwardedHttps(request))
        {
            return false;
        }

        var host = request.Host.Host;

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "127.0.0.1", StringComparison.Ordinal) ||
            string.Equals(host, "::1", StringComparison.Ordinal) ||
            string.Equals(host, "[::1]", StringComparison.Ordinal))
        {
            return false;
        }

        return !_options.ExcludedHosts.Any(excluded =>
            string.Equals(excluded, host, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasForwardedHttps(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("X-Forwarded-Proto", out var values))
        {
            return false;
        }

        foreach (var value in values)
        {
            var first = value?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(first))
            {
                return string.Equals(first, "https", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    private string BuildPolicy()
    {
        var policy = $"max-age={(long)_options.MaxAge.TotalSeconds}";

        if (_options.IncludeSubDomains)
        {
            policy += "; includeSubDomains";
        }

        if (_options.Preload)
        {
            policy += "; preload";
        }

        return policy;
    }
}
