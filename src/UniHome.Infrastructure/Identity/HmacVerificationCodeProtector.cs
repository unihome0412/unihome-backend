using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniHome.Application.Identity.Abstractions;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Identity;

/// <summary>
/// Protege el código de confirmación con HMAC-SHA256. Si no hay secreto configurado,
/// genera uno efímero por proceso (los códigos pendientes dejan de validar al reiniciar).
/// Nunca se registra el secreto ni el código.
/// </summary>
public sealed class HmacVerificationCodeProtector : IVerificationCodeProtector
{
    private const int EphemeralSecretBytes = 32;

    private readonly byte[] _key;

    public HmacVerificationCodeProtector(
        IOptions<VerificationCodeOptions> options,
        ILogger<HmacVerificationCodeProtector> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        if (options.Value.HasStrongSecret)
        {
            _key = Encoding.UTF8.GetBytes(options.Value.HmacSecret!);
            return;
        }

        _key = RandomNumberGenerator.GetBytes(EphemeralSecretBytes);
        logger.LogWarning(
            "No hay un secreto HMAC configurado para códigos de confirmación; se usará uno efímero por proceso.");
    }

    public string Protect(string code)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);

        var hash = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(code));

        return Convert.ToBase64String(hash);
    }

    public bool Matches(string code, string protectedCode)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(protectedCode))
        {
            return false;
        }

        try
        {
            var expected = Convert.FromBase64String(protectedCode);
            var actual = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(code));

            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
