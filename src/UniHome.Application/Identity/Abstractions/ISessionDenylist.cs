namespace UniHome.Application.Identity.Abstractions;

/// <summary>
/// Revocación inmediata de tokens de acceso ya emitidos, por <c>sid</c>
/// (identificador de la sesión de refresh que los originó). A diferencia de
/// <see cref="IRefreshSessionRepository"/> (que revoca la posibilidad de
/// renovar), esto corta el acceso de un token que todavía no expiró —
/// logout, restablecimiento de contraseña, eliminación o suspensión de
/// cuenta no esperan a que el access token expire por sí solo.
/// </summary>
public interface ISessionDenylist
{
    /// <summary>
    /// Deniega <paramref name="sessionId"/> por <paramref name="ttl"/>. El TTL
    /// debe ser al menos la vigencia restante del token de acceso: pasado ese
    /// tiempo el propio token ya expiró y la entrada deja de ser necesaria.
    /// </summary>
    Task DenyAsync(Guid sessionId, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>Indica si <paramref name="sessionId"/> está denegado.</summary>
    Task<bool> IsDeniedAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
