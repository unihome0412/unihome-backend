using System.Security.Cryptography;
using System.Text;

namespace UniHome.Application.Abstractions.RateLimiting;

/// <summary>
/// Seudonimiza las direcciones IP de los clientes mediante HMAC-SHA256 utilizando una sal
/// criptográfica como clave antes de emplearlas en las claves de partición de Redis.
/// 
/// Nota de diseño sobre rotación de claves:
/// Rotar la sal (Auth__RateLimit__IpHashSalt) altera todas las claves derivadas generadas
/// para una misma IP. Esto invalida de forma inmediata y desacoplada el mapeo anterior,
/// reiniciando efectivamente el historial de contadores en Redis sin requerir operaciones FLUSHALL.
/// </summary>
public static class ClientIpHasher
{
    public const int MinSaltLength = 16;

    /// <summary>
    /// Genera un hash determinista en formato hexadecimal en minúsculas para la dirección IP.
    /// Si la IP es nula o vacía, devuelve "unknown".
    /// Si la sal está vacía (solo permitido en desarrollo local), utiliza SHA-256 directo como fallback.
    /// </summary>
    public static string Hash(string? ipAddress, string? salt)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return "unknown";
        }

        var normalizedIp = ipAddress.Trim();
        var ipBytes = Encoding.UTF8.GetBytes(normalizedIp);

        if (string.IsNullOrEmpty(salt))
        {
            var fallbackHash = SHA256.HashData(ipBytes);
            return Convert.ToHexString(fallbackHash).ToLowerInvariant();
        }

        var saltBytes = Encoding.UTF8.GetBytes(salt);
        var hmacHash = HMACSHA256.HashData(saltBytes, ipBytes);
        return Convert.ToHexString(hmacHash).ToLowerInvariant();
    }
}
