using System.Security.Cryptography;
using System.Text;
using UniHome.Application.Identity.Abstractions;

namespace UniHome.Infrastructure.Identity;

/// <summary>
/// Token de recuperación de 256 bits. El valor visible incluye el identificador
/// interno para poder aplicar un límite de intentos; en MongoDB solo se persiste
/// SHA-256 del token completo y el token en claro se entrega una sola vez.
/// </summary>
internal sealed class SecurePasswordResetTokenGenerator : IPasswordResetTokenGenerator
{
    private const int SecretBytes = 32;
    private const int SecretCharacters = 43;
    private const int MaxTokenLength = 128;

    public GeneratedPasswordResetToken Generate()
    {
        var id = Guid.NewGuid();
        var secret = Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretBytes));
        var value = $"{id:N}.{secret}";

        return new GeneratedPasswordResetToken(id, value, Hash(value));
    }

    public string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    public Guid? TryGetTokenId(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
        {
            return null;
        }

        var separator = token.IndexOf('.', StringComparison.Ordinal);

        if (separator <= 0 || separator == token.Length - 1)
        {
            return null;
        }

        var idPart = token[..separator];
        var secretPart = token[(separator + 1)..];

        if (!Guid.TryParseExact(idPart, "N", out var id) ||
            secretPart.Length != SecretCharacters ||
            secretPart.Any(character => !IsBase64UrlCharacter(character)))
        {
            return null;
        }

        return id;
    }

    private static bool IsBase64UrlCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '-' or '_';

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
