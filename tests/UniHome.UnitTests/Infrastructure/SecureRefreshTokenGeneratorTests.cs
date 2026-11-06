using UniHome.Infrastructure.Identity;

namespace UniHome.UnitTests.Infrastructure;

public sealed class SecureRefreshTokenGeneratorTests
{
    private readonly SecureRefreshTokenGenerator _generator = new();

    [Fact]
    public void Generate_DevuelveTokenConSesionYHashNoReversible()
    {
        var sessionId = Guid.NewGuid();

        var token = _generator.Generate(sessionId);

        Assert.StartsWith(sessionId.ToString("N") + ".", token.Value, StringComparison.Ordinal);
        Assert.NotEqual(token.Value, token.Hash);
        Assert.Equal(64, token.Hash.Length);
        Assert.Equal(token.Hash, _generator.Hash(token.Value));
        Assert.Equal(sessionId, _generator.TryGetSessionId(token.Value));
    }

    [Fact]
    public void Generate_DosTokensParaLaMismaSesion_SonDistintos()
    {
        var sessionId = Guid.NewGuid();

        var first = _generator.Generate(sessionId);
        var second = _generator.Generate(sessionId);

        Assert.NotEqual(first.Value, second.Value);
        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public void Generate_ConSesionVacia_EsRechazado()
    {
        Assert.Throws<ArgumentException>(() => _generator.Generate(Guid.Empty));
    }

    [Theory]
    [InlineData("")]
    [InlineData("sin-separador")]
    [InlineData("no-es-guid.secret012345678901234567890123456789012")]
    [InlineData("00000000000000000000000000000000.corto")]
    [InlineData("00000000000000000000000000000000.secret012345678901234567890123456789012!")]
    public void TryGetSessionId_RechazaFormatosInvalidos(string token)
    {
        Assert.Null(_generator.TryGetSessionId(token));
    }

    [Fact]
    public void Hash_EsDeterministaYRechazaVacios()
    {
        var token = _generator.Generate(Guid.NewGuid()).Value;

        Assert.Equal(_generator.Hash(token), _generator.Hash(token));
        Assert.Throws<ArgumentException>(() => _generator.Hash(" "));
    }
}
