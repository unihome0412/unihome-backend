using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniHome.Application.Identity.Abstractions;
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

    protected virtual bool TrustForwardedHeaders => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs valida VerificationCode:HmacSecret leyendo directamente
        // builder.Configuration antes de que WebApplicationFactory pueda inyectar
        // configuración de prueba (eso solo surte efecto a partir de Build()).
        // Las variables de entorno sí están presentes desde el arranque del
        // proceso, por eso se fijan aquí y no solo en el IConfigurationBuilder.
        Environment.SetEnvironmentVariable("VerificationCode__HmacSecret", TestVerificationCodeHmacSecret);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestSigningKey);

        builder.UseEnvironment("Production");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
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
                ["Proxy:TrustForwardedHeaders"] = TrustForwardedHeaders ? "true" : "false",
                ["Auth:RateLimit:LoginPermitLimit"] = "2",
                ["Auth:RateLimit:LoginWindowSeconds"] = "60",
                ["Auth:RateLimit:RefreshPermitLimit"] = "1000",
                ["Auth:RateLimit:RefreshWindowSeconds"] = "60",
                ["Auth:RateLimit:ConfirmEmailPermitLimit"] = "1000",
                ["Auth:RateLimit:ConfirmEmailWindowSeconds"] = "60",
                ["Auth:RateLimit:ResendPermitLimit"] = "1000",
                ["Auth:RateLimit:ResendWindowSeconds"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            // TestServer no expone una dirección HTTPS real; se fija el puerto
            // para que UseHttpsRedirection calcule un Location determinista.
            services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = 443);

            // Sin esto, login golpea MongoDbClientProvider sin configurar y
            // responde 503 antes de llegar al rate limiter que se quiere probar.
            services.RemoveAll<IAccountRepository>();
            services.AddSingleton<IAccountRepository>(new InMemoryAccountRepository());

            services.RemoveAll<IRefreshSessionRepository>();
            services.AddSingleton<IRefreshSessionRepository>(new InMemoryRefreshSessionRepository());
        });
    }
}
