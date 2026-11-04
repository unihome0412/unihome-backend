using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Sessions;
using UniHome.Domain.Accounts;

namespace UniHome.UnitTests.TestSupport;

internal static class AccountTestData
{
    public const string Password = "ClaveSegura1";

    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public static Account Create(
        string email,
        string password = Password,
        AccountStatus status = AccountStatus.Active,
        bool emailConfirmed = true) =>
        Account.Restore(
            Guid.NewGuid(),
            "Ana López",
            EmailAddress.Create(email),
            new DateOnly(2004, 5, 10),
            Gender.Mujer,
            $"hash:{password}",
            new PrivacyAcceptance("2026-01", Now),
            status,
            emailConfirmed,
            Now,
            emailConfirmed ? Now : null,
            emailVerification: null,
            verificationResendAttempts: []);

    public static Account WithStatus(Account account, AccountStatus status) =>
        Account.Restore(
            account.Id,
            account.FullName,
            account.Email,
            account.BirthDate,
            account.Gender,
            account.PasswordHash,
            account.PrivacyAcceptance,
            status,
            account.IsEmailConfirmed,
            account.CreatedAt,
            account.EmailConfirmedAt,
            account.EmailVerification,
            account.VerificationResendAttempts);

    public static Account WithEmailConfirmed(Account account, bool confirmed) =>
        Account.Restore(
            account.Id,
            account.FullName,
            account.Email,
            account.BirthDate,
            account.Gender,
            account.PasswordHash,
            account.PrivacyAcceptance,
            account.Status,
            confirmed,
            account.CreatedAt,
            confirmed ? account.CreatedAt : null,
            account.EmailVerification,
            account.VerificationResendAttempts);
}

internal sealed class FixedAuthTokenSettings : IAuthTokenSettings
{
    public FixedAuthTokenSettings(TimeSpan? refreshTokenLifetime = null)
    {
        RefreshTokenLifetime = refreshTokenLifetime ?? TimeSpan.FromDays(30);
    }

    public TimeSpan RefreshTokenLifetime { get; }
}

internal sealed class FixedDummyPasswordHashProvider : IDummyPasswordHashProvider
{
    public string Hash => "hash:dummy";
}

internal sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    public List<(Guid AccountId, Guid SessionId, DateTimeOffset Now)> Calls { get; } = [];

    public IssuedAccessToken Issue(Guid accountId, Guid sessionId, DateTimeOffset now)
    {
        Calls.Add((accountId, sessionId, now));
        return new IssuedAccessToken(
            $"access:{accountId:N}:{sessionId:N}",
            now.AddMinutes(60));
    }
}

internal sealed class FakeRefreshTokenGenerator : IRefreshTokenGenerator
{
    private int _counter;

    public GeneratedRefreshToken Generate(Guid sessionId)
    {
        _counter++;
        var token = $"{sessionId:N}.secret{_counter}";
        return new GeneratedRefreshToken(token, Hash(token));
    }

    public Guid? TryGetSessionId(string refreshToken)
    {
        var separator = refreshToken.IndexOf('.', StringComparison.Ordinal);

        if (separator <= 0 ||
            !refreshToken[(separator + 1)..].StartsWith("secret", StringComparison.Ordinal) ||
            !Guid.TryParseExact(refreshToken[..separator], "N", out var sessionId))
        {
            return null;
        }

        return sessionId;
    }

    public string Hash(string refreshToken) => $"hash:{refreshToken}";
}

internal sealed class FakeRefreshSessionRepository : IRefreshSessionRepository
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
        lock (_gate)
        {
            _sessions.Add(session.Id, session);
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
                UsedTokens =
                [
                    .. session.UsedTokens,
                    new UsedRefreshToken(currentTokenHash, now)
                ],
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

    public RefreshSession Single() =>
        _sessions.Values.Single();
}
