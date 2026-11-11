using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UniHome.Application.Abstractions.RateLimiting;
using UniHome.Application.Identity.Abstractions;
using UniHome.Infrastructure;
using UniHome.Infrastructure.Redis;

namespace UniHome.UnitTests.RateLimiting;

public sealed class DistributedRateLimiterTests
{
    private sealed class ControllableClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task InstanciaUnica_RegistraCorrectamenteLosLimites()
    {
        // 1. Una instancia registra correctamente los límites
        var clock = new ControllableClock();
        var limiter = new InMemoryDistributedRateLimiter(clock);
        const string partitionKey = "test:user:ip1";
        const int limit = 3;
        var window = TimeSpan.FromMinutes(1);

        for (var i = 1; i <= limit; i++)
        {
            var result = await limiter.CheckAndIncrementAsync(partitionKey, limit, window);
            Assert.True(result.IsAllowed, $"Petición {i} debió ser permitida.");
            Assert.Equal(i, result.CurrentCount);
            Assert.False(result.IsServiceUnavailable);
        }

        // Intento 4 debe exceder el límite
        var exceededResult = await limiter.CheckAndIncrementAsync(partitionKey, limit, window);
        Assert.False(exceededResult.IsAllowed);
        Assert.Equal(4, exceededResult.CurrentCount);
        Assert.True(exceededResult.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public async Task DosInstancias_CompartenElMismoContadorGlobal()
    {
        // 2. Dos instancias comparten el mismo contador
        var sharedStore = new InMemoryDistributedRateLimiter();
        IDistributedRateLimiter replicaA = sharedStore;
        IDistributedRateLimiter replicaB = sharedStore;

        const string partitionKey = "test:shared:ip-hash";
        const int limit = 5;
        var window = TimeSpan.FromSeconds(60);

        // Réplica A atiende 3 peticiones
        var r1 = await replicaA.CheckAndIncrementAsync(partitionKey, limit, window);
        var r2 = await replicaA.CheckAndIncrementAsync(partitionKey, limit, window);
        var r3 = await replicaA.CheckAndIncrementAsync(partitionKey, limit, window);

        Assert.True(r1.IsAllowed);
        Assert.True(r2.IsAllowed);
        Assert.True(r3.IsAllowed);

        // Réplica B atiende las siguientes 2 peticiones
        var r4 = await replicaB.CheckAndIncrementAsync(partitionKey, limit, window);
        var r5 = await replicaB.CheckAndIncrementAsync(partitionKey, limit, window);

        Assert.True(r4.IsAllowed);
        Assert.Equal(4, r4.CurrentCount);
        Assert.True(r5.IsAllowed);
        Assert.Equal(5, r5.CurrentCount);

        // Petición 6 en Réplica B debe ser rechazada porque el límite global es 5
        var r6 = await replicaB.CheckAndIncrementAsync(partitionKey, limit, window);
        Assert.False(r6.IsAllowed, "Réplica B no debe permitir solicitudes por encima del límite global.");
        Assert.Equal(6, r6.CurrentCount);

        // Petición 7 en Réplica A también debe ser rechazada
        var r7 = await replicaA.CheckAndIncrementAsync(partitionKey, limit, window);
        Assert.False(r7.IsAllowed, "Réplica A tampoco debe permitir solicitudes excedidas.");
        Assert.Equal(7, r7.CurrentCount);
    }

    [Fact]
    public async Task OperacionesConcurrentes_NoPermitenSuperarElLimite()
    {
        // 3. Que las operaciones concurrentes no permiten superar el límite
        var limiter = new InMemoryDistributedRateLimiter();
        const string partitionKey = "test:concurrent:ip";
        const int limit = 10;
        const int totalAttempts = 50;
        var window = TimeSpan.FromMinutes(1);

        var tasks = Enumerable.Range(0, totalAttempts)
            .Select(_ => limiter.CheckAndIncrementAsync(partitionKey, limit, window))
            .ToList();

        var results = await Task.WhenAll(tasks);

        var allowedCount = results.Count(r => r.IsAllowed);
        var rejectedCount = results.Count(r => !r.IsAllowed);

        Assert.Equal(limit, allowedCount);
        Assert.Equal(totalAttempts - limit, rejectedCount);
    }

    [Fact]
    public async Task ContadoresExpiran_MedianteTTL()
    {
        // 4. Que los contadores expiran mediante TTL
        var clock = new ControllableClock();
        var limiter = new InMemoryDistributedRateLimiter(clock);
        const string partitionKey = "test:ttl:ip";
        const int limit = 2;
        var window = TimeSpan.FromSeconds(30);

        // Consumir cupo
        var r1 = await limiter.CheckAndIncrementAsync(partitionKey, limit, window);
        var r2 = await limiter.CheckAndIncrementAsync(partitionKey, limit, window);
        var r3 = await limiter.CheckAndIncrementAsync(partitionKey, limit, window);

        Assert.True(r1.IsAllowed);
        Assert.True(r2.IsAllowed);
        Assert.False(r3.IsAllowed);

        // Avanzar el reloj más allá de la ventana TTL
        clock.UtcNow = clock.UtcNow.AddSeconds(31);

        // La siguiente petición debe reiniciar el contador y ser permitida
        var r4 = await limiter.CheckAndIncrementAsync(partitionKey, limit, window);
        Assert.True(r4.IsAllowed, "Al expirar el TTL, el contador debe reiniciarse y permitir nuevas solicitudes.");
        Assert.Equal(1, r4.CurrentCount);
    }

    [Fact]
    public async Task UpstashClient_EjecutaLuaScriptYRetorna429ConRetryAfter()
    {
        // 5. Que se devuelve evaluación correcta y TTL cuando se excede el límite
        var options = Options.Create(new RedisOptions
        {
            Enabled = true,
            RestUrl = "https://mock-upstash.io",
            RestToken = "mock-secret-token"
        });

        var callCount = 0;
        var handler = new MockHttpMessageHandler(request =>
        {
            callCount++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("mock-secret-token", request.Headers.Authorization?.Parameter);

            // Simular respuesta Upstash con Lua result: [0 (rejected), 11 (count), 45 (ttl)]
            var responseBody = JsonSerializer.Serialize(new
            {
                result = new long[] { 0, 11, 45 }
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            };
        });

        using var httpClient = new HttpClient(handler);
        using var client = new UpstashRedisDistributedRateLimiter(
            options,
            NullLogger<UpstashRedisDistributedRateLimiter>.Instance,
            httpClient);

        var result = await client.CheckAndIncrementAsync("rl:auth-login:hash123", 10, TimeSpan.FromSeconds(60));

        Assert.False(result.IsAllowed);
        Assert.Equal(11, result.CurrentCount);
        Assert.Equal(TimeSpan.FromSeconds(45), result.RetryAfter);
        Assert.False(result.IsServiceUnavailable);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task UpstashClient_ManejoDeErroresSegunPolitica_FailClosed_Retorna503()
    {
        // 6. Que los errores de Redis se manejan según la política aprobada (FailClosed -> 503)
        var options = Options.Create(new RedisOptions
        {
            Enabled = true,
            RestUrl = "https://mock-upstash.io",
            RestToken = "mock-secret-token",
            FailureMode = RedisFailureMode.FailClosed
        });

        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError));

        using var httpClient = new HttpClient(handler);
        using var client = new UpstashRedisDistributedRateLimiter(
            options,
            NullLogger<UpstashRedisDistributedRateLimiter>.Instance,
            httpClient);

        var result = await client.CheckAndIncrementAsync("rl:auth-login:hash123", 10, TimeSpan.FromSeconds(60));

        Assert.False(result.IsAllowed);
        Assert.True(result.IsServiceUnavailable, "Bajo FailClosed, la caída de Redis debe marcarse como ServiceUnavailable (503).");
    }

    [Fact]
    public async Task UpstashClient_ManejoDeErroresSegunPolitica_FailOpen_PermiteSinContadoresLocales()
    {
        // 6b. Que los errores de Redis se manejan según la política aprobada (FailOpen -> Allowed sin contador local)
        var options = Options.Create(new RedisOptions
        {
            Enabled = true,
            RestUrl = "https://mock-upstash.io",
            RestToken = "mock-secret-token",
            FailureMode = RedisFailureMode.FailOpen
        });

        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadGateway));

        using var httpClient = new HttpClient(handler);
        using var client = new UpstashRedisDistributedRateLimiter(
            options,
            NullLogger<UpstashRedisDistributedRateLimiter>.Instance,
            httpClient);

        var result = await client.CheckAndIncrementAsync("rl:auth-login:hash123", 10, TimeSpan.FromSeconds(60));

        Assert.True(result.IsAllowed, "Bajo FailOpen, la solicitud debe permitirse para asegurar continuidad.");
        Assert.False(result.IsServiceUnavailable);
        Assert.Equal(0, result.CurrentCount);
    }

    [Fact]
    public void ClientIpHasher_HmacSha256_EsDeterminista()
    {
        const string ip = "192.168.1.50";
        const string salt = "sal-secreta-de-prueba-valida";

        var hash1 = ClientIpHasher.Hash(ip, salt);
        var hash2 = ClientIpHasher.Hash(ip, salt);

        Assert.NotEmpty(hash1);
        Assert.Equal(64, hash1.Length); // 64 hex = 256 bits
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ClientIpHasher_HmacSha256_DiferentesClavesProducenDiferentesHashes()
    {
        const string ip = "192.168.1.50";
        const string salt1 = "sal-secreta-de-prueba-primera";
        const string salt2 = "sal-secreta-de-prueba-segunda";

        var hash1 = ClientIpHasher.Hash(ip, salt1);
        var hash2 = ClientIpHasher.Hash(ip, salt2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ClientIpHasher_IpNulaOVacia_RetornaUnknown()
    {
        const string salt = "sal-secreta-de-prueba";
        Assert.Equal("unknown", ClientIpHasher.Hash(null, salt));
        Assert.Equal("unknown", ClientIpHasher.Hash("   ", salt));
    }

    [Fact]
    public void DependencyInjection_EnDevelopmentSinRedis_RegistraProveedorEnMemoria()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Enabled"] = "false"
            })
            .Build();

        services.AddInfrastructure(configuration, isDevelopment: true);
        var sp = services.BuildServiceProvider();

        var limiter = sp.GetRequiredService<IDistributedRateLimiter>();
        Assert.IsType<InMemoryDistributedRateLimiter>(limiter);
    }

    [Fact]
    public void DependencyInjection_EnProduccionSinRedis_FallaEnArranque()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Enabled"] = "false"
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration, isDevelopment: false));

        Assert.Contains("obligatorio fuera del entorno de Development", ex.Message);
    }

    [Fact]
    public void DependencyInjection_EnProduccionConRedisHabilitadoPeroSinCredenciales_FallaEnArranque()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Enabled"] = "true",
                ["Redis:ConnectionString"] = "",
                ["Redis:RestUrl"] = "",
                ["Redis:RestToken"] = ""
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration, isDevelopment: false));

        Assert.Contains("credenciales válidas", ex.Message);
    }
}
