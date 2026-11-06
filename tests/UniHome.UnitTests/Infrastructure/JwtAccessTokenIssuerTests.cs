using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Identity;

namespace UniHome.UnitTests.Infrastructure;

public sealed class JwtAccessTokenIssuerTests
{
    private const string SigningKey =
        "unihome-unit-test-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz";

    private static readonly DateTimeOffset Now =
        DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    [Fact]
    public async Task Issue_GeneraTokenValidoConSujetoSesionYVigencia()
    {
        var issuer = CreateIssuer();
        var accountId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var accessToken = issuer.Issue(accountId, sessionId, Now);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            accessToken.Value,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "UniHome.Test",
                ValidateAudience = true,
                ValidAudience = "UniHome.Mobile.Test",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            });

        Assert.True(result.IsValid, result.Exception?.Message);

        var token = new JsonWebTokenHandler().ReadJsonWebToken(accessToken.Value);
        Assert.Equal(accountId.ToString("N"), token.Subject);
        Assert.Contains(
            token.Claims,
            claim => claim.Type == "sid" && claim.Value == sessionId.ToString("N"));
        Assert.DoesNotContain(token.Claims, claim => claim.Type is "role" or "roles");
        Assert.Equal(Now.AddMinutes(60), accessToken.ExpiresAt);
        Assert.Equal(accessToken.ExpiresAt.UtcDateTime, token.ValidTo);
    }

    [Fact]
    public void Issue_ConClaveCorta_EsRechazado()
    {
        var issuer = new JwtAccessTokenIssuer(
            Options.Create(new JwtOptions
            {
                Issuer = "UniHome.Test",
                Audience = "UniHome.Mobile.Test",
                SigningKey = "corta"
            }));

        Assert.Throws<InvalidOperationException>(
            () => issuer.Issue(Guid.NewGuid(), Guid.NewGuid(), Now));
    }

    private static JwtAccessTokenIssuer CreateIssuer() =>
        new(
            Options.Create(new JwtOptions
            {
                Issuer = "UniHome.Test",
                Audience = "UniHome.Mobile.Test",
                SigningKey = SigningKey,
                AccessTokenMinutes = 60
            }));
}
