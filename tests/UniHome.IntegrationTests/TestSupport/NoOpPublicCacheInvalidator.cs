using UniHome.Application.Abstractions.Caching;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>Usado en pruebas de persistencia que construyen el servicio
/// directamente contra Mongo real, sin levantar el host completo.</summary>
internal sealed class NoOpPublicCacheInvalidator : IPublicCacheInvalidator
{
    public Task EvictAsync(string tag, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
