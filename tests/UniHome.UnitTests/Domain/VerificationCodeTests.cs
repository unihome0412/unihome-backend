using System.Text;
using UniHome.Domain.Accounts;

namespace UniHome.UnitTests.Domain;

public sealed class VerificationCodeTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validate_AceptaCodigoCorrectoAntesDeDiezMinutos()
    {
        var code = VerificationCode.Issue(Hash("123456"), IssuedAt);

        var result = code.Validate(Hash("123456"), IssuedAt.AddMinutes(9));

        Assert.Equal(CodeValidationResult.Valid, result);
        Assert.False(code.IsConsumed);
    }

    [Fact]
    public void Validate_RechazaCodigoVencidoALosDiezMinutos()
    {
        var code = VerificationCode.Issue(Hash("123456"), IssuedAt);

        var result = code.Validate(Hash("123456"), IssuedAt.AddMinutes(10));

        Assert.Equal(CodeValidationResult.Expired, result);
    }

    [Fact]
    public void Validate_RechazaCodigoIncorrecto()
    {
        var code = VerificationCode.Issue(Hash("123456"), IssuedAt);

        var result = code.Validate(Hash("654321"), IssuedAt.AddMinutes(1));

        Assert.Equal(CodeValidationResult.Incorrect, result);
    }

    [Fact]
    public void Validate_RechazaCodigoYaConsumido()
    {
        var code = VerificationCode.Issue(Hash("123456"), IssuedAt);
        code.Consume(IssuedAt.AddMinutes(1));

        var result = code.Validate(Hash("123456"), IssuedAt.AddMinutes(2));

        Assert.Equal(CodeValidationResult.AlreadyUsed, result);
    }

    [Fact]
    public void Consume_NoPermiteDosUsos()
    {
        var code = VerificationCode.Issue(Hash("123456"), IssuedAt);
        code.Consume(IssuedAt.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() => code.Consume(IssuedAt.AddMinutes(2)));
    }

    [Fact]
    public void Restore_ConservaVigenciaYConsumo()
    {
        var code = VerificationCode.Restore(
            Hash("123456"),
            IssuedAt,
            IssuedAt.AddMinutes(10),
            IssuedAt.AddMinutes(2));

        Assert.True(code.IsConsumed);
        Assert.Equal(IssuedAt.AddMinutes(10), code.ExpiresAt);
        Assert.Equal(CodeValidationResult.AlreadyUsed, code.Validate(Hash("123456"), IssuedAt.AddMinutes(3)));
    }

    private static string Hash(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"hash:{value}"));
}
