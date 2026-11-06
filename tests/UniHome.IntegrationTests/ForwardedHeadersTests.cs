using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UniHome.IntegrationTests;

/// <summary>
/// Container Apps termina TLS en su ingress y reenvía HTTP al proceso. Estas
/// pruebas corren en entorno Production (no Development) para ejercitar HSTS,
/// redirección HTTPS y el rate limiter tal como se comportan fuera de Development.
/// </summary>
public sealed class ForwardedHeadersTests :
    IClassFixture<ProductionWithForwardedHeadersApiFactory>,
    IClassFixture<ProductionApiFactory>
{
    private readonly ProductionWithForwardedHeadersApiFactory _enabled;
    private readonly ProductionApiFactory _disabled;

    public ForwardedHeadersTests(
        ProductionWithForwardedHeadersApiFactory enabled,
        ProductionApiFactory disabled)
    {
        _enabled = enabled;
        _disabled = disabled;
    }

    [Fact]
    public async Task ConHttpsReenviado_NoRedirige()
    {
        using var client = CreateNoRedirectClient(_enabled);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ConHttpReenviado_Redirige()
    {
        using var client = CreateNoRedirectClient(_enabled);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Forwarded-Proto", "http");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.StartsWith("https://", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task IpDelClienteReenviada_SeUsaParaElRateLimit()
    {
        using var client = _enabled.CreateClient();

        // Límite configurado en 2 por IP; dos solicitudes desde la misma IP
        // reenviada agotan su cupo, pero una tercera IP distinta sigue libre.
        using var first = await LoginAsync(client, "1.1.1.1");
        using var second = await LoginAsync(client, "1.1.1.1");
        using var fromOtherIp = await LoginAsync(client, "2.2.2.2");
        using var third = await LoginAsync(client, "1.1.1.1");

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, fromOtherIp.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Fact]
    public async Task ConOpcionDesactivada_IgnoraLosEncabezados()
    {
        using var client = _disabled.CreateClient();

        // Con la opción desactivada, X-Forwarded-For no cambia la partición del
        // limitador: las tres solicitudes comparten el mismo cupo de la IP real
        // de la conexión de prueba, sin importar el encabezado.
        using var first = await LoginAsync(client, "1.1.1.1");
        using var second = await LoginAsync(client, "1.1.1.1");
        using var withDifferentHeader = await LoginAsync(client, "2.2.2.2");

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, withDifferentHeader.StatusCode);
    }

    private static HttpClient CreateNoRedirectClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string forwardedFor)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new
            {
                correo = AuthTestHelpers.UniqueEmail(),
                contrasena = "OtraClave1"
            })
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return client.SendAsync(request);
    }
}
