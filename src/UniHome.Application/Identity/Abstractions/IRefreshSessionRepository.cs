using UniHome.Application.Identity.Sessions;

namespace UniHome.Application.Identity.Abstractions;

/// <summary>
/// Persistencia de sesiones y refresh tokens. La rotación debe ser atómica en el
/// adaptador: solo una operación puede consumir el token vigente.
/// </summary>
public interface IRefreshSessionRepository
{
    Task CreateAsync(RefreshSession session, CancellationToken cancellationToken = default);

    Task<RefreshSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consume <paramref name="currentTokenHash"/> y activa <paramref name="newTokenHash"/>
    /// en una sola operación atómica. Devuelve false si la sesión no existe, está
    /// revocada, venció o el token dejó de ser el vigente.
    /// </summary>
    Task<bool> TryRotateAsync(
        Guid sessionId,
        string currentTokenHash,
        string newTokenHash,
        DateTimeOffset now,
        DateTimeOffset newExpiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca una sesión solo si pertenece a la cuenta indicada. Devuelve false si
    /// la sesión ya no existe o ya estaba revocada.
    /// </summary>
    Task<bool> RevokeAsync(
        Guid sessionId,
        Guid accountId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca la familia completa ante reuso de un refresh token rotado.
    /// </summary>
    Task RevokeFamilyAsync(
        Guid sessionId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// HU-096: revoca todas las sesiones activas de la cuenta (todos los
    /// dispositivos), a diferencia de logout que solo afecta la sesión actual.
    /// </summary>
    Task RevokeAllForAccountAsync(
        Guid accountId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken = default);
}
