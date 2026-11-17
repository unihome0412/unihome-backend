using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using UniHome.Application.Verification.Abstractions;
using UniHome.Domain.Verification;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Verification;

internal sealed class HmacDocumentUrlSigner : IDocumentUrlSigner
{
    private const string Version = "v1";

    private readonly byte[] _key;
    private readonly int _lifetimeSeconds;

    public HmacDocumentUrlSigner(IOptions<DocumentSecurityOptions> options)
    {
        var encryptionKey = options.Value.GetEncryptionKeyBytes();
        _key = options.Value.GetUrlSigningKeyBytes(encryptionKey);
        _lifetimeSeconds = options.Value.UrlLifetimeSeconds;
    }

    public DocumentAccessGrant Create(
        Guid expedienteId,
        TipoDocumentoVerificacion documentType,
        int version,
        DocumentUrlVariant variant,
        DateTimeOffset now)
    {
        if (expedienteId == Guid.Empty)
        {
            throw new ArgumentException("El expediente es obligatorio.", nameof(expedienteId));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);

        var expiresAt = now.AddSeconds(_lifetimeSeconds);
        var nonce = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(12));
        var payload = string.Join(
            '|',
            Version,
            expedienteId.ToString("N", CultureInfo.InvariantCulture),
            ((int)documentType).ToString(CultureInfo.InvariantCulture),
            version.ToString(CultureInfo.InvariantCulture),
            ((int)variant).ToString(CultureInfo.InvariantCulture),
            expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            nonce);

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = HMACSHA256.HashData(_key, payloadBytes);
        var token = WebEncoders.Base64UrlEncode(payloadBytes) +
                    "." +
                    WebEncoders.Base64UrlEncode(signature);

        return new DocumentAccessGrant(token, expiresAt);
    }

    public DocumentAccessToken? Validate(string token, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048)
        {
            return null;
        }

        var separator = token.IndexOf('.', StringComparison.Ordinal);

        if (separator <= 0 || separator == token.Length - 1 ||
            token.IndexOf('.', separator + 1) >= 0)
        {
            return null;
        }

        byte[] payloadBytes;
        byte[] signature;

        try
        {
            payloadBytes = WebEncoders.Base64UrlDecode(token[..separator]);
            signature = WebEncoders.Base64UrlDecode(token[(separator + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }

        var expected = HMACSHA256.HashData(_key, payloadBytes);

        if (!CryptographicOperations.FixedTimeEquals(expected, signature))
        {
            return null;
        }

        var parts = Encoding.UTF8.GetString(payloadBytes).Split('|');

        if (parts.Length != 7 || !string.Equals(parts[0], Version, StringComparison.Ordinal))
        {
            return null;
        }

        if (!Guid.TryParseExact(parts[1], "N", out var expedienteId) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var typeValue) ||
            !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var version) ||
            !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var variantValue) ||
            !long.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out var expiresUnix))
        {
            return null;
        }

        if (expedienteId == Guid.Empty ||
            version <= 0 ||
            !Enum.IsDefined((TipoDocumentoVerificacion)typeValue) ||
            !Enum.IsDefined((DocumentUrlVariant)variantValue))
        {
            return null;
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresUnix);

        return expiresAt < now
            ? null
            : new DocumentAccessToken(
                expedienteId,
                (TipoDocumentoVerificacion)typeValue,
                version,
                (DocumentUrlVariant)variantValue,
                expiresAt);
    }
}
