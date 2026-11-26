using System.Collections.Concurrent;
using UniHome.Application.Identity.Abstractions;

namespace UniHome.Infrastructure.Redis;

/// <summary>
/// Denylist de <c>sid</c> en memoria de proceso. Solo para Development y
/// pruebas automatizadas; fuera de Development Redis es obligatorio (igual
/// que el rate limiting), porque esto no comparte estado entre réplicas.
/// </summary>
public sealed class InMemorySessionDenylist : ISessionDenylist
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _denied = new();

    public Task DenyAsync(Guid sessionId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (sessionId != Guid.Empty)
        {
            _denied[sessionId] = DateTimeOffset.UtcNow.Add(ttl);
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsDeniedAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (_denied.TryGetValue(sessionId, out var expiresAt))
        {
            if (expiresAt > DateTimeOffset.UtcNow)
            {
                return Task.FromResult(true);
            }

            _denied.TryRemove(sessionId, out _);
        }

        return Task.FromResult(false);
    }
}
