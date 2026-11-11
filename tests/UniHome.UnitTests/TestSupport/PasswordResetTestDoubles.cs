using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;

namespace UniHome.UnitTests.TestSupport;

internal sealed class InMemoryPasswordResetRepository : IPasswordResetRepository
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
}

internal sealed class FixedPasswordResetTokenGenerator : IPasswordResetTokenGenerator
{
    private int _counter;

    public Guid NextId { get; private set; } = Guid.NewGuid();

    public GeneratedPasswordResetToken Generate()
    {
        NextId = Guid.NewGuid();
        var token = CreateToken(NextId);
        return new GeneratedPasswordResetToken(NextId, token, Hash(token));
    }

    public string Hash(string token) => $"hash:{token}";

    public Guid? TryGetTokenId(string token)
    {
        var separator = token.IndexOf('.', StringComparison.Ordinal);

        if (separator <= 0 ||
            !Guid.TryParseExact(token[..separator], "N", out var id) ||
            token[(separator + 1)..].Length != 43)
        {
            return null;
        }

        return id;
    }

    public string CreateToken(Guid id) =>
        $"{id:N}.{new string((char)('A' + (_counter++ % 26)), 43)}";
}

internal sealed class RecordingPasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly List<(string Email, string Token)> _messages = [];

    public IReadOnlyList<(string Email, string Token)> Messages => _messages;

    public Task<bool> SendAsync(
        EmailAddress email,
        string token,
        CancellationToken cancellationToken = default)
    {
        _messages.Add((email.Value, token));
        return Task.FromResult(true);
    }
}

internal sealed class ThrowingPasswordResetEmailSender : IPasswordResetEmailSender
{
    public Task<bool> SendAsync(
        EmailAddress email,
        string token,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Fallo simulado de SMTP.");
}

internal sealed class ThrowingPasswordResetRepository : IPasswordResetRepository
{
    public Task AddAsync(
        PasswordResetToken token,
        CancellationToken cancellationToken = default) =>
        throw new UniHome.Application.Identity.Exceptions.StorageUnavailableException();

    public Task<PasswordResetToken?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        throw new UniHome.Application.Identity.Exceptions.StorageUnavailableException();

    public Task<bool> TryConsumeAsync(
        Guid id,
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        throw new UniHome.Application.Identity.Exceptions.StorageUnavailableException();

    public Task<PasswordResetToken?> RegisterFailedAttemptAsync(
        Guid id,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        throw new UniHome.Application.Identity.Exceptions.StorageUnavailableException();

    public Task InvalidateAllForAccountAsync(
        Guid accountId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        throw new UniHome.Application.Identity.Exceptions.StorageUnavailableException();
}
