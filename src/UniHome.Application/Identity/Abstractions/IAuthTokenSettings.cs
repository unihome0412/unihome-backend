namespace UniHome.Application.Identity.Abstractions;

/// <summary>
/// Vigencias de autenticación configurables.
/// </summary>
public interface IAuthTokenSettings
{
    TimeSpan RefreshTokenLifetime { get; }

    /// <summary>
    /// Vigencia máxima del token de acceso. Se usa como TTL de la entrada de
    /// denylist en Redis al revocar un <c>sid</c>: después de ese tiempo el
    /// propio token ya habría expirado, así que no hace falta conservar la
    /// entrada más tiempo.
    /// </summary>
    TimeSpan AccessTokenLifetime { get; }
}
