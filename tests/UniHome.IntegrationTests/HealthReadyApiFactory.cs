using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniHome.Application.Abstractions.RateLimiting;
using UniHome.Application.Abstractions.Storage;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Aísla /health/ready de MongoDB y Redis reales para probar la traducción de
/// estados de dependencias críticas sin red ni credenciales.
/// </summary>
public sealed class HealthReadyApiFactory : UniHomeApiFactory
{
    public ControllableRateLimiterReadinessProbe RedisProbe { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IStorageReadinessProbe>();
            services.AddSingleton<IStorageReadinessProbe>(
                new FixedStorageReadinessProbe(StorageReadiness.Ready()));

            services.RemoveAll<IRateLimiterReadinessProbe>();
            services.AddSingleton<IRateLimiterReadinessProbe>(RedisProbe);
        });
    }
}
