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
        string algorithm = SecurityAlgorithms.HmacSha256,
        bool includeAdminClaim = false)
    {
        var now = DateTimeOffset.UtcNow;
        var nbf = notBefore ?? now.AddMinutes(-1);
        var claims = new List<Claim>
        {
            new("sub", accountId.ToString("N")),
            new("sid", sessionId.ToString("N")),
            new("jti", Guid.NewGuid().ToString("N"))
        };

        if (includeAdminClaim)
        {
            claims.Add(new Claim("admin", "true"));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
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
