namespace UniHome.Application.Identity.Abstractions;

/// <summary>
/// Vigencias de autenticación configurables. La vigencia del token de acceso la
/// aplica el emisor; Application solo necesita conocer la del refresh.
/// </summary>
public interface IAuthTokenSettings
{
    TimeSpan RefreshTokenLifetime { get; }
}
