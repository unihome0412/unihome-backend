using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using UniHome.Application.Identity.Abstractions;
using UniHome.Infrastructure.Configuration;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Fábrica opt-in que usa los repositorios MongoDB reales y reemplaza únicamente
/// el envío de correo por un doble en memoria. La configuración de la API se
/// reemplaza para no arrastrar SMTP ni la base de desarrollo desde User Secrets.
/// </summary>
public sealed class MongoBackedApiFactory : WebApplicationFactory<Program>
{
    public CapturingVerificationEmailSender EmailSender { get; } = new();
    public RefreshRotationBarrier RotationBarrier { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.Sources.Clear();
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDb:ConnectionString"] = MongoIntegrationEnvironment.ConnectionString,
                ["MongoDb:DatabaseName"] = MongoIntegrationEnvironment.DatabaseName,
                ["MongoDb:ServerSelectionTimeoutSeconds"] = "15",
                ["Jwt:Issuer"] = UniHomeApiFactory.TestIssuer,
                ["Jwt:Audience"] = UniHomeApiFactory.TestAudience,
                ["Jwt:SigningKey"] = UniHomeApiFactory.TestSigningKey,
                ["Jwt:AccessTokenMinutes"] = "60",
                ["Jwt:RefreshTokenDays"] = "30",
                ["VerificationCode:HmacSecret"] =
                    "unihome-mongo-integration-hmac-secret-0123456789-abcdef",
                ["Auth:RateLimit:LoginPermitLimit"] = "1000",
                ["Auth:RateLimit:LoginWindowSeconds"] = "60",
                ["Auth:RateLimit:RefreshPermitLimit"] = "1000",
                ["Auth:RateLimit:RefreshWindowSeconds"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            // La prueba no envía correos: se elimina la validación SMTP que pudo
            // haberse registrado desde User Secrets, pero se conserva el resto de
            // la composición de Infrastructure (incluidos los repositorios reales).
            services.RemoveAll<IValidateOptions<SmtpOptions>>();
            services.RemoveAll<IVerificationEmailSender>();
            services.AddSingleton<IVerificationEmailSender>(EmailSender);
            var original = services.Single(service => service.ServiceType == typeof(IRefreshSessionRepository));
            services.Replace(ServiceDescriptor.Singleton<IRefreshSessionRepository>(provider =>
                new CoordinatedRefreshSessionRepository(
                    (IRefreshSessionRepository)ActivatorUtilities.CreateInstance(provider, original.ImplementationType!),
                    RotationBarrier)));
        });
    }
}
