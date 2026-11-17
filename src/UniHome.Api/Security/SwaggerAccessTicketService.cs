using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UniHome.Api.Configuration;
using UniHome.Application.Identity.Abstractions;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Api.Security;

/// <summary>
/// Emite y valida tickets opacos para la cookie de documentación. El ticket no
/// es un JWT y no sirve para llamar a la API: solo identifica al sujeto mientras
/// el handler vuelve a comprobar estado de cuenta y la policy comprueba la
/// allowlist administrativa. La clave se deriva de <c>Jwt:SigningKey</c> con un
/// separador de dominio, de modo que rotar la clave JWT invalida las sesiones.
/// </summary>
public sealed class SwaggerAccessTicketService
{
    private const string DomainSeparator = "UniHome.SwaggerAccess.v1:";
    private const int MaxTicketLength = 2048;

    private readonly IOptions<JwtOptions> _jwtOptions;
    private readonly IOptions<SwaggerAccessOptions> _accessOptions;
    private readonly ISystemClock _clock;

    public SwaggerAccessTicketService(
        IOptions<JwtOptions> jwtOptions,
        IOptions<SwaggerAccessOptions> accessOptions,
        ISystemClock clock)
    {
        _jwtOptions = jwtOptions;
        _accessOptions = accessOptions;
        _clock = clock;
    }

    public string Issue(Guid accountId)
    {
        var now = _clock.UtcNow.ToUnixTimeSeconds();
        var payload = new Payload(
            accountId.ToString("N", CultureInfo.InvariantCulture),
            now,
            now + (long)_accessOptions.Value.Lifetime.TotalSeconds,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
        var payloadJson = JsonSerializer.SerializeToUtf8Bytes(payload);
        var encodedPayload = Base64UrlEncode(payloadJson);
        var signature = Sign(encodedPayload);

        return $"{encodedPayload}.{Base64UrlEncode(signature)}";
    }

    public bool TryValidate(string? ticket, out Guid accountId)
    {
        accountId = default;

        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > MaxTicketLength)
        {
            return false;
        }

        var separator = ticket.IndexOf('.', StringComparison.Ordinal);

        if (separator <= 0 || separator == ticket.Length - 1)
        {
            return false;
        }

        var encodedPayload = ticket[..separator];
        var encodedSignature = ticket[(separator + 1)..];

        byte[] providedSignature;

        try
        {
            providedSignature = Base64UrlDecode(encodedSignature);
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedSignature = Sign(encodedPayload);

        if (expectedSignature.Length == 0 ||
            !CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
        {
            return false;
        }

        Payload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<Payload>(Base64UrlDecode(encodedPayload));
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            return false;
        }

        if (payload is null ||
            !Guid.TryParseExact(payload.Subject, "N", out accountId) ||
            payload.ExpiresAtUnixSeconds <= _clock.UtcNow.ToUnixTimeSeconds() ||
            payload.IssuedAtUnixSeconds > _clock.UtcNow.AddMinutes(1).ToUnixTimeSeconds())
        {
            accountId = default;
            return false;
        }

        return true;
    }

    private byte[] Sign(string encodedPayload)
    {
        var signingKey = _jwtOptions.Value.SigningKey;

        if (string.IsNullOrWhiteSpace(signingKey))
        {
            return [];
        }

        var derivedKey = SHA256.HashData(
            Encoding.UTF8.GetBytes(DomainSeparator + signingKey));
        using var hmac = new HMACSHA256(derivedKey);
        return hmac.ComputeHash(Encoding.ASCII.GetBytes(encodedPayload));
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var normalized = value
            .Replace('-', '+')
            .Replace('_', '/');
        var padding = (4 - normalized.Length % 4) % 4;
        return Convert.FromBase64String(normalized.PadRight(normalized.Length + padding, '='));
    }

    private sealed record Payload(
        [property: System.Text.Json.Serialization.JsonPropertyName("sub")] string Subject,
        [property: System.Text.Json.Serialization.JsonPropertyName("iat")] long IssuedAtUnixSeconds,
        [property: System.Text.Json.Serialization.JsonPropertyName("exp")] long ExpiresAtUnixSeconds,
        [property: System.Text.Json.Serialization.JsonPropertyName("nonce")] string Nonce);
}
