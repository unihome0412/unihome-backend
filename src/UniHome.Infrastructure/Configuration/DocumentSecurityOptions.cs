using System.Security.Cryptography;

namespace UniHome.Infrastructure.Configuration;

/// <summary>
/// Claves del almacenamiento documental. La clave de cifrado usa AES-256-GCM; la
/// de firma de URLs se deriva de ella si no se configura una independiente. Fuera
/// de Development ambas deben provenir de un almacén de secretos.
/// </summary>
public sealed class DocumentSecurityOptions
{
    public const string SectionName = "DocumentSecurity";
    public const int KeyBytes = 32;
    public const int MinUrlSigningKeyLength = 32;
    public const int DefaultUrlLifetimeSeconds = 300;
    public const int MaxUrlLifetimeSeconds = 900;

    public string EncryptionKey { get; set; } = string.Empty;

    /// <summary>Versión de la clave actual; las anteriores se conservan para leer.</summary>
    public int CurrentKeyVersion { get; set; } = 1;
    /// <summary>Claves anteriores en Base64, ordenadas de la versión 1 a N-1.</summary>
    public List<string> PreviousEncryptionKeys { get; set; } = [];

    public string UrlSigningKey { get; set; } = string.Empty;

    public int UrlLifetimeSeconds { get; set; } = DefaultUrlLifetimeSeconds;

    /// <summary>
    /// Base absoluta para construir enlaces (por ejemplo, la URL pública de la
    /// API). Si está vacía se devuelve una URL relativa; nunca se confía en Host.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    public bool EphemeralEncryptionKey { get; set; }

    public byte[] GetEncryptionKeyBytes()
    {
        try
        {
            var bytes = Convert.FromBase64String(EncryptionKey);
            return bytes.Length == KeyBytes
                ? bytes
                : throw new InvalidOperationException(
                    $"{SectionName}:EncryptionKey debe contener {KeyBytes} bytes en Base64.");
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                $"{SectionName}:EncryptionKey debe ser Base64 válido.");
        }
    }

    public byte[] GetEncryptionKeyBytes(int version)
    {
        if (version == CurrentKeyVersion)
        {
            return GetEncryptionKeyBytes();
        }

        var index = version - 1;

        if (version < 1 || index >= PreviousEncryptionKeys.Count)
        {
            throw new InvalidOperationException(
                $"No existe una clave documental para la versión {version}.");
        }

        try
        {
            var bytes = Convert.FromBase64String(PreviousEncryptionKeys[index]);
            return bytes.Length == KeyBytes
                ? bytes
                : throw new InvalidOperationException(
                    $"{SectionName}:PreviousEncryptionKeys[{index}] debe contener " +
                    $"{KeyBytes} bytes en Base64.");
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                $"{SectionName}:PreviousEncryptionKeys[{index}] debe ser Base64 válido.");
        }
    }

    public byte[] GetUrlSigningKeyBytes(byte[] encryptionKey)
    {
        if (!string.IsNullOrWhiteSpace(UrlSigningKey))
        {
            return System.Text.Encoding.UTF8.GetBytes(UrlSigningKey);
        }

        return HMACSHA256.HashData(
            encryptionKey,
            "unihome:document-access-url:v1"u8);
    }

    public static string CreateEphemeralKey() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeyBytes));

    public bool IsValid() =>
        UrlLifetimeSeconds is > 0 and <= MaxUrlLifetimeSeconds &&
        CurrentKeyVersion >= 1 &&
        CurrentKeyVersion <= 6 &&
        PreviousEncryptionKeys.Count == CurrentKeyVersion - 1 &&
        PreviousEncryptionKeys.All(key => IsValidBase64Key(key)) &&
        (string.IsNullOrWhiteSpace(UrlSigningKey) ||
         UrlSigningKey.Length >= MinUrlSigningKeyLength) &&
        (string.IsNullOrWhiteSpace(PublicBaseUrl) ||
         Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri) &&
         uri.Scheme == Uri.UriSchemeHttps);

    private static bool IsValidBase64Key(string value)
    {
        try
        {
            return Convert.FromBase64String(value).Length == KeyBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public bool HasValidEncryptionKey()
    {
        try
        {
            return GetEncryptionKeyBytes().Length == KeyBytes;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
