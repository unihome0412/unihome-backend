using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using UniHome.Api.Configuration;
using UniHome.Application.Abstractions.RateLimiting;

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
}
