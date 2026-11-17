using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace UniHome.IntegrationTests;

/// <summary>
/// Simula el ingress de Azure Container Apps: el host de pruebas recibe HTTP
/// desde loopback, pero solo confía en X-Forwarded-Proto si la conexión viene
/// de un proxy loopback conocido. No se habilita esta confianza en producción
/// fuera de la allowlist configurada.
/// </summary>
public sealed class SwaggerTrustedProxyApiFactory : SwaggerEnabledApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs lee ProxyOptions antes de que ConfigureAppConfiguration
        // del host de pruebas se aplique; igual que ProductionApiFactory, la
        // allowlist se fija por entorno antes de construir la aplicación.
        Environment.SetEnvironmentVariable("Proxy__TrustForwardedHeaders", "true");
        Environment.SetEnvironmentVariable("Proxy__KnownProxies__0", "127.0.0.1");
        Environment.SetEnvironmentVariable("Proxy__KnownProxies__1", "::1");

        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Proxy:TrustForwardedHeaders"] = "true",
                ["Proxy:KnownProxies:0"] = "127.0.0.1",
                ["Proxy:KnownProxies:1"] = "::1"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Environment.SetEnvironmentVariable("Proxy__TrustForwardedHeaders", null);
        Environment.SetEnvironmentVariable("Proxy__KnownProxies__0", null);
        Environment.SetEnvironmentVariable("Proxy__KnownProxies__1", null);
    }
}
