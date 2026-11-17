using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UniHome.IntegrationTests;

/// <summary>
/// Reproduce el transporte HTTP del runner DAST detrás de un proxy loopback
/// confiable. ForwardedHeaders solo acepta X-Forwarded-Proto cuando la conexión
/// viene de Proxy:KnownProxies; un request HTTP directo no obtiene una sesión
/// documental insegura.
/// </summary>
public sealed class SwaggerForwardedHeadersTests
    : IClassFixture<SwaggerTrustedProxyApiFactory>
{
    private readonly SwaggerTrustedProxyApiFactory _factory;

    public SwaggerForwardedHeadersTests(SwaggerTrustedProxyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HttpSinForwardedProto_RedirigeSinFormularioNiCookie()
    {
        using var client = CreateHttpClient();

        using var response = await client.GetAsync("/swagger/login");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.StartsWith(
            "https://",
            response.Headers.Location!.OriginalString,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "__RequestVerificationToken",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        Assert.DoesNotContain("__Host-unihome-antiforgery", SetCookie(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HttpConForwardedProtoHttps_EmiteFormularioYCookieSegura()
    {
        using var client = CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/swagger/login");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("name=\"__RequestVerificationToken\"", body, StringComparison.Ordinal);

        var cookie = SetCookie(response);
        Assert.Contains("__Host-unihome-antiforgery=", cookie, StringComparison.Ordinal);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HttpConForwardedProtoHttp_RedirigeSinCookie()
    {
        using var client = CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/swagger/login");
        request.Headers.Add("X-Forwarded-Proto", "http");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.StartsWith(
            "https://",
            response.Headers.Location!.OriginalString,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("__Host-unihome-antiforgery", SetCookie(response), StringComparison.Ordinal);
    }

    private HttpClient CreateHttpClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });

    private static string SetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? string.Join("\n", values)
            : string.Empty;
}
