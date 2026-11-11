using System.Security.Cryptography;
using System.Text;

namespace UniHome.Application.Abstractions.RateLimiting;

/// <summary>
/// Seudonimiza las direcciones IP de los clientes mediante SHA-256 y una sal criptográfica
/// antes de utilizarlas en las claves de Redis.
/// </summary>
public static class ClientIpHasher
{
    public static string Hash(string? ipAddress, string? salt)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return "unknown";
        }

        var normalized = ipAddress.Trim();
        var input = string.IsNullOrEmpty(salt) ? normalized : $"{normalized}:{salt}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
