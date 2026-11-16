using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace UniHome.IntegrationTests;

/// <summary>
/// Verifica que M04/M07 respondan 503 fail-closed cuando MongoDB no está
/// configurado. No se usan mocks de persistencia para estas rutas.
/// </summary>
public sealed class ExplorationWithoutStorageTests
    : IClassFixture<UnconfiguredExplorationApiFactory>
{
    private readonly UnconfiguredExplorationApiFactory _factory;

    public ExplorationWithoutStorageTests(UnconfiguredExplorationApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Mapa_SinMongoConfigurado_DebeResponder503()
    {
        using var client = _factory.CreateClient();

        var universityId = Guid.NewGuid();
        using var map = await client.GetAsync(
            $"/api/v1/exploracion/mapa?universidadId={universityId}");
        using var list = await client.GetAsync(
            $"/api/v1/exploracion/lista?universidadId={universityId}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, map.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, list.StatusCode);
        await AssertProblemAsync(map, expectedStatus: 503);
        await AssertProblemAsync(list, expectedStatus: 503);
    }

    [Fact]
    public async Task Tarjeta_SinMongoConfigurado_DebeResponder503()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/unidades/{Guid.NewGuid()}/tarjeta");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertProblemAsync(response, expectedStatus: 503);
    }

    [Fact]
    public async Task Favoritos_SinMongoConfigurado_DebenResponder503()
    {
        var account = await AuthTestHelpers.SeedAccountAsync(
            _factory,
            AuthTestHelpers.UniqueEmail());
        using var client = _factory.CreateClient();
        using var login = await AuthTestHelpers.LoginAsync(client, account.Email.Value);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        using var list = await client.GetAsync("/api/v1/favoritos");
        using var add = await client.PutAsync(
            $"/api/v1/favoritos/{Guid.NewGuid()}",
            content: null);
        using var detail = await client.GetAsync(
            $"/api/v1/unidades/{Guid.NewGuid()}/ficha");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, list.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, add.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, detail.StatusCode);
        await AssertProblemAsync(list, expectedStatus: 503);
        await AssertProblemAsync(add, expectedStatus: 503);
        await AssertProblemAsync(detail, expectedStatus: 503);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        int expectedStatus)
    {
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(expectedStatus, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("mongodb://", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mongodb+srv://", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }
}
