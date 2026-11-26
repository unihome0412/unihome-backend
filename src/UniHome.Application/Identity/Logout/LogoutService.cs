using UniHome.Application.Identity.Abstractions;

namespace UniHome.Application.Identity.Logout;

/// <summary>
/// HU-005: revoca la sesión del dispositivo actual identificada por el claim sid.
/// No toca otras sesiones de la misma cuenta. Además denylistea el sid en
/// Redis: a diferencia del refresh (que ya no puede renovarse), el access
/// token emitido para esta sesión seguiría siendo válido hasta su expiración
/// si no se revoca de inmediato.
/// </summary>
public sealed class LogoutService
{
    private const string LogoutReason = "logout";

    private readonly IRefreshSessionRepository _sessions;
    private readonly ISessionDenylist _denylist;
    private readonly IAuthTokenSettings _tokenSettings;
    private readonly ISystemClock _clock;

    public LogoutService(
        IRefreshSessionRepository sessions,
        ISessionDenylist denylist,
        IAuthTokenSettings tokenSettings,
        ISystemClock clock)
    {
        _sessions = sessions;
        _denylist = denylist;
        _tokenSettings = tokenSettings;
        _clock = clock;
    }

    public async Task<bool> LogoutAsync(
        Guid accountId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty || sessionId == Guid.Empty)
        {
            return false;
        }

        var revoked = await _sessions.RevokeAsync(
            sessionId,
            accountId,
            _clock.UtcNow,
            LogoutReason,
            cancellationToken).ConfigureAwait(false);

        if (revoked)
        {
            await _denylist
                .DenyAsync(sessionId, _tokenSettings.AccessTokenLifetime, cancellationToken)
                .ConfigureAwait(false);
        }

        return revoked;
    }
}
