using UniHome.Domain.Accounts;

namespace UniHome.UnitTests.Domain;

public sealed class PasswordResetTokenTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid AccountId = Guid.NewGuid();
    private const string Hash = "hash-del-token";

    [Fact]
    public void Validate_AceptaTokenAntesDeLosTreintaMinutos()
    {
        var token = PasswordResetToken.Issue(
            Guid.NewGuid(),
            AccountId,
            Hash,
            IssuedAt);

        Assert.Equal(
            PasswordResetValidation.Valid,
            token.Validate(Hash, IssuedAt.AddMinutes(29)));
        Assert.False(token.IsConsumed);
    }

    [Fact]
    public void Validate_RechazaTokenVencidoExactamenteALosTreintaMinutos()
    {
        var token = PasswordResetToken.Issue(
            Guid.NewGuid(),
            AccountId,
            Hash,
            IssuedAt);

        Assert.Equal(
            PasswordResetValidation.Expired,
            token.Validate(Hash, IssuedAt.AddMinutes(30)));
    }

    [Fact]
    public void Validate_RechazaHashIncorrecto()
    {
        var token = PasswordResetToken.Issue(
            Guid.NewGuid(),
            AccountId,
            Hash,
            IssuedAt);

        Assert.Equal(
            PasswordResetValidation.Invalid,
            token.Validate("otro-hash", IssuedAt.AddMinutes(1)));
    }

    [Fact]
    public void Validate_RechazaTokenYaConsumido()
    {
        var token = PasswordResetToken.Issue(
            Guid.NewGuid(),
            AccountId,
            Hash,
            IssuedAt);
        token.Consume(IssuedAt.AddMinutes(1));

        Assert.Equal(
            PasswordResetValidation.AlreadyUsed,
            token.Validate(Hash, IssuedAt.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => token.Consume(IssuedAt.AddMinutes(3)));
    }

    [Fact]
    public void Validate_TrasCincoIntentos_SeTrataComoVencido()
    {
        var token = PasswordResetToken.Issue(
            Guid.NewGuid(),
            AccountId,
            Hash,
            IssuedAt);

        for (var attempt = 0; attempt < PasswordResetToken.MaxAttempts; attempt++)
        {
            token.RegisterFailedAttempt();
        }

        Assert.True(token.HasExceededAttempts);
        Assert.Equal(
            PasswordResetValidation.Expired,
            token.Validate(Hash, IssuedAt.AddMinutes(1)));
    }

    [Fact]
    public void Restore_ConservaVigenciaConsumoEIntentos()
    {
        var token = PasswordResetToken.Restore(
            Guid.NewGuid(),
            AccountId,
            Hash,
            IssuedAt,
            IssuedAt.AddMinutes(30),
            IssuedAt.AddMinutes(2),
            failedAttempts: 3);

        Assert.True(token.IsConsumed);
        Assert.Equal(3, token.FailedAttempts);
        Assert.Equal(IssuedAt.AddMinutes(30), token.ExpiresAt);
    }
}
