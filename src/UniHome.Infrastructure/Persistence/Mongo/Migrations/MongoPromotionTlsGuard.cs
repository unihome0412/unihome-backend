using MongoDB.Driver;

namespace UniHome.Infrastructure.Persistence.Mongo.Migrations;

/// <summary>
/// Comprobación de seguridad para la conexión de promoción. No modifica TLS ni
/// certificados: rechaza una configuración que desactive TLS, la validación del
/// certificado o que instale una validación personalizada.
/// </summary>
public static class MongoPromotionTlsGuard
{
    private static readonly string[] InsecureTrueOptions =
    [
        "tlsinsecure",
        "tlsallowinvalidcertificates",
        "tlsallowinvalidhostnames",
        "sslinvalidhostnameallowed",
        "tlsdisablecertificaterevocationcheck"
    ];

    public static void EnsureSecure(string connectionString, MongoClientSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        EnsureSecure(settings);
        EnsureSecureOptions(connectionString);
    }

    public static void EnsureSecure(MongoClientSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

#pragma warning disable CS0618 // Se comprueba explícitamente la validación del certificado.
        var verifySslCertificate = settings.VerifySslCertificate;
#pragma warning restore CS0618

        if (settings.UseTls != true ||
            settings.AllowInsecureTls ||
            !verifySslCertificate ||
            settings.SslSettings?.ServerCertificateValidationCallback is not null)
        {
            throw new MongoPromotionSecurityException(
                "La conexión de promoción debe usar TLS con validación de certificado.");
        }
    }

    private static void EnsureSecureOptions(string connectionString)
    {
        var queryStart = connectionString.IndexOf('?', StringComparison.Ordinal);
        if (queryStart < 0)
        {
            return;
        }

        var queryEnd = connectionString.IndexOf('#', queryStart);
        var query = queryEnd < 0
            ? connectionString[(queryStart + 1)..]
            : connectionString[(queryStart + 1)..queryEnd];

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            var rawKey = separator < 0 ? pair : pair[..separator];
            var rawValue = separator < 0 ? string.Empty : pair[(separator + 1)..];
            var key = Uri.UnescapeDataString(rawKey).Trim();
            var value = Uri.UnescapeDataString(rawValue).Trim();

            if (key.Equals("tls", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("ssl", StringComparison.OrdinalIgnoreCase))
            {
                if (IsFalse(value))
                {
                    ThrowInsecure();
                }

                continue;
            }

            if (InsecureTrueOptions.Contains(key, StringComparer.OrdinalIgnoreCase) &&
                IsTrue(value))
            {
                ThrowInsecure();
            }
        }
    }

    private static bool IsTrue(string value) =>
        value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("1", StringComparison.Ordinal);

    private static bool IsFalse(string value) =>
        value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("0", StringComparison.Ordinal);

    private static void ThrowInsecure() =>
        throw new MongoPromotionSecurityException(
            "La conexión de promoción debe usar TLS con validación de certificado.");
}

public sealed class MongoPromotionSecurityException : InvalidOperationException
{
    public MongoPromotionSecurityException(string message)
        : base(message)
    {
    }
}
