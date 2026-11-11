using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniHome.Application.Abstractions.RateLimiting;
using UniHome.Application.Identity.Abstractions;
using UniHome.Infrastructure.Redis;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Fábrica en entorno Production (no Development) para probar HSTS, redirección
/// HTTPS y encabezados reenviados tal como se comportan fuera de Development.
/// <see cref="TrustForwardedHeaders"/> es <c>false</c> por defecto, igual que en
/// producción real.
/// </summary>
public class ProductionApiFactory : WebApplicationFactory<Program>
{
    public const string TestSigningKey =
        "unihome-production-test-signing-key-0123456789-abcdefg";

    public const string TestVerificationCodeHmacSecret =
        "unihome-production-test-verification-code-hmac-secret-01234";

    public const string TestIpHashSalt =
        "unihome-production-test-salt-0123456789-abcdef";

    protected virtual bool TrustForwardedHeaders => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs valida VerificationCode:HmacSecret, Jwt:SigningKey, Auth:RateLimit:IpHashSalt
        // y Redis:Enabled leyendo directamente builder.Configuration antes de que WebApplicationFactory
        // pueda inyectar configuración de prueba. Las variables de entorno sí están presentes desde el arranque.
        Environment.SetEnvironmentVariable("VerificationCode__HmacSecret", TestVerificationCodeHmacSecret);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestSigningKey);
        Environment.SetEnvironmentVariable("Auth__RateLimit__IpHashSalt", TestIpHashSalt);
        Environment.SetEnvironmentVariable("Redis__Enabled", "true");
        Environment.SetEnvironmentVariable("Redis__RestUrl", "https://test-mock.upstash.io");
        Environment.SetEnvironmentVariable("Redis__RestToken", "mock-token-test");

        if (TrustForwardedHeaders)
        {
            Environment.SetEnvironmentVariable("Proxy__TrustForwardedHeaders", "true");
            Environment.SetEnvironmentVariable("Proxy__KnownProxies__0", "127.0.0.1");
            Environment.SetEnvironmentVariable("Proxy__KnownProxies__1", "::1");
        }
        else
        {
            Environment.SetEnvironmentVariable("Proxy__TrustForwardedHeaders", "false");
        }

        builder.UseEnvironment("Production");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var config = new Dictionary<string, string?>
            {
                ["MongoDb:ConnectionString"] = string.Empty,
                ["MongoDb:DatabaseName"] = string.Empty,
                ["MongoDb:ServerSelectionTimeoutSeconds"] = "1",
                ["Jwt:Issuer"] = "UniHome.Test",
                ["Jwt:Audience"] = "UniHome.Mobile.Test",
                ["Jwt:SigningKey"] = TestSigningKey,
                ["Jwt:AccessTokenMinutes"] = "60",
                ["Jwt:RefreshTokenDays"] = "30",
                ["VerificationCode:HmacSecret"] = TestVerificationCodeHmacSecret,
                ["Auth:RateLimit:IpHashSalt"] = TestIpHashSalt,
                ["Redis:Enabled"] = "true",
                ["Redis:RestUrl"] = "https://test-mock.upstash.io",
                ["Redis:RestToken"] = "mock-token-test",
                ["Proxy:TrustForwardedHeaders"] = TrustForwardedHeaders ? "true" : "false",
                ["Auth:RateLimit:LoginPermitLimit"] = "2",
                ["Auth:RateLimit:LoginWindowSeconds"] = "60",
                ["Auth:RateLimit:RefreshPermitLimit"] = "1000",
                ["Auth:RateLimit:RefreshWindowSeconds"] = "60",
                ["Auth:RateLimit:ConfirmEmailPermitLimit"] = "1000",
                ["Auth:RateLimit:ConfirmEmailWindowSeconds"] = "60",
                ["Auth:RateLimit:ResendPermitLimit"] = "1000",
                ["Auth:RateLimit:ResendWindowSeconds"] = "60"
            };

            if (TrustForwardedHeaders)
            {
                config["Proxy:KnownProxies:0"] = "127.0.0.1";
                config["Proxy:KnownProxies:1"] = "::1";
            }

            configurationBuilder.AddInMemoryCollection(config);
        });

        builder.ConfigureServices(services =>
        {
            services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = 443);

            services.RemoveAll<IAccountRepository>();
            services.AddSingleton<IAccountRepository>(new InMemoryAccountRepository());

            services.RemoveAll<IRefreshSessionRepository>();
            services.AddSingleton<IRefreshSessionRepository>(new InMemoryRefreshSessionRepository());

            services.RemoveAll<IDistributedRateLimiter>();
            services.AddSingleton<IDistributedRateLimiter, InMemoryDistributedRateLimiter>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Environment.SetEnvironmentVariable("Proxy__TrustForwardedHeaders", null);
        Environment.SetEnvironmentVariable("Proxy__KnownProxies__0", null);
        Environment.SetEnvironmentVariable("Proxy__KnownProxies__1", null);
        Environment.SetEnvironmentVariable("Auth__RateLimit__IpHashSalt", null);
        Environment.SetEnvironmentVariable("Redis__Enabled", null);
        Environment.SetEnvironmentVariable("Redis__RestUrl", null);
        Environment.SetEnvironmentVariable("Redis__RestToken", null);
    }
}
