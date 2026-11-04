using UniHome.Application.Identity.Abstractions;

namespace UniHome.Application.Identity.Logout;

/// <summary>
/// HU-005: revoca la sesión del dispositivo actual identificada por el claim sid.
/// No toca otras sesiones de la misma cuenta.
/// </summary>
public sealed class LogoutService
{
    private const string LogoutReason = "logout";

    private readonly IRefreshSessionRepository _sessions;
    private readonly ISystemClock _clock;

    public LogoutService(IRefreshSessionRepository sessions, ISystemClock clock)
    {
        _sessions = sessions;
        _clock = clock;
    }

    public Task<bool> LogoutAsync(
        Guid accountId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty || sessionId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        return _sessions.RevokeAsync(
            sessionId,
            accountId,
            _clock.UtcNow,
            LogoutReason,
            cancellationToken);
    }
}
