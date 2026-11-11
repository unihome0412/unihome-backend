using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniHome.Application.Abstractions.RateLimiting;
using UniHome.Infrastructure.Redis;

namespace UniHome.IntegrationTests.RateLimiting;

public sealed class DistributedRedisIntegrationTests
{
    private sealed class FailingDistributedRateLimiter : IDistributedRateLimiter
    {
        private readonly bool _isUnavailable;

        public FailingDistributedRateLimiter(bool isUnavailable)
        {
            _isUnavailable = isUnavailable;
        }

        public Task<RateLimitResult> CheckAndIncrementAsync(
            string partitionKey,
            int permitLimit,
            TimeSpan window,
            CancellationToken cancellationToken = default)
        {
            return _isUnavailable
                ? Task.FromResult(RateLimitResult.ServiceUnavailable("Simulated Redis Outage"))
                : Task.FromResult(RateLimitResult.Allowed(0, TimeSpan.Zero));
        }
    }

    private sealed class CustomRateLimitApiFactory : UniHomeApiFactory
    {
        private readonly Action<IServiceCollection>? _configureServices;

        public CustomRateLimitApiFactory(Action<IServiceCollection>? configureServices = null)
        {
            _configureServices = configureServices;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:RateLimit:LoginPermitLimit"] = "3",
                    ["Auth:RateLimit:LoginWindowSeconds"] = "60"
                });
            });

            if (_configureServices is not null)
            {
                builder.ConfigureTestServices(_configureServices);
            }
        }
    }

    private sealed class ProductionMisconfiguredApiFactory : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> _envOverrides;

        public ProductionMisconfiguredApiFactory(Dictionary<string, string?> envOverrides)
        {
            _envOverrides = envOverrides;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Environment.SetEnvironmentVariable("VerificationCode__HmacSecret", "test-secret-long-enough-0123456789");
            Environment.SetEnvironmentVariable("Jwt__SigningKey", "test-signing-key-long-enough-0123456789-abcdef");

            foreach (var (k, v) in _envOverrides)
            {
                Environment.SetEnvironmentVariable(k.Replace(":", "__"), v);
            }

            builder.UseEnvironment("Production");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(_envOverrides);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            foreach (var k in _envOverrides.Keys)
            {
                Environment.SetEnvironmentVariable(k.Replace(":", "__"), null);
            }
        }
    }

    [Fact]
    public async Task MultiplesInstancias_CompartenContadorDistribuidoEnHttp()
    {
        // Simulamos un backend distribuido de Redis compartido entre dos réplicas API (Instancia 1 e Instancia 2)
        var sharedRedisStore = new InMemoryDistributedRateLimiter();

        using var factory1 = new CustomRateLimitApiFactory(services =>
        {
            services.AddSingleton<IDistributedRateLimiter>(sharedRedisStore);
        });

        using var factory2 = new CustomRateLimitApiFactory(services =>
        {
            services.AddSingleton<IDistributedRateLimiter>(sharedRedisStore);
        });

        using var clientInstance1 = factory1.CreateClient();
        using var clientInstance2 = factory2.CreateClient();

        // Instancia 1 recibe 2 peticiones (Límite configurado = 3)
        var r1 = await clientInstance1.PostAsJsonAsync("/api/v1/auth/login", new { correo = "test@share.com", contrasena = "Clave123!" });
        var r2 = await clientInstance1.PostAsJsonAsync("/api/v1/auth/login", new { correo = "test@share.com", contrasena = "Clave123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, r2.StatusCode);

        // Instancia 2 recibe la 3ra petición (Alcanza el límite global de 3)
        var r3 = await clientInstance2.PostAsJsonAsync("/api/v1/auth/login", new { correo = "test@share.com", contrasena = "Clave123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, r3.StatusCode);

        // Instancia 2 recibe la 4ta petición -> Debe ser rechazada con HTTP 429 gracias al contador compartido
        var r4 = await clientInstance2.PostAsJsonAsync("/api/v1/auth/login", new { correo = "test@share.com", contrasena = "Clave123!" });
        Assert.Equal(HttpStatusCode.TooManyRequests, r4.StatusCode);
        Assert.NotNull(r4.Headers.RetryAfter);

        // Instancia 1 recibe la 5ta petición -> También debe ser rechazada con HTTP 429
        var r5 = await clientInstance1.PostAsJsonAsync("/api/v1/auth/login", new { correo = "test@share.com", contrasena = "Clave123!" });
        Assert.Equal(HttpStatusCode.TooManyRequests, r5.StatusCode);
        Assert.NotNull(r5.Headers.RetryAfter);
    }

    private sealed class TrackingDistributedRateLimiter : IDistributedRateLimiter
    {
        public int InvocationCount { get; private set; }
        public List<string> InvokedKeys { get; } = new();

        public Task<RateLimitResult> CheckAndIncrementAsync(
            string partitionKey,
            int permitLimit,
            TimeSpan window,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            InvokedKeys.Add(partitionKey);
            return Task.FromResult(InvocationCount <= permitLimit
                ? RateLimitResult.Allowed(InvocationCount, TimeSpan.FromSeconds(60))
                : RateLimitResult.Exceeded(InvocationCount, TimeSpan.FromSeconds(45)));
        }
    }

    [Fact]
    public async Task PipelineHttp_AdquisicionAsincrona_InvocaProveedorDistribuidoYResponde429ConRetryAfter()
    {
        var trackingLimiter = new TrackingDistributedRateLimiter();

        using var factory = new CustomRateLimitApiFactory(services =>
        {
            services.AddSingleton<IDistributedRateLimiter>(trackingLimiter);
        });

        using var client = factory.CreateClient();

        // Peticiones dentro del límite (límite configurado = 3)
        var r1 = await client.PostAsJsonAsync("/api/v1/auth/login", new { correo = "u@test.com", contrasena = "Pass123!" });
        var r2 = await client.PostAsJsonAsync("/api/v1/auth/login", new { correo = "u@test.com", contrasena = "Pass123!" });
        var r3 = await client.PostAsJsonAsync("/api/v1/auth/login", new { correo = "u@test.com", contrasena = "Pass123!" });

        // Llegan al controlador (retorna 401 por credenciales incorrectas, no 429 ni 503)
        Assert.Equal(HttpStatusCode.Unauthorized, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, r2.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, r3.StatusCode);

        // Confirma que el proveedor distribuido fue consultado asíncronamente 3 veces
        Assert.Equal(3, trackingLimiter.InvocationCount);
        Assert.All(trackingLimiter.InvokedKeys, k => Assert.StartsWith("unihome:rl:auth-login:", k));

        // 4ta petición excede el límite
        var r4 = await client.PostAsJsonAsync("/api/v1/auth/login", new { correo = "u@test.com", contrasena = "Pass123!" });
        Assert.Equal(HttpStatusCode.TooManyRequests, r4.StatusCode);
        Assert.NotNull(r4.Headers.RetryAfter);
        Assert.Equal(4, trackingLimiter.InvocationCount);
    }

    [Fact]
    public async Task RedisFalla_EnModoFailClosed_ApiResponde503ServiceUnavailable()
    {
        using var factory = new CustomRateLimitApiFactory(services =>
        {
            services.AddSingleton<IDistributedRateLimiter>(new FailingDistributedRateLimiter(isUnavailable: true));
        });

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { correo = "test@failclosed.com", contrasena = "Clave123!" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(503, document.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("no disponible temporalmente", document.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RedisFalla_EnModoFailOpen_ApiPermitePeticionSin429()
    {
        using var factory = new CustomRateLimitApiFactory(services =>
        {
            services.AddSingleton<IDistributedRateLimiter>(new FailingDistributedRateLimiter(isUnavailable: false));
        });

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { correo = "test@failopen.com", contrasena = "Clave123!" });

        // Debe permitir pasar al controlador de auth (retorna 401 Unauthorized por clave falsa, NO 429 ni 503)
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void ProduccionConRedisDeshabilitado_FallaEnArranque()
    {
        try
        {
            using var factory = new ProductionMisconfiguredApiFactory(new Dictionary<string, string?>
            {
                ["Auth:RateLimit:IpHashSalt"] = "test-salt-long-enough-0123456789",
                ["Redis:Enabled"] = "false"
            });

            var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
            Assert.Contains("obligatorio fuera del entorno de Development", ex.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("Redis__Enabled", null);
            Environment.SetEnvironmentVariable("Auth__RateLimit__IpHashSalt", null);
        }
    }

    [Fact]
    public void ProduccionConTrustForwardedHeadersSinProxies_FallaEnArranque()
    {
        try
        {
            using var factory = new ProductionMisconfiguredApiFactory(new Dictionary<string, string?>
            {
                ["Auth:RateLimit:IpHashSalt"] = "test-salt-long-enough-0123456789",
                ["Redis:Enabled"] = "true",
                ["Redis:RestUrl"] = "https://mock.upstash.io",
                ["Redis:RestToken"] = "token",
                ["Proxy:TrustForwardedHeaders"] = "true"
            });

            var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
            Assert.Contains("Proxy:TrustForwardedHeaders está activo pero no se especificó ninguna dirección", ex.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("Proxy__TrustForwardedHeaders", null);
            Environment.SetEnvironmentVariable("Redis__Enabled", null);
            Environment.SetEnvironmentVariable("Redis__RestUrl", null);
            Environment.SetEnvironmentVariable("Redis__RestToken", null);
            Environment.SetEnvironmentVariable("Auth__RateLimit__IpHashSalt", null);
        }
    }

    [Fact]
    public void ProduccionSinIpHashSalt_FallaEnArranque()
    {
        try
        {
            using var factory = new ProductionMisconfiguredApiFactory(new Dictionary<string, string?>
            {
                ["Auth:RateLimit:IpHashSalt"] = "",
                ["Redis:Enabled"] = "true",
                ["Redis:RestUrl"] = "https://mock.upstash.io",
                ["Redis:RestToken"] = "token"
            });

            var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
            Assert.Contains("Auth:RateLimit:IpHashSalt", ex.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("Auth__RateLimit__IpHashSalt", null);
            Environment.SetEnvironmentVariable("Redis__Enabled", null);
            Environment.SetEnvironmentVariable("Redis__RestUrl", null);
            Environment.SetEnvironmentVariable("Redis__RestToken", null);
        }
    }
}
