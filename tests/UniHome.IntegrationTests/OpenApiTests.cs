using System.Net;
using System.Text.Json;

namespace UniHome.IntegrationTests;

public sealed class OpenApiTests : IClassFixture<UniHomeApiFactory>
{
    private readonly HttpClient _client;

    public OpenApiTests(UniHomeApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApi_DebeEstarDisponibleEnDesarrollo()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("openapi", out _));
        Assert.True(root.TryGetProperty("paths", out _));
    }

    [Fact]
    public async Task OpenApi_DebeIncluirElRegistroDeM01()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/auth/registro", out var registro));
        Assert.True(registro.TryGetProperty("post", out _));
    }
}
