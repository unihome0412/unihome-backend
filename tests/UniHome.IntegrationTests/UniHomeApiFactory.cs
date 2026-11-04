using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniHome.Infrastructure.Configuration;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Fábrica de pruebas: fuerza MongoDB sin configurar para que readiness no dependa
/// de un cluster real y agrega controladores exclusivos de prueba (validación/error).
/// </summary>
public sealed class UniHomeApiFactory : WebApplicationFactory<Program>
{
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

            services
                .AddControllers()
                .AddApplicationPart(typeof(TestOnlyProbeController).Assembly);
        });
    }
}
