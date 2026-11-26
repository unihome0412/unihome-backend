using UniHome.Application.Abstractions.Caching;

namespace UniHome.UnitTests.TestSupport;

/// <summary>Registra las invalidaciones pedidas, sin tocar ningún caché real.</summary>
internal sealed class FakePublicCacheInvalidator : IPublicCacheInvalidator
{
    public List<string> EvictedTags { get; } = [];

    public Task EvictAsync(string tag, CancellationToken cancellationToken = default)
    {
        EvictedTags.Add(tag);
        return Task.CompletedTask;
    }
}
