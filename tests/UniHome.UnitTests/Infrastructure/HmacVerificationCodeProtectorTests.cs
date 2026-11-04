using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Identity;

namespace UniHome.UnitTests.Infrastructure;

public sealed class HmacVerificationCodeProtectorTests
{
    private static readonly string Secret = new('s', VerificationCodeOptions.MinSecretLength);

    [Fact]
    public void Protect_EsDeterministaYNoContieneElCodigo()
    {
        var protector = CreateProtector();

        var first = protector.Protect("123456");
        var second = protector.Protect("123456");

        Assert.Equal(first, second);
        Assert.DoesNotContain("123456", first, StringComparison.Ordinal);
        Assert.NotEqual(protector.Protect("654321"), first);
    }

    [Fact]
    public void Matches_AceptaElCodigoCorrectoYRechazaElIncorrecto()
    {
        var protector = CreateProtector();
        var protectedCode = protector.Protect("123456");

        Assert.True(protector.Matches("123456", protectedCode));
        Assert.False(protector.Matches("654321", protectedCode));
        Assert.False(protector.Matches("123456", "no-es-base64"));
        Assert.False(protector.Matches("123456", string.Empty));
    }

    [Fact]
    public void Protect_ConSecretosDistintos_ProduceValoresDistintos()
    {
        var first = CreateProtector(new string('a', VerificationCodeOptions.MinSecretLength));
        var second = CreateProtector(new string('b', VerificationCodeOptions.MinSecretLength));

        Assert.NotEqual(first.Protect("123456"), second.Protect("123456"));
    }

    private static HmacVerificationCodeProtector CreateProtector(string? secret = null) =>
        new(
            Options.Create(new VerificationCodeOptions { HmacSecret = secret ?? Secret }),
            NullLogger<HmacVerificationCodeProtector>.Instance);
}
