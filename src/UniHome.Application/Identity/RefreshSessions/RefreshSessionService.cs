using System.Security.Cryptography;
using System.Text;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Sessions;
using UniHome.Domain.Accounts;

namespace UniHome.Application.Identity.RefreshSessions;

/// <summary>
/// EP-M01-08: renueva la sesión con rotación de refresh token. El consumo del
/// token vigente es atómico en persistencia. Si se presenta un token ya rotado,
/// se revoca la familia completa para contener el robo de tokens.
/// </summary>
public sealed class RefreshSessionService
{
    private const int MaxRefreshTokenLength = 256;
    private const string ReuseReason = "refresh_token_reuse";
    private const string AccountUnavailableReason = "account_unavailable";

    private readonly IRefreshSessionRepository _sessions;
    private readonly IAccountRepository _accounts;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly IAccessTokenIssuer _accessTokens;
    private readonly IAuthTokenSettings _tokenSettings;
    private readonly ISystemClock _clock;

    public RefreshSessionService(
        IRefreshSessionRepository sessions,
        IAccountRepository accounts,
        IRefreshTokenGenerator refreshTokens,
        IAccessTokenIssuer accessTokens,
        IAuthTokenSettings tokenSettings,
        ISystemClock clock)
    {
        _sessions = sessions;
        _accounts = accounts;
        _refreshTokens = refreshTokens;
        _accessTokens = accessTokens;
        _tokenSettings = tokenSettings;
        _clock = clock;
    }

    public async Task<RefreshSessionResult> RefreshAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return RefreshSessionResult.ValidationFailed(new Dictionary<string, string[]>(
                StringComparer.Ordinal)
            {
                ["refreshToken"] = ["El refresh token es obligatorio."]
            });
        }

        if (command.RefreshToken.Length > MaxRefreshTokenLength)
        {
            return RefreshSessionResult.Invalid();
        }

        var sessionId = _refreshTokens.TryGetSessionId(command.RefreshToken);

        if (sessionId is null)
        {
            return RefreshSessionResult.Invalid();
        }

        var now = _clock.UtcNow;
        var session = await _sessions
            .FindByIdAsync(sessionId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (session is null || session.RevokedAt is not null || session.ExpiresAt <= now)
        {
            return RefreshSessionResult.Invalid();
        }

        var presentedHash = _refreshTokens.Hash(command.RefreshToken);

        if (!HashMatches(session.CurrentTokenHash, presentedHash))
        {
            if (ContainsUsedToken(session, presentedHash))
            {
                await _sessions
                    .RevokeFamilyAsync(session.Id, now, ReuseReason, cancellationToken)
                    .ConfigureAwait(false);
            }

            return RefreshSessionResult.Invalid();
        }

        var account = await _accounts
            .FindByIdAsync(session.AccountId, cancellationToken)
            .ConfigureAwait(false);

        if (account is null ||
            account.Status != AccountStatus.Active ||
            !account.IsEmailConfirmed)
        {
            await _sessions
                .RevokeFamilyAsync(session.Id, now, AccountUnavailableReason, cancellationToken)
                .ConfigureAwait(false);

            return RefreshSessionResult.Invalid();
        }

        var newToken = _refreshTokens.Generate(session.Id);
        var newExpiresAt = now.Add(_tokenSettings.RefreshTokenLifetime);

        var rotated = await _sessions
            .TryRotateAsync(
                session.Id,
                presentedHash,
                newToken.Hash,
                now,
                newExpiresAt,
                cancellationToken)
            .ConfigureAwait(false);

        if (!rotated)
        {
            // Otra solicitud consumió el token entre la lectura y la rotación.
            // Si el token ya figura como usado, es reuso y se revoca la familia.
            var latest = await _sessions
                .FindByIdAsync(session.Id, cancellationToken)
                .ConfigureAwait(false);

            if (latest is not null && ContainsUsedToken(latest, presentedHash))
            {
                await _sessions
                    .RevokeFamilyAsync(session.Id, now, ReuseReason, cancellationToken)
                    .ConfigureAwait(false);
            }

            return RefreshSessionResult.Invalid();
        }

        var accessToken = _accessTokens.Issue(account.Id, session.Id, now);

        return RefreshSessionResult.Success(
            accessToken.Value,
            newToken.Value,
            accessToken.ExpiresAt);
    }

    private static bool ContainsUsedToken(RefreshSession session, string tokenHash) =>
        session.UsedTokens.Any(used => HashMatches(used.TokenHash, tokenHash));

    private static bool HashMatches(string left, string right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(left),
            Encoding.ASCII.GetBytes(right));
    }
}
