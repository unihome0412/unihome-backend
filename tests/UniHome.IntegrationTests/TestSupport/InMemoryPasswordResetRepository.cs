using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class InMemoryPasswordResetRepository : IPasswordResetRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, PasswordResetToken> _tokens = [];

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _tokens.Count;
            }
        }
    }

    public Task AddAsync(
        PasswordResetToken token,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        lock (_gate)
        {
            _tokens.Add(token.Id, token);
        }

        return Task.CompletedTask;
    }

    public Task<PasswordResetToken?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _tokens.TryGetValue(id, out var token);
            return Task.FromResult(token);
        }
    }

    public Task<bool> TryConsumeAsync(
        Guid id,
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_tokens.TryGetValue(id, out var token) ||
                token.IsConsumed ||
                token.IsExpired(now) ||
                token.HasExceededAttempts ||
                !string.Equals(token.TokenHash, tokenHash, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            token.Consume(now);
            return Task.FromResult(true);
        }
    }

    public Task<PasswordResetToken?> RegisterFailedAttemptAsync(
        Guid id,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_tokens.TryGetValue(id, out var token) ||
                token.IsConsumed ||
                token.IsExpired(now) ||
                token.HasExceededAttempts)
            {
                return Task.FromResult<PasswordResetToken?>(null);
            }

            token.RegisterFailedAttempt();
            return Task.FromResult<PasswordResetToken?>(token);
        }
    }

    public Task InvalidateAllForAccountAsync(
        Guid accountId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            foreach (var token in _tokens.Values.Where(
                token => token.AccountId == accountId && !token.IsConsumed).ToList())
            {
                token.Consume(now);
            }
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<PasswordResetToken> All
    {
        get
        {
            lock (_gate)
            {
                return _tokens.Values.ToArray();
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _tokens.Clear();
        }
    }
}

public sealed record SentPasswordResetEmail(string Email, string Token);

public sealed class CapturingPasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly object _gate = new();
    private readonly List<SentPasswordResetEmail> _messages = [];

    public IReadOnlyList<SentPasswordResetEmail> Messages
    {
        get
        {
            lock (_gate)
            {
                return _messages.ToArray();
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _messages.Clear();
        }
    }

    public Task<bool> SendAsync(
        EmailAddress email,
        string token,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _messages.Add(new SentPasswordResetEmail(email.Value, token));
        }

        return Task.FromResult(true);
    }
}
