using System.Net;
using System.Text;
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
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains(
            "default-src 'none'",
            response.Headers.GetValues("Content-Security-Policy").Single(),
            StringComparison.Ordinal);

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

    [Fact]
    public async Task Auth_DebeIncluirNoStoreYSinCookies()
    {
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync("/api/v1/auth/login", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "no-store",
            response.Headers.GetValues("Cache-Control").Single(),
            StringComparison.OrdinalIgnoreCase);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    private static Dictionary<string, JsonElement> ParseChecks(JsonElement root) =>
        root.GetProperty("checks")
            .EnumerateArray()
            .ToDictionary(
                entry => entry.GetProperty("name").GetString()!,
                entry => entry,
                StringComparer.Ordinal);
}
