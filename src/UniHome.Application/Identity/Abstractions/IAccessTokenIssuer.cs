namespace UniHome.Application.Identity.Abstractions;

/// <summary>
/// Emite tokens de acceso firmados. Application no conoce el algoritmo ni la
/// librería criptográfica; solo recibe el token y su vencimiento en UTC.
/// </summary>
public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(Guid accountId, Guid sessionId, DateTimeOffset now);
}

public sealed record IssuedAccessToken(string Value, DateTimeOffset ExpiresAt);
