using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniHome.Infrastructure.Configuration;

namespace UniHome.IntegrationTests;

/// <summary>
/// Fábrica sin dobles de persistencia: mantiene MongoDB vacío para verificar que
/// el registro responde 503 cuando el almacenamiento no está configurado.
/// </summary>
public sealed class UnconfiguredIdentityApiFactory : WebApplicationFactory<Program>
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
        });
    }
}
