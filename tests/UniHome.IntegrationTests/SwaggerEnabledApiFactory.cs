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
public class SwaggerEnabledApiFactory : UniHomeApiFactory
{
    public Account AdminAccount { get; private set; } = null!;

    public Account DocumentationReaderAccount { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Api:EnableSwagger"] = "true",
                ["DocumentationAccess:AccountIds:0"] = TestDocumentationReaderAccountId.ToString("N")
            });
        });

        AdminAccount = EnsureAccount(
            TestAdminAccountId,
            "swagger.admin@unihome.test");
        DocumentationReaderAccount = EnsureAccount(
            TestDocumentationReaderAccountId,
            "swagger.reader@unihome.test");
    }

    private Account EnsureAccount(Guid id, string email)
    {
        var existing = Accounts.FindByIdAsync(id).GetAwaiter().GetResult();

        if (existing is not null)
        {
            return existing;
        }

        var account = TestAccountFactory.Create(email, id: id);
        Accounts.AddAsync(account).GetAwaiter().GetResult();
        return account;
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
