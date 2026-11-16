using UniHome.Application.Abstractions.RateLimiting;
using UniHome.Application.Abstractions.Storage;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class ControllableRateLimiterReadinessProbe : IRateLimiterReadinessProbe
{
    public RateLimiterReadiness Next { get; set; } = RateLimiterReadiness.Ready();

    public Task<RateLimiterReadiness> CheckReadinessAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Next);
}

internal sealed class FixedStorageReadinessProbe : IStorageReadinessProbe
{
    private readonly StorageReadiness _readiness;

    public FixedStorageReadinessProbe(StorageReadiness readiness)
    {
        _readiness = readiness;
    }

    public Task<StorageReadiness> CheckAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_readiness);
}
