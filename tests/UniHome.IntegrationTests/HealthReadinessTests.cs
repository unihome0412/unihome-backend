using System.Net;
using System.Text.Json;
using UniHome.Application.Abstractions.RateLimiting;

namespace UniHome.IntegrationTests;

/// <summary>
/// Readiness debe reflejar MongoDB y Redis/rate limiting distribuido.
/// /health/live permanece independiente de servicios externos.
/// </summary>
public sealed class HealthReadinessTests : IClassFixture<HealthReadyApiFactory>
{
    private readonly HealthReadyApiFactory _factory;
    private readonly HttpClient _client;

    public HealthReadinessTests(HealthReadyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Ready_ConMongoYRedisDisponibles_Devuelve200EIncluyeAmbasComprobaciones()
    {
        _factory.RedisProbe.Next = RateLimiterReadiness.Ready();

        using var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        var checks = ParseChecks(root);
        Assert.Equal("Healthy", checks["storage"].GetProperty("status").GetString());
        Assert.Equal("Healthy", checks["redis"].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ready_ConRedisNoDisponible_Devuelve503SinFiltrarCredenciales()
    {
        _factory.RedisProbe.Next = RateLimiterReadiness.Unavailable(
            "El control de tasa distribuido no está disponible.");

        using var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal("Unhealthy", root.GetProperty("status").GetString());
        var redis = ParseChecks(root)["redis"];
        Assert.Equal("Unhealthy", redis.GetProperty("status").GetString());
        Assert.Contains(
            "control de tasa",
            redis.GetProperty("description").GetString(),
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("upstash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mock-upstash", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ready_TrasRecuperacionDeRedis_VuelveA200()
    {
        _factory.RedisProbe.Next = RateLimiterReadiness.Unavailable(
            "Redis no respondió dentro del tiempo límite.");

        using var unavailable = await _client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);

        _factory.RedisProbe.Next = RateLimiterReadiness.Ready();

        using var recovered = await _client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);

        using var document = JsonDocument.Parse(await recovered.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Live_NoEvaluaRedisNiMongo()
    {
        _factory.RedisProbe.Next = RateLimiterReadiness.Unavailable(
            "Redis caído para probar liveness.");

        using var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var checks = ParseChecks(document.RootElement);

        Assert.True(checks.ContainsKey("self"));
        Assert.DoesNotContain("redis", checks.Keys);
        Assert.DoesNotContain("storage", checks.Keys);
    }

    private static Dictionary<string, JsonElement> ParseChecks(JsonElement root) =>
        root.GetProperty("checks")
            .EnumerateArray()
            .ToDictionary(
                entry => entry.GetProperty("name").GetString()!,
                entry => entry,
                StringComparer.Ordinal);
}
