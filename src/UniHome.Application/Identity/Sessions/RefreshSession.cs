namespace UniHome.Application.Identity.Sessions;

/// <summary>
/// Sesión lógica de un dispositivo. Cada inicio de sesión crea una familia nueva.
/// Solo se persisten hashes de los refresh tokens, nunca el valor en claro.
/// </summary>
public sealed record RefreshSession(
    Guid Id,
    Guid AccountId,
    string CurrentTokenHash,
    DateTimeOffset CurrentTokenIssuedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<UsedRefreshToken> UsedTokens,
    DateTimeOffset? RevokedAt,
    string? RevokedReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastRotatedAt);

/// <summary>
/// Huella de un refresh token ya rotado. Sirve para detectar reuso sin conservar
/// el token original.
/// </summary>
public sealed record UsedRefreshToken(string TokenHash, DateTimeOffset UsedAt);
