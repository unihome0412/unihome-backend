using System.Collections.Concurrent;
using UniHome.Application.Abstractions.RateLimiting;
using UniHome.Application.Identity.Abstractions;

namespace UniHome.Infrastructure.Redis;

/// <summary>
/// Limitador de tasa en memoria compatible con IDistributedRateLimiter.
/// Empleado en entornos de desarrollo local y pruebas automatizadas, o como estado compartido
/// simulado entre múltiples instancias de prueba en el mismo proceso.
/// </summary>
public sealed class InMemoryDistributedRateLimiter : IDistributedRateLimiter
{
    private sealed class WindowCounter
    {
        public long Count { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }

    private readonly ConcurrentDictionary<string, WindowCounter> _counters = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private readonly ISystemClock _clock;

    public InMemoryDistributedRateLimiter(ISystemClock? clock = null)
    {
        _clock = clock ?? new Identity.SystemClock();
    }

    public Task<RateLimitResult> CheckAndIncrementAsync(
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);

        var now = _clock.UtcNow;

        lock (_lock)
        {
            if (!_counters.TryGetValue(partitionKey, out var entry) || now >= entry.ExpiresAt)
            {
                entry = new WindowCounter
                {
                    Count = 1,
                    ExpiresAt = now.Add(window)
                };
                _counters[partitionKey] = entry;

                return Task.FromResult(RateLimitResult.Allowed(entry.Count, window));
            }

            entry.Count++;
            var remainingTtl = entry.ExpiresAt - now;
            if (remainingTtl < TimeSpan.Zero)
            {
                remainingTtl = TimeSpan.Zero;
            }

            if (entry.Count <= permitLimit)
            {
                return Task.FromResult(RateLimitResult.Allowed(entry.Count, remainingTtl));
            }

            return Task.FromResult(RateLimitResult.Exceeded(entry.Count, remainingTtl));
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _counters.Clear();
        }
    }
}
