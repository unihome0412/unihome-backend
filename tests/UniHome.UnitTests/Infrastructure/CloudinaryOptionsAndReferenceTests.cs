using UniHome.Application.Units.Photos;
using UniHome.Infrastructure.Configuration;

namespace UniHome.UnitTests.Infrastructure;

public sealed class CloudinaryOptionsAndReferenceTests
{
    [Fact]
    public void Options_SoloDebeEstarConfiguradaConLasTresCredenciales()
    {
        var options = new CloudinaryOptions
        {
            CloudName = "cloud",
            ApiKey = "key",
            ApiSecret = "secret"
        };

        Assert.True(options.IsConfigured);
        Assert.Equal("unihome/unidades", options.EffectiveFolder);
        Assert.Equal(30, options.EffectiveTimeoutSeconds);

        options.ApiSecret = " ";
        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void Options_DebeAcotarTimeoutYFolder()
    {
        var options = new CloudinaryOptions
        {
            CloudName = "cloud",
            ApiKey = "key",
            ApiSecret = "secret",
            Folder = " /fotos/ ",
            TimeoutSeconds = 999
        };

        Assert.Equal("fotos", options.EffectiveFolder);
        Assert.Equal(CloudinaryOptions.DefaultTimeoutSeconds, options.EffectiveTimeoutSeconds);
        Assert.Equal(TimeSpan.FromSeconds(CloudinaryOptions.DefaultTimeoutSeconds), options.Timeout);
    }

    [Fact]
    public void PhotoReference_DebeSerRoundTripYNoExponerLaClave()
    {
        const string storageKey = "unihome/unidades/abc-123";

        var reference = PhotoReference.Encode(storageKey);

        Assert.DoesNotContain("unihome", reference, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/", reference, StringComparison.Ordinal);
        Assert.True(PhotoReference.TryDecode(reference, out var decoded));
        Assert.Equal(storageKey, decoded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("%%%")]
    [InlineData("***")]
    public void PhotoReference_Invalida_DebeRechazarse(string? reference)
    {
        Assert.False(PhotoReference.TryDecode(reference, out _));
    }
}
