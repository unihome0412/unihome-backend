using UniHome.Infrastructure.Configuration;

namespace UniHome.UnitTests.Infrastructure;

public sealed class PasswordRecoveryOptionsTests
{
    [Fact]
    public void TryBuildResetUrl_ConHttpsValida_ConstruyeUrlConTokenEscapado()
    {
        var options = new PasswordRecoveryOptions
        {
            ResetLinkBaseUrl = " https://app.unihome.test/recuperacion/ "
        };

        var built = options.TryBuildResetUrl("abc.def_-", out var url);

        Assert.True(built);
        Assert.Equal("https://app.unihome.test/recuperacion?token=abc.def_-", url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("app.unihome.test/recuperacion")]
    [InlineData("http://app.unihome.test/recuperacion")]
    [InlineData("https://user:pass@app.unihome.test/recuperacion")]
    [InlineData("https://app.unihome.test/recuperacion?x=1")]
    [InlineData("https://app.unihome.test/recuperacion#fragmento")]
    public void HasValidResetLinkBaseUrl_RechazaConfiguracionesInseguras(string? value)
    {
        var options = new PasswordRecoveryOptions { ResetLinkBaseUrl = value };

        Assert.False(options.HasValidResetLinkBaseUrl);
    }

    [Fact]
    public void HasValidResetLinkBaseUrl_AdmiteHttpLoopbackSoloParaDesarrollo()
    {
        var options = new PasswordRecoveryOptions
        {
            ResetLinkBaseUrl = "http://localhost:3000/recuperacion"
        };

        Assert.True(options.HasValidResetLinkBaseUrl);
        Assert.True(options.TryBuildResetUrl("token", out var url));
        Assert.StartsWith("http://localhost:3000/recuperacion?token=", url, StringComparison.Ordinal);
    }
}
