using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Genera JWT con firma, issuer, audience y vigencia controlables para las pruebas
/// de seguridad del middleware de autenticación.
/// </summary>
public static class JwtTestTokenFactory
{
    private static readonly JsonWebTokenHandler Handler = new();

    public static string Create(
        Guid accountId,
        Guid sessionId,
        string signingKey = UniHomeApiFactory.TestSigningKey,
        string issuer = UniHomeApiFactory.TestIssuer,
        string audience = UniHomeApiFactory.TestAudience,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? notBefore = null,
        string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var now = DateTimeOffset.UtcNow;
        var nbf = notBefore ?? now.AddMinutes(-1);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", accountId.ToString("N")),
                new Claim("sid", sessionId.ToString("N")),
                new Claim("jti", Guid.NewGuid().ToString("N"))
            ]),
            NotBefore = nbf.UtcDateTime,
            IssuedAt = nbf.UtcDateTime,
            Expires = (expiresAt ?? now.AddMinutes(60)).UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                algorithm),
            TokenType = "JWT"
        };

        return Handler.CreateToken(descriptor);
    }
}
