using UniHome.Infrastructure.Configuration;

namespace UniHome.UnitTests.Infrastructure;

public sealed class VerificationCodeOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corto")]
    public void HasStrongSecret_EsFalsoConSecretoAusenteOCorto(string? secret)
    {
        var options = new VerificationCodeOptions { HmacSecret = secret };

        Assert.False(options.HasStrongSecret);
    }

    [Fact]
    public void HasStrongSecret_EsVerdaderoConLongitudMinima()
    {
        var options = new VerificationCodeOptions
        {
            HmacSecret = new string('s', VerificationCodeOptions.MinSecretLength)
        };

        Assert.True(options.HasStrongSecret);
    }
}
