using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Sessions;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>Sincroniza dos llamadas al CAS real; no sustituye la persistencia.</summary>
public sealed class RefreshRotationBarrier
{
    private TaskCompletionSource? _ready;
    private int _arrivals;
    public int Arrivals => Volatile.Read(ref _arrivals);

    public void Arm()
    {
        _arrivals = 0;
        _ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        var ready = Volatile.Read(ref _ready);
        if (ready is null)
        {
            return;
        }

        if (Interlocked.Increment(ref _arrivals) == 2)
        {
            Interlocked.CompareExchange(ref _ready, null, ready);
            ready.TrySetResult();
        }
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
    }
}

internal sealed class CoordinatedRefreshSessionRepository(
    IRefreshSessionRepository inner, RefreshRotationBarrier barrier) : IRefreshSessionRepository
{
    public Task CreateAsync(RefreshSession session, CancellationToken cancellationToken = default) =>
        inner.CreateAsync(session, cancellationToken);

    public Task<RefreshSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        inner.FindByIdAsync(sessionId, cancellationToken);

    public async Task<bool> TryRotateAsync(Guid sessionId, string currentTokenHash, string newTokenHash,
        DateTimeOffset now, DateTimeOffset newExpiresAt, CancellationToken cancellationToken = default)
    {
        await barrier.WaitAsync(cancellationToken);
        return await inner.TryRotateAsync(sessionId, currentTokenHash, newTokenHash, now, newExpiresAt, cancellationToken);
    }

    public Task<bool> RevokeAsync(Guid sessionId, Guid accountId, DateTimeOffset now, string reason,
        CancellationToken cancellationToken = default) =>
        inner.RevokeAsync(sessionId, accountId, now, reason, cancellationToken);

    public Task RevokeFamilyAsync(Guid sessionId, DateTimeOffset now, string reason,
        CancellationToken cancellationToken = default) =>
        inner.RevokeFamilyAsync(sessionId, now, reason, cancellationToken);

    public Task RevokeAllForAccountAsync(Guid accountId, DateTimeOffset now, string reason,
        CancellationToken cancellationToken = default) =>
        inner.RevokeAllForAccountAsync(accountId, now, reason, cancellationToken);
}
