using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using UniHome.Application.Identity.Abstractions;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Identity;

/// <summary>
/// Emisor HS256. El sujeto es el identificador interno de la cuenta y la sesión
/// viaja en el claim sid; no se incluyen roles ni datos personales.
/// </summary>
internal sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    private static readonly JsonWebTokenHandler Handler = new();

    private readonly IOptions<JwtOptions> _options;

    public JwtAccessTokenIssuer(IOptions<JwtOptions> options)
    {
        _options = options;
    }

    public IssuedAccessToken Issue(Guid accountId, Guid sessionId, DateTimeOffset now)
    {
        var options = _options.Value;

        if (!options.HasSecureSigningKey)
        {
            throw new InvalidOperationException("La clave de firma JWT no está configurada de forma segura.");
        }

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds());
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(
            now.Add(options.AccessTokenLifetime).ToUnixTimeSeconds());

        var identity = new ClaimsIdentity(
        [
            new Claim("sub", accountId.ToString("N")),
            new Claim("sid", sessionId.ToString("N")),
            new Claim("jti", Guid.NewGuid().ToString("N"))
        ]);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Subject = identity,
            NotBefore = issuedAt.UtcDateTime,
            IssuedAt = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey!)),
                SecurityAlgorithms.HmacSha256),
            TokenType = "JWT"
        };

        return new IssuedAccessToken(Handler.CreateToken(descriptor), expiresAt);
    }
}
