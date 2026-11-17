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

    [Fact]
    public void TryBuildResetUrl_ConEsquemaAndroidConfigurado_ConstruyeDeepLink()
    {
        var options = new PasswordRecoveryOptions
        {
            ResetLinkBaseUrl = "unihome://recuperacion",
            AndroidAppScheme = "unihome"
        };

        Assert.True(options.IsValid());
        Assert.True(options.TryBuildResetUrl("abc.def_-", out var url));
        Assert.Equal("unihome://recuperacion?token=abc.def_-", url);
    }

    [Theory]
    [InlineData("unihome://recuperacion", null)]
    [InlineData("unihome://recuperacion", "otraapp")]
    [InlineData("javascript://recuperacion", "javascript")]
    [InlineData("unihome://recuperacion?token=previo", "unihome")]
    [InlineData("unihome://recuperacion#fragmento", "unihome")]
    [InlineData("unihome://", "unihome")]
    public void EsquemaAndroid_RechazaConfiguracionesNoAutorizadas(
        string baseUrl,
        string? configuredScheme)
    {
        var options = new PasswordRecoveryOptions
        {
            ResetLinkBaseUrl = baseUrl,
            AndroidAppScheme = configuredScheme
        };

        Assert.False(options.IsValid());
        Assert.False(options.TryBuildResetUrl("token", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1unihome")]
    [InlineData("uni home")]
    [InlineData("https")]
    [InlineData("unihome/recuperacion")]
    public void AndroidAppScheme_Invalido_NoEsValido(string scheme)
    {
        var options = new PasswordRecoveryOptions
        {
            ResetLinkBaseUrl = "unihome://recuperacion",
            AndroidAppScheme = scheme
        };

        Assert.False(options.IsValid());
    }
}
