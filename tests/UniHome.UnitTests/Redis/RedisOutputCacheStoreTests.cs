using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UniHome.Infrastructure;
using UniHome.Infrastructure.Redis;

namespace UniHome.UnitTests.Redis;

public sealed class RedisOutputCacheStoreTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }

    private static IOptions<RedisOptions> BuildOptions() => Options.Create(new RedisOptions
    {
        Enabled = true,
        RestUrl = "https://mock-upstash.io",
        RestToken = "mock-token"
    });

    [Fact]
    public async Task SetAsync_LuegoGetAsync_DevuelveElMismoContenido()
    {
        var store_backend = new Dictionary<string, string>(StringComparer.Ordinal);
        var handler = new MockHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var payload = JsonSerializer.Deserialize<string[]>(body)!;

            switch (payload[0])
            {
                case "SET":
                    store_backend[payload[1]] = payload[2];
                    return Ok(new { result = "OK" });
                case "GET":
                    return store_backend.TryGetValue(payload[1], out var value)
                        ? Ok(new { result = value })
                        : Ok(new { result = (string?)null });
                default:
                    throw new InvalidOperationException($"Comando no soportado: {payload[0]}");
            }
        });

        using var httpClient = new HttpClient(handler);
        using var store = new RedisOutputCacheStore(
            BuildOptions(), NullLogger<RedisOutputCacheStore>.Instance, httpClient);

        var payloadBytes = Encoding.UTF8.GetBytes("contenido-de-prueba");
        await store.SetAsync("clave-1", payloadBytes, tags: null, TimeSpan.FromSeconds(30), CancellationToken.None);

        var retrieved = await store.GetAsync("clave-1", CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal("contenido-de-prueba", Encoding.UTF8.GetString(retrieved!));
    }

    [Fact]
    public async Task GetAsync_ClaveInexistente_DevuelveNull()
    {
        var handler = new MockHttpMessageHandler(_ => Ok(new { result = (string?)null }));
        using var httpClient = new HttpClient(handler);
        using var store = new RedisOutputCacheStore(
            BuildOptions(), NullLogger<RedisOutputCacheStore>.Instance, httpClient);

        var result = await store.GetAsync("no-existe", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task EvictByTagAsync_BorraCadaEntradaDelTagYElPropioIndiceDelTag()
    {
        var deletedKeys = new List<string>();
        var handler = new MockHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var payload = JsonSerializer.Deserialize<string[]>(body)!;

            switch (payload[0])
            {
                case "SMEMBERS":
                    return Ok(new { result = new[] { "entrada-a", "entrada-b" } });
                case "DEL":
                    deletedKeys.Add(payload[1]);
                    return Ok(new { result = 1 });
                default:
                    throw new InvalidOperationException($"Comando no soportado: {payload[0]}");
            }
        });

        using var httpClient = new HttpClient(handler);
        using var store = new RedisOutputCacheStore(
            BuildOptions(), NullLogger<RedisOutputCacheStore>.Instance, httpClient);

        await store.EvictByTagAsync("mi-tag", CancellationToken.None);

        Assert.Contains("outputcache:entry:entrada-a", deletedKeys);
        Assert.Contains("outputcache:entry:entrada-b", deletedKeys);
        Assert.Contains("outputcache:tag:mi-tag", deletedKeys);
    }

    [Fact]
    public async Task SetAsync_ConTags_RegistraLaEntradaEnElIndiceDeCadaTag()
    {
        var sadds = new List<(string Key, string Member)>();
        var handler = new MockHttpMessageHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var payload = JsonSerializer.Deserialize<string[]>(body)!;

            switch (payload[0])
            {
                case "SET":
                    return Ok(new { result = "OK" });
                case "SADD":
                    sadds.Add((payload[1], payload[2]));
                    return Ok(new { result = 1 });
                case "EXPIRE":
                    return Ok(new { result = 1 });
                default:
                    throw new InvalidOperationException($"Comando no soportado: {payload[0]}");
            }
        });

        using var httpClient = new HttpClient(handler);
        using var store = new RedisOutputCacheStore(
            BuildOptions(), NullLogger<RedisOutputCacheStore>.Instance, httpClient);

        await store.SetAsync(
            "clave-2",
            Encoding.UTF8.GetBytes("x"),
            tags: ["tag-a", "tag-b"],
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.Contains(("outputcache:tag:tag-a", "clave-2"), sadds);
        Assert.Contains(("outputcache:tag:tag-b", "clave-2"), sadds);
    }

    [Fact]
    public async Task GetAsync_ValorCorruptoNoBase64_DevuelveNullSinLanzar()
    {
        var handler = new MockHttpMessageHandler(_ => Ok(new { result = "esto no es base64 válido ¡!" }));
        using var httpClient = new HttpClient(handler);
        using var store = new RedisOutputCacheStore(
            BuildOptions(), NullLogger<RedisOutputCacheStore>.Instance, httpClient);

        var result = await store.GetAsync("clave-corrupta", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void DependencyInjection_ConRedisHabilitado_RegistraRedisOutputCacheStore()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Enabled"] = "true",
                ["Redis:RestUrl"] = "https://mock-upstash.io",
                ["Redis:RestToken"] = "mock-token"
            })
            .Build();

        services.AddLogging();
        services.AddInfrastructure(configuration, isDevelopment: true);
        var provider = services.BuildServiceProvider();

        Assert.IsType<RedisOutputCacheStore>(provider.GetRequiredService<IOutputCacheStore>());
    }

    [Fact]
    public void DependencyInjection_SinRedis_NoRegistraUnAlmacenPropio()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Redis:Enabled"] = "false" })
            .Build();

        services.AddInfrastructure(configuration, isDevelopment: true);

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IOutputCacheStore));
    }

    private static HttpResponseMessage Ok(object body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body)) };
}
