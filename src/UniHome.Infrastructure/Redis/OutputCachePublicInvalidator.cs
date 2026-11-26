using Microsoft.AspNetCore.OutputCaching;
using UniHome.Application.Abstractions.Caching;

namespace UniHome.Infrastructure.Redis;

/// <summary>
/// Adaptador de <see cref="IPublicCacheInvalidator"/> sobre el
/// <see cref="IOutputCacheStore"/> que ASP.NET Core ya resuelve (Redis o en
/// memoria, según <c>Redis:Enabled</c>; ver <c>DependencyInjection</c>).
/// </summary>
internal sealed class OutputCachePublicInvalidator : IPublicCacheInvalidator
{
    private readonly IOutputCacheStore _store;

    public OutputCachePublicInvalidator(IOutputCacheStore store)
    {
        _store = store;
    }

    public Task EvictAsync(string tag, CancellationToken cancellationToken = default) =>
        _store.EvictByTagAsync(tag, cancellationToken).AsTask();
}
