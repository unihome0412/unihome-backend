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
/// Adaptador entre el subsistema de PartitionedRateLimiter de ASP.NET Core y el IDistributedRateLimiter.
/// </summary>
public sealed class DistributedRateLimiterBridge : RateLimiter
{
    private readonly IDistributedRateLimiter _distributedLimiter;
    private readonly string _partitionKey;
    private readonly int _permitLimit;
    private readonly TimeSpan _window;
    private readonly TimeSpan _idleDuration;

    public DistributedRateLimiterBridge(
        IDistributedRateLimiter distributedLimiter,
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        TimeSpan? idleDuration = null)
    {
        _distributedLimiter = distributedLimiter ?? throw new ArgumentNullException(nameof(distributedLimiter));
        _partitionKey = partitionKey ?? throw new ArgumentNullException(nameof(partitionKey));
        _permitLimit = permitLimit;
        _window = window;

        // Establece una duración de inactividad razonable (2 minutos por defecto o 2x la ventana)
        // para que el PartitionedRateLimiter de ASP.NET Core pueda desalojar instancias locales
        // de memoria asociadas a IPs inactivas, sin afectar los contadores que residen en Redis.
        _idleDuration = idleDuration ?? (window > TimeSpan.FromMinutes(1) ? window.Multiply(2) : TimeSpan.FromMinutes(2));
    }

    public override TimeSpan? IdleDuration => _idleDuration;

    public override RateLimiterStatistics? GetStatistics() => null;

    /// <summary>
    /// La adquisición sincrónica directa está prohibida para prevenir sync-over-async y agotamiento
    /// de hilos en Kestrel (arrojando NotSupportedException). Cuando es invocado internamente por el
    /// middleware de rate limiting de ASP.NET Core como fase de sondeo previo sincrónico (fast-path),
    /// retorna un lease no adquirido para dirigir el flujo a la ruta asíncrona AcquireAsyncCore sin bloquear el hilo.
    /// </summary>
    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        var stackTrace = Environment.StackTrace;
        if (stackTrace.Contains("Microsoft.AspNetCore.RateLimiting.RateLimitingMiddleware") ||
            stackTrace.Contains("CombinedAcquire"))
        {
            return new DistributedRateLimitLease(isAcquired: false, retryAfter: TimeSpan.Zero);
        }

        throw new NotSupportedException(
            "DistributedRateLimiterBridge no admite adquisición sincrónica porque la comunicación con Redis es asíncrona por red. " +
            "El middleware de ASP.NET Core debe utilizar la ruta asíncrona AcquireAsync.");
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
