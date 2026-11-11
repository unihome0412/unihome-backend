using UniHome.Infrastructure.Identity;

namespace UniHome.UnitTests.Infrastructure;

public sealed class SecurePasswordResetTokenGeneratorTests
{
    private readonly SecurePasswordResetTokenGenerator _generator = new();

    [Fact]
    public void Generate_DebeCrearTokenDeAltaEntropiaConIdRecuperable()
    {
        var generated = _generator.Generate();

        Assert.NotEqual(Guid.Empty, generated.Id);
        Assert.StartsWith(generated.Id.ToString("N") + ".", generated.Value, StringComparison.Ordinal);
        Assert.Equal(76, generated.Value.Length);
        Assert.Equal(64, generated.Hash.Length);
        Assert.Equal(generated.Hash, _generator.Hash(generated.Value));
        Assert.Equal(generated.Id, _generator.TryGetTokenId(generated.Value));
    }

    [Fact]
    public void Generate_DebeProducirTokensDistintos()
    {
        var first = _generator.Generate();
        var second = _generator.Generate();

        Assert.NotEqual(first.Value, second.Value);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("token-sin-id")]
    [InlineData("00112233445566778899aabbccddeeff.")]
    [InlineData("00112233445566778899aabbccddeeff.corto")]
    [InlineData("00112233445566778899aabbccddeeff." + "a!aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void TryGetTokenId_RechazaFormatosInvalidos(string? token)
    {
        Assert.Null(_generator.TryGetTokenId(token!));
    }

    [Fact]
    public void Hash_EsDeterministaYNoEsElToken()
    {
        var generated = _generator.Generate();

        var first = _generator.Hash(generated.Value);
        var second = _generator.Hash(generated.Value);

        Assert.Equal(first, second);
        Assert.NotEqual(generated.Value, first);
        Assert.DoesNotContain(generated.Value, first, StringComparison.Ordinal);
    }
}
