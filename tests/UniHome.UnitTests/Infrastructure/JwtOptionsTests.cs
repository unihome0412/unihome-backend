using UniHome.Infrastructure.Configuration;

namespace UniHome.UnitTests.Infrastructure;

public sealed class JwtOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corta")]
    public void HasSecureSigningKey_EsFalsoConClaveAusenteOCorta(string? signingKey)
    {
        var options = new JwtOptions { SigningKey = signingKey };

        Assert.False(options.HasSecureSigningKey);
    }

    [Fact]
    public void HasSecureSigningKey_EsVerdaderoConLongitudMinima()
    {
        var options = new JwtOptions
        {
            SigningKey = new string('s', JwtOptions.MinSigningKeyLength)
        };

        Assert.True(options.HasSecureSigningKey);
    }

    [Fact]
    public void VigenciasPredeterminadas_CoincidenConM01()
    {
        var options = new JwtOptions();

        Assert.Equal(TimeSpan.FromMinutes(60), options.AccessTokenLifetime);
        Assert.Equal(TimeSpan.FromDays(30), options.RefreshTokenLifetime);
        Assert.Equal("UniHome", options.Issuer);
        Assert.Equal("UniHome.Mobile", options.Audience);
        Assert.False(options.EphemeralSigningKey);
    }
}
