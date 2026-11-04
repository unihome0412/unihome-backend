namespace UniHome.Application.Identity.Abstractions;

/// <summary>
/// Genera refresh tokens opacos y calcula su verificador no reversible.
/// El identificador de sesión viaja en el token para localizar la sesión sin
/// exponer datos de la cuenta ni permitir que el cliente elija el sujeto.
/// </summary>
public interface IRefreshTokenGenerator
{
    GeneratedRefreshToken Generate(Guid sessionId);

    Guid? TryGetSessionId(string refreshToken);

    string Hash(string refreshToken);
}

public sealed record GeneratedRefreshToken(string Value, string Hash);
