using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UniHome.Application.Identity.Abstractions;
using UniHome.Infrastructure;
using UniHome.Infrastructure.Redis;

namespace UniHome.UnitTests.Redis;

public sealed class SessionDenylistTests
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

    [Fact]
    public async Task InMemory_DenyAsync_LuegoIsDeniedAsync_DevuelveTrue()
    {
        var denylist = new InMemorySessionDenylist();
        var sessionId = Guid.NewGuid();

        Assert.False(await denylist.IsDeniedAsync(sessionId));

        await denylist.DenyAsync(sessionId, TimeSpan.FromMinutes(5));

        Assert.True(await denylist.IsDeniedAsync(sessionId));
    }

    [Fact]
    public async Task InMemory_TtlVencido_DejaDeEstarDenegado()
    {
        var denylist = new InMemorySessionDenylist();
        var sessionId = Guid.NewGuid();

        await denylist.DenyAsync(sessionId, TimeSpan.FromMilliseconds(-1));

        Assert.False(await denylist.IsDeniedAsync(sessionId));
    }

    [Fact]
    public async Task InMemory_GuidVacio_NoHaceNadaYNuncaEstaDenegado()
    {
        var denylist = new InMemorySessionDenylist();

        await denylist.DenyAsync(Guid.Empty, TimeSpan.FromMinutes(5));

        Assert.False(await denylist.IsDeniedAsync(Guid.Empty));
    }

    [Fact]
    public async Task Redis_DenyAsync_EnviaSetConExYClavePrefijada()
    {
        var sessionId = Guid.NewGuid();
        var options = Options.Create(new RedisOptions
        {
            Enabled = true,
            RestUrl = "https://mock-upstash.io",
            RestToken = "mock-token"
        });

        HttpRequestMessage? captured = null;
        string? body = null;
        var handler = new MockHttpMessageHandler(request =>
        {
            captured = request;
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { result = "OK" }))
            };
        });

        using var httpClient = new HttpClient(handler);
        using var denylist = new RedisSessionDenylist(
            options,
            NullLogger<RedisSessionDenylist>.Instance,
            httpClient);

        await denylist.DenyAsync(sessionId, TimeSpan.FromMinutes(60));

        Assert.NotNull(captured);
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("mock-token", captured.Headers.Authorization?.Parameter);

        var payload = JsonSerializer.Deserialize<string[]>(body!)!;
        Assert.Equal("SET", payload[0]);
        Assert.Equal($"denylist:sid:{sessionId:N}", payload[1]);
        Assert.Equal("1", payload[2]);
        Assert.Equal("EX", payload[3]);
        Assert.Equal("3600", payload[4]);
    }

    [Fact]
    public async Task Redis_IsDeniedAsync_ExistsUno_DevuelveTrue()
    {
        var sessionId = Guid.NewGuid();
        var options = Options.Create(new RedisOptions
        {
            Enabled = true,
            RestUrl = "https://mock-upstash.io",
            RestToken = "mock-token"
        });

        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { result = 1 }))
            });

        using var httpClient = new HttpClient(handler);
        using var denylist = new RedisSessionDenylist(
            options,
            NullLogger<RedisSessionDenylist>.Instance,
            httpClient);

        Assert.True(await denylist.IsDeniedAsync(sessionId));
    }

    [Fact]
    public async Task Redis_IsDeniedAsync_ExistsCero_DevuelveFalse()
    {
        var sessionId = Guid.NewGuid();
        var options = Options.Create(new RedisOptions
        {
            Enabled = true,
            RestUrl = "https://mock-upstash.io",
            RestToken = "mock-token"
        });

        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { result = 0 }))
            });

        using var httpClient = new HttpClient(handler);
        using var denylist = new RedisSessionDenylist(
            options,
            NullLogger<RedisSessionDenylist>.Instance,
            httpClient);

        Assert.False(await denylist.IsDeniedAsync(sessionId));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Redis_IsDeniedAsync_FallaDeRedis_FallaAbiertoDevuelveFalse(HttpStatusCode statusCode)
    {
        // A diferencia del rate limiting (FailClosed), esta comprobación corre en
        // el camino crítico de toda petición autenticada: una caída de Redis
        // nunca debe traducirse en bloquear a todos los usuarios con sesión
        // válida (ver RedisSessionDenylist, resumen de la clase).
        var sessionId = Guid.NewGuid();
        var options = Options.Create(new RedisOptions
        {
            Enabled = true,
            RestUrl = "https://mock-upstash.io",
            RestToken = "mock-token",
            MaxAttempts = 1
        });

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(statusCode));

        using var httpClient = new HttpClient(handler);
        using var denylist = new RedisSessionDenylist(
            options,
            NullLogger<RedisSessionDenylist>.Instance,
            httpClient);

        Assert.False(await denylist.IsDeniedAsync(sessionId));
    }

    [Fact]
    public async Task Redis_SinCredenciales_NoLanzaYFallaAbierto()
    {
        var sessionId = Guid.NewGuid();
        var options = Options.Create(new RedisOptions
        {
            Enabled = true,
            RestUrl = string.Empty,
            RestToken = string.Empty
        });

        using var denylist = new RedisSessionDenylist(
            options,
            NullLogger<RedisSessionDenylist>.Instance,
            new HttpClient());

        await denylist.DenyAsync(sessionId, TimeSpan.FromMinutes(60));
        Assert.False(await denylist.IsDeniedAsync(sessionId));
    }

    [Fact]
    public async Task Redis_GuidVacio_NoEnviaComandoYNuncaEstaDenegado()
    {
        var calls = 0;
        var options = Options.Create(new RedisOptions
        {
            Enabled = true,
            RestUrl = "https://mock-upstash.io",
            RestToken = "mock-token"
        });

        var handler = new MockHttpMessageHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { result = 1 }))
            };
        });

        using var httpClient = new HttpClient(handler);
        using var denylist = new RedisSessionDenylist(
            options,
            NullLogger<RedisSessionDenylist>.Instance,
            httpClient);

        await denylist.DenyAsync(Guid.Empty, TimeSpan.FromMinutes(60));
        var denied = await denylist.IsDeniedAsync(Guid.Empty);

        Assert.False(denied);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void DependencyInjection_EnDevelopmentSinRedis_RegistraDenylistEnMemoria()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Redis:Enabled"] = "false" })
            .Build();

        services.AddInfrastructure(configuration, isDevelopment: true);
        var provider = services.BuildServiceProvider();

        Assert.IsType<InMemorySessionDenylist>(provider.GetRequiredService<ISessionDenylist>());
    }

    [Fact]
    public void DependencyInjection_ConRedisHabilitado_RegistraDenylistDeRedis()
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

        Assert.IsType<RedisSessionDenylist>(provider.GetRequiredService<ISessionDenylist>());
    }
}
