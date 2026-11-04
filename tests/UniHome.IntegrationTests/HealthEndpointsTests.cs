using System.Net;
using System.Text.Json;

namespace UniHome.IntegrationTests;

public sealed class HealthEndpointsTests : IClassFixture<UniHomeApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointsTests(UniHomeApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Live_DebeResponder200Sano_SinMongoConfigurado()
    {
        using var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));

        var checks = ParseChecks(root);
        Assert.True(checks.ContainsKey("self"));
        Assert.DoesNotContain("storage", checks.Keys);
    }

    [Fact]
    public async Task Ready_DebeResponder503YNoDarFalsoOk_SinMongoConfigurado()
    {
        using var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal("Unhealthy", root.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));

        var checks = ParseChecks(root);
        Assert.True(checks.TryGetValue("storage", out var storage));
        Assert.Equal("Unhealthy", storage.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(storage.GetProperty("description").GetString()));

        Assert.DoesNotContain("mongodb://", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mongodb+srv://", body, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, JsonElement> ParseChecks(JsonElement root) =>
        root.GetProperty("checks")
            .EnumerateArray()
            .ToDictionary(
                entry => entry.GetProperty("name").GetString()!,
                entry => entry,
                StringComparer.Ordinal);
}
