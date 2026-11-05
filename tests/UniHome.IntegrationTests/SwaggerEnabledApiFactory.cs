using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace UniHome.IntegrationTests;

/// <summary>
/// Misma fábrica de <see cref="UniHomeApiFactory"/> pero con
/// <c>Api:EnableSwagger=true</c>, para probar Swagger UI y el documento
/// OpenAPI cuando están explícitamente activados.
/// </summary>
public sealed class SwaggerEnabledApiFactory : UniHomeApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Api:EnableSwagger"] = "true"
            });
        });
    }
}
