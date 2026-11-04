using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Sessions;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Implementación en memoria que reproduce la atomicidad de la rotación: solo una
/// operación puede consumir el hash vigente. El lock sustituye al findOneAndUpdate
/// de MongoDB sin cambiar la semántica observable.
/// </summary>
public sealed class InMemoryRefreshSessionRepository : IRefreshSessionRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, RefreshSession> _sessions = [];

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _sessions.Count;
            }
        }
    }

    public Task CreateAsync(
        RefreshSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_gate)
        {
            if (!_sessions.TryAdd(session.Id, session))
            {
                throw new InvalidOperationException("La sesión ya existe.");
            }
        }

        return Task.CompletedTask;
    }

    public Task<RefreshSession?> FindByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _sessions.TryGetValue(sessionId, out var session);
            return Task.FromResult(session);
        }
    }

    public Task<bool> TryRotateAsync(
        Guid sessionId,
        string currentTokenHash,
        string newTokenHash,
        DateTimeOffset now,
        DateTimeOffset newExpiresAt,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var session) ||
                session.RevokedAt is not null ||
                session.ExpiresAt <= now ||
                !string.Equals(session.CurrentTokenHash, currentTokenHash, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            _sessions[sessionId] = session with
            {
                CurrentTokenHash = newTokenHash,
                CurrentTokenIssuedAt = now,
                ExpiresAt = newExpiresAt,
                UsedTokens = [.. session.UsedTokens, new UsedRefreshToken(currentTokenHash, now)],
                LastRotatedAt = now
            };

            return Task.FromResult(true);
        }
    }

    public Task<bool> RevokeAsync(
        Guid sessionId,
        Guid accountId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var session) ||
                session.AccountId != accountId ||
                session.RevokedAt is not null)
            {
                return Task.FromResult(false);
            }

            _sessions[sessionId] = session with
            {
                RevokedAt = now,
                RevokedReason = reason
            };

            return Task.FromResult(true);
        }
    }

    public Task RevokeFamilyAsync(
        Guid sessionId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(sessionId, out var session) &&
                session.RevokedAt is null)
            {
                _sessions[sessionId] = session with
                {
                    RevokedAt = now,
                    RevokedReason = reason
                };
            }
        }

        return Task.CompletedTask;
    }

    public void SetExpiresAt(Guid sessionId, DateTimeOffset expiresAt)
    {
        lock (_gate)
        {
            _sessions[sessionId] = _sessions[sessionId] with { ExpiresAt = expiresAt };
        }
    }
}
