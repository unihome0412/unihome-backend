using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using UniHome.Domain.Accounts;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Misma fábrica de <see cref="UniHomeApiFactory"/> pero con
/// <c>Api:EnableSwagger=true</c>, para probar Swagger UI y el documento
/// OpenAPI cuando están explícitamente activados. La cuenta administrativa
/// sembrada aquí solo existe en el proceso de pruebas.
/// </summary>
public sealed class SwaggerEnabledApiFactory : UniHomeApiFactory
{
    public Account AdminAccount { get; private set; } = null!;

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

        AdminAccount = Accounts
            .FindByIdAsync(TestAdminAccountId)
            .GetAwaiter()
            .GetResult() ?? TestAccountFactory.Create(
                "swagger.admin@unihome.test",
                id: TestAdminAccountId);

        if (Accounts.FindByIdAsync(TestAdminAccountId).GetAwaiter().GetResult() is null)
        {
            Accounts.AddAsync(AdminAccount).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Cliente con un JWT administrativo real del host de pruebas. No usa la
    /// cookie de documentación y no crea usuarios ni secretos productivos.
    /// </summary>
    public HttpClient CreateAdminClient(WebApplicationFactoryClientOptions? options = null)
    {
        var client = options is null ? CreateClient() : CreateClient(options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestTokenFactory.Create(TestAdminAccountId, Guid.NewGuid()));
        return client;
    }
}
