using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Publications.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Universities.Abstractions;
using UniHome.Infrastructure.Configuration;
using UniHome.IntegrationTests.TestSupport;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace UniHome.IntegrationTests;

/// <summary>
/// Fábrica de pruebas: fuerza MongoDB sin configurar para que readiness no dependa
/// de un cluster real, sustituye cuentas, sesiones y envío de correo por dobles en
/// memoria, configura una clave JWT exclusiva de pruebas y agrega controladores
/// exclusivos de prueba.
/// </summary>
public class UniHomeApiFactory : WebApplicationFactory<Program>
{
    public const string TestSigningKey =
        "unihome-test-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz";

    public const string TestIssuer = "UniHome.Test";
    public const string TestAudience = "UniHome.Mobile.Test";

    /// <summary>
    /// Secreto HMAC fijo de pruebas (no es un secreto real) para que las pruebas
    /// puedan reproducir el mismo <c>HmacVerificationCodeProtector</c> que usa el
    /// host; sin esta fijación el protector real genera una clave efímera
    /// aleatoria por proceso y las pruebas no podrían construir códigos propios.
    /// </summary>
    public const string TestVerificationCodeHmacSecret =
        "unihome-test-verification-code-hmac-secret-0123456789";

    /// <summary>Cuenta interna usada solo por las pruebas de rutas /admin.</summary>
    public static readonly Guid TestAdminAccountId =
        Guid.Parse("d3b07384-d9a0-4f5a-8a8d-1a2b3c4d5e6f");

    public InMemoryAccountRepository Accounts { get; } = new();

    public InMemoryRefreshSessionRepository Sessions { get; } = new();

    public InMemoryPasswordResetRepository PasswordResets { get; } = new();

    public InMemoryUniversityRepository Universities { get; } = new();

    public InMemoryPropertyRepository Properties { get; } = new();

    public InMemoryUnitRepository Units { get; } = new();

    public InMemoryTermsAcceptanceRepository Terms { get; } = new();

    public InMemoryUnitPhotoStorage PhotoStorage { get; } = new();

    public CapturingVerificationEmailSender EmailSender { get; } = new();

    public CapturingPasswordResetEmailSender PasswordResetEmailSender { get; } = new();

    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDb:ConnectionString"] = string.Empty,
                ["MongoDb:DatabaseName"] = string.Empty,
                ["MongoDb:ServerSelectionTimeoutSeconds"] = "1",
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["Jwt:SigningKey"] = TestSigningKey,
                ["Jwt:AccessTokenMinutes"] = "60",
                ["Jwt:RefreshTokenDays"] = "30",
                ["VerificationCode:HmacSecret"] = TestVerificationCodeHmacSecret,
                ["Admin:AccountIds:0"] = TestAdminAccountId.ToString("N"),
                ["Redis:Enabled"] = "false",
                ["Auth:RateLimit:IpHashSalt"] = "unihome-dev-test-salt-0123456789-abcdef",
                ["Auth:RateLimit:LoginPermitLimit"] = "1000",
                ["Auth:RateLimit:LoginWindowSeconds"] = "60",
                ["Auth:RateLimit:RefreshPermitLimit"] = "1000",
                ["Auth:RateLimit:RefreshWindowSeconds"] = "60",
                ["Auth:RateLimit:ConfirmEmailPermitLimit"] = "1000",
                ["Auth:RateLimit:ConfirmEmailWindowSeconds"] = "60",
                ["Auth:RateLimit:ResendPermitLimit"] = "1000",
                ["Auth:RateLimit:ResendWindowSeconds"] = "60",
                ["Auth:RateLimit:PasswordResetRequestPermitLimit"] = "1000",
                ["Auth:RateLimit:PasswordResetRequestWindowSeconds"] = "60",
                ["Auth:RateLimit:PasswordResetConfirmPermitLimit"] = "1000",
                ["Auth:RateLimit:PasswordResetConfirmWindowSeconds"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<MongoDbOptions>(options =>
            {
                options.ConnectionString = string.Empty;
                options.DatabaseName = string.Empty;
                options.ServerSelectionTimeoutSeconds = 1;
            });

            services.RemoveAll<IAccountRepository>();
            services.AddSingleton<IAccountRepository>(Accounts);

            services.RemoveAll<IRefreshSessionRepository>();
            services.AddSingleton<IRefreshSessionRepository>(Sessions);

            services.RemoveAll<IPasswordResetRepository>();
            services.AddSingleton<IPasswordResetRepository>(PasswordResets);

            services.RemoveAll<IUniversityRepository>();
            services.AddSingleton<IUniversityRepository>(Universities);

            services.RemoveAll<IPropertyRepository>();
            services.AddSingleton<IPropertyRepository>(Properties);

            services.RemoveAll<IUnitRepository>();
            services.AddSingleton<IUnitRepository>(Units);

            services.RemoveAll<ITermsAcceptanceRepository>();
            services.AddSingleton<ITermsAcceptanceRepository>(Terms);

            services.RemoveAll<IUnitPhotoStorage>();
            services.AddSingleton<IUnitPhotoStorage>(PhotoStorage);

            services.RemoveAll<IVerificationEmailSender>();
            services.AddSingleton<IVerificationEmailSender>(EmailSender);

            services.RemoveAll<IPasswordResetEmailSender>();
            services.AddSingleton<IPasswordResetEmailSender>(PasswordResetEmailSender);

            services
                .AddControllers()
                .AddApplicationPart(typeof(TestOnlyProbeController).Assembly);
        });
    }
}
