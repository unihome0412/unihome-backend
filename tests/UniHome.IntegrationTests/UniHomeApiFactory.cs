using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniHome.Application.Identity.Abstractions;
using UniHome.Infrastructure.Configuration;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Fábrica de pruebas: fuerza MongoDB sin configurar para que readiness no dependa
/// de un cluster real, sustituye la persistencia de cuentas y el envío de correo
/// por dobles en memoria, y agrega controladores exclusivos de prueba.
/// </summary>
public sealed class UniHomeApiFactory : WebApplicationFactory<Program>
{
    public InMemoryAccountRepository Accounts { get; } = new();

    public CapturingVerificationEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDb:ConnectionString"] = string.Empty,
                ["MongoDb:DatabaseName"] = string.Empty,
                ["MongoDb:ServerSelectionTimeoutSeconds"] = "1"
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

            services.RemoveAll<IVerificationEmailSender>();
            services.AddSingleton<IVerificationEmailSender>(EmailSender);

            services
                .AddControllers()
                .AddApplicationPart(typeof(TestOnlyProbeController).Assembly);
        });
    }
}
