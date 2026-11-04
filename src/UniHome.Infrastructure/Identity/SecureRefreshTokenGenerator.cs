using System.Security.Cryptography;
using System.Text;
using UniHome.Application.Identity.Abstractions;

namespace UniHome.Infrastructure.Identity;

/// <summary>
/// Refresh token opaco de 256 bits. El valor se devuelve una sola vez; en MongoDB
/// solo se persiste SHA-256 del token completo. El prefijo es el identificador de
/// sesión, no un dato personal.
/// </summary>
internal sealed class SecureRefreshTokenGenerator : IRefreshTokenGenerator
{
    private const int TokenBytes = 32;
    private const int MaxTokenLength = 256;
    private const int MinSecretLength = 32;
    private const int MaxSecretLength = 128;

    public GeneratedRefreshToken Generate(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("La sesión es obligatoria.", nameof(sessionId));
        }

        var secret = Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));
        var value = $"{sessionId:N}.{secret}";

        return new GeneratedRefreshToken(value, Hash(value));
    }

    public Guid? TryGetSessionId(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > MaxTokenLength)
        {
            return null;
        }

        var separator = refreshToken.IndexOf('.', StringComparison.Ordinal);

        if (separator <= 0 || separator == refreshToken.Length - 1)
        {
            return null;
        }

        var sessionPart = refreshToken[..separator];
        var secretPart = refreshToken[(separator + 1)..];

        if (!Guid.TryParseExact(sessionPart, "N", out var sessionId) ||
            secretPart.Length is < MinSecretLength or > MaxSecretLength ||
            secretPart.Any(character => !IsBase64UrlCharacter(character)))
        {
            return null;
        }

        return sessionId;
    }

    public string Hash(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }

    private static bool IsBase64UrlCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '-' or '_';

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
