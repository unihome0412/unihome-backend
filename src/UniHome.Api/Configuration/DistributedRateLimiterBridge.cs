using System.Threading.RateLimiting;
using UniHome.Application.Abstractions.RateLimiting;

namespace UniHome.Api.Configuration;

/// <summary>
/// Lease especializado que transporta metadatos del rate limiting distribuido,
/// permitiendo a OnRejected distinguir de forma inequívoca entre HTTP 429 y HTTP 503.
/// </summary>
public sealed class DistributedRateLimitLease : RateLimitLease
{
    public const string IsServiceUnavailableMetadataName = "IsServiceUnavailable";

    private readonly bool _isAcquired;
    private readonly TimeSpan _retryAfter;
    private readonly bool _isServiceUnavailable;

    public DistributedRateLimitLease(bool isAcquired, TimeSpan retryAfter, bool isServiceUnavailable = false)
    {
        _isAcquired = isAcquired;
        _retryAfter = retryAfter;
        _isServiceUnavailable = isServiceUnavailable;
    }

    public override bool IsAcquired => _isAcquired;

    public override IEnumerable<string> MetadataNames
    {
        get
        {
            if (!_isAcquired && _retryAfter > TimeSpan.Zero)
            {
                yield return MetadataName.RetryAfter.Name;
            }

            if (_isServiceUnavailable)
            {
                yield return IsServiceUnavailableMetadataName;
            }
        }
    }

    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        if (metadataName == MetadataName.RetryAfter.Name && !_isAcquired && _retryAfter > TimeSpan.Zero)
        {
            metadata = _retryAfter;
            return true;
        }

        if (metadataName == IsServiceUnavailableMetadataName && _isServiceUnavailable)
        {
            metadata = true;
            return true;
        }

        metadata = null;
        return false;
    }
}

/// <summary>
/// Puente entre el subsistema de System.Threading.RateLimiting de ASP.NET Core
/// y el IDistributedRateLimiter (Upstash Redis).
/// </summary>
public sealed class DistributedRateLimiterBridge : RateLimiter
{
    private readonly IDistributedRateLimiter _distributedLimiter;
    private readonly string _partitionKey;
    private readonly int _permitLimit;
    private readonly TimeSpan _window;

    public DistributedRateLimiterBridge(
        IDistributedRateLimiter distributedLimiter,
        string partitionKey,
        int permitLimit,
        TimeSpan window)
    {
        _distributedLimiter = distributedLimiter ?? throw new ArgumentNullException(nameof(distributedLimiter));
        _partitionKey = partitionKey ?? throw new ArgumentNullException(nameof(partitionKey));
        _permitLimit = permitLimit;
        _window = window;
    }

    public override TimeSpan? IdleDuration => null;

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        return AcquireAsyncCore(permitCount, CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        var result = await _distributedLimiter.CheckAndIncrementAsync(
            _partitionKey,
            _permitLimit,
            _window,
            cancellationToken).ConfigureAwait(false);

        if (result.IsServiceUnavailable)
        {
            return new DistributedRateLimitLease(
                isAcquired: false,
                retryAfter: TimeSpan.Zero,
                isServiceUnavailable: true);
        }

        if (result.IsAllowed)
        {
            return new DistributedRateLimitLease(
                isAcquired: true,
                retryAfter: TimeSpan.Zero,
                isServiceUnavailable: false);
        }

        return new DistributedRateLimitLease(
            isAcquired: false,
            retryAfter: result.RetryAfter,
            isServiceUnavailable: false);
    }
}
