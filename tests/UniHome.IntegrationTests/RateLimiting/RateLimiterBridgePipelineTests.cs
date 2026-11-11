using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using UniHome.Api.Configuration;
using UniHome.Application.Abstractions.RateLimiting;
using UniHome.Infrastructure.Redis;

namespace UniHome.IntegrationTests.RateLimiting;

public sealed class RateLimiterBridgePipelineTests
{
    private sealed class StubDistributedRateLimiter : IDistributedRateLimiter
    {
        public RateLimitResult NextResult { get; set; } = RateLimitResult.Allowed(1, TimeSpan.FromSeconds(60));

        public Task<RateLimitResult> CheckAndIncrementAsync(
            string partitionKey,
            int permitLimit,
            TimeSpan window,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(NextResult);
        }
    }

    [Fact]
    public async Task Bridge_CuandoExcedeLimite_RetornaLeaseRechazadoConRetryAfter()
    {
        var stub = new StubDistributedRateLimiter
        {
            NextResult = RateLimitResult.Exceeded(11, TimeSpan.FromSeconds(45))
        };

        var bridge = new DistributedRateLimiterBridge(stub, "partition-key", 10, TimeSpan.FromSeconds(60));

        using var lease = await bridge.AcquireAsync(1);

        Assert.False(lease.IsAcquired);
        Assert.True(lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter));
        Assert.Equal(TimeSpan.FromSeconds(45), retryAfter);

        Assert.False(lease.TryGetMetadata(DistributedRateLimitLease.IsServiceUnavailableMetadataName, out _));
    }

    [Fact]
    public async Task Bridge_CuandoFallaRedisEnFailClosed_RetornaLeaseConMetadataServiceUnavailable()
    {
        var stub = new StubDistributedRateLimiter
        {
            NextResult = RateLimitResult.ServiceUnavailable("Redis unreachable")
        };

        var bridge = new DistributedRateLimiterBridge(stub, "partition-key", 10, TimeSpan.FromSeconds(60));

        using var lease = await bridge.AcquireAsync(1);

        Assert.False(lease.IsAcquired);
        Assert.True(lease.TryGetMetadata(DistributedRateLimitLease.IsServiceUnavailableMetadataName, out var isUnavailable));
        Assert.Equal(true, isUnavailable);
    }

    [Fact]
    public void Bridge_IdleDuration_EsNoNuloYPermiteDesalojoDeParticionesInactivas()
    {
        var stub = new StubDistributedRateLimiter();
        var bridge = new DistributedRateLimiterBridge(stub, "partition-key", 10, TimeSpan.FromSeconds(60));

        Assert.NotNull(bridge.IdleDuration);
        Assert.True(bridge.IdleDuration.Value >= TimeSpan.FromMinutes(2),
            "IdleDuration debe ser de al menos 2 minutos para permitir recolección de memoria sin churn prematuro.");
    }

    [Fact]
    public void Bridge_AttemptAcquireSincronoDirecto_RetornaLeaseNoAdquiridoSinLanzarExcepcion()
    {
        var stub = new StubDistributedRateLimiter();
        var bridge = new DistributedRateLimiterBridge(stub, "partition-key", 10, TimeSpan.FromSeconds(60));

        using var lease = bridge.AttemptAcquire(1);

        Assert.NotNull(lease);
        Assert.False(lease.IsAcquired, "La ruta sincrónica preliminar no debe conceder permisos para forzar la ruta asíncrona por red.");
        Assert.False(lease.TryGetMetadata(DistributedRateLimitLease.IsServiceUnavailableMetadataName, out _));
    }

    [Fact]
    public async Task DesalojoDeParticionLocal_NoAfectaElContadorCompartidoEnRedis()
    {
        // Simula que la instancia local de DistributedRateLimiterBridge es desalojada por inactividad
        // y luego una nueva instancia es creada por la fábrica de particiones para la misma clave.
        var sharedStore = new InMemoryDistributedRateLimiter();
        const string key = "unihome:rl:auth-login:hash-ip";

        // Primera instancia del bridge ejecuta 2 peticiones
        var bridge1 = new DistributedRateLimiterBridge(sharedStore, key, 5, TimeSpan.FromSeconds(60));
        var l1 = await bridge1.AcquireAsync(1);
        var l2 = await bridge1.AcquireAsync(1);
        Assert.True(l1.IsAcquired);
        Assert.True(l2.IsAcquired);

        // Simulamos desalojo / recolección de bridge1. Se crea bridge2 para la misma clave de partición:
        var bridge2 = new DistributedRateLimiterBridge(sharedStore, key, 5, TimeSpan.FromSeconds(60));
        var l3 = await bridge2.AcquireAsync(1);
        Assert.True(l3.IsAcquired);

        // La 3ra petición debe continuar la cuenta (alcanzar 3, 4, 5, y la 6ta debe fallar)
        var l4 = await bridge2.AcquireAsync(1);
        var l5 = await bridge2.AcquireAsync(1);
        var l6 = await bridge2.AcquireAsync(1);

        Assert.True(l4.IsAcquired);
        Assert.True(l5.IsAcquired);
        Assert.False(l6.IsAcquired, "El contador compartido debe conservarse intacto tras el desalojo de la instancia local.");
    }

    [Fact]
    public void ProxyOptions_ConfiguraKnownProxiesYNetworksCorrectamente()
    {
        var proxyOptions = new ProxyOptions
        {
            TrustForwardedHeaders = true,
            KnownProxies = ["10.0.0.5", "192.168.1.1"],
            KnownNetworks = ["10.0.0.0/16", "172.16.0.0/12"]
        };

        var forwardedOptions = new ForwardedHeadersOptions();
        forwardedOptions.KnownProxies.Clear();
        forwardedOptions.KnownIPNetworks.Clear();

        foreach (var proxy in proxyOptions.KnownProxies)
        {
            if (IPAddress.TryParse(proxy, out var ip))
            {
                forwardedOptions.KnownProxies.Add(ip);
            }
        }

        foreach (var network in proxyOptions.KnownNetworks)
        {
            var parts = network.Split('/');
            if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var prefix) && int.TryParse(parts[1], out var prefixLength))
            {
                forwardedOptions.KnownIPNetworks.Add(new System.Net.IPNetwork(prefix, prefixLength));
            }
        }

        Assert.Equal(2, forwardedOptions.KnownProxies.Count);
        Assert.Contains(IPAddress.Parse("10.0.0.5"), forwardedOptions.KnownProxies);
        Assert.Contains(IPAddress.Parse("192.168.1.1"), forwardedOptions.KnownProxies);

        Assert.Equal(2, forwardedOptions.KnownIPNetworks.Count);
    }

    [Fact]
    public void DistributedRateLimitLease_MetadataNames_EnumeraCorrectamenteSegunEstado()
    {
        // 1. Lease rechazado por exceder límite con RetryAfter
        var leaseExceeded = new DistributedRateLimitLease(
            isAcquired: false,
            retryAfter: TimeSpan.FromSeconds(45),
            isServiceUnavailable: false);

        var metaExceeded = leaseExceeded.MetadataNames.ToList();
        Assert.Contains(MetadataName.RetryAfter.Name, metaExceeded);
        Assert.DoesNotContain(DistributedRateLimitLease.IsServiceUnavailableMetadataName, metaExceeded);

        // 2. Lease rechazado por indisponibilidad de Redis (FailClosed -> 503)
        var leaseUnavailable = new DistributedRateLimitLease(
            isAcquired: false,
            retryAfter: TimeSpan.Zero,
            isServiceUnavailable: true);

        var metaUnavailable = leaseUnavailable.MetadataNames.ToList();
        Assert.Contains(DistributedRateLimitLease.IsServiceUnavailableMetadataName, metaUnavailable);
        Assert.DoesNotContain(MetadataName.RetryAfter.Name, metaUnavailable);

        // 3. Lease adquirido exitosamente
        var leaseAcquired = new DistributedRateLimitLease(
            isAcquired: true,
            retryAfter: TimeSpan.Zero,
            isServiceUnavailable: false);

        Assert.Empty(leaseAcquired.MetadataNames);
    }

    [Fact]
    public void DistributedRateLimiterBridge_GetStatistics_RetornaNull()
    {
        var stub = new StubDistributedRateLimiter();
        var bridge = new DistributedRateLimiterBridge(stub, "key", 5, TimeSpan.FromSeconds(60));
        Assert.Null(bridge.GetStatistics());
    }
}
