using UniHome.Domain.Publications;

namespace UniHome.UnitTests.Domain;

public sealed class TermsAcceptanceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ConVersionValida_DebeRegistrarFechaYCuenta()
    {
        var accountId = Guid.NewGuid();

        var acceptance = TermsAcceptance.Create(accountId, "  2026-11  ", Now);

        Assert.Equal(accountId, acceptance.AccountId);
        Assert.Equal("2026-11", acceptance.Version);
        Assert.Equal(Now, acceptance.AcceptedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_SinVersion_DebeRechazarse(string? version)
    {
        Assert.Throws<ArgumentException>(() =>
            TermsAcceptance.Create(Guid.NewGuid(), version, Now));
    }

    [Fact]
    public void Create_ConVersionExcesiva_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() => TermsAcceptance.Create(
            Guid.NewGuid(),
            new string('a', TermsAcceptance.MaxVersionLength + 1),
            Now));
    }

    [Fact]
    public void Restore_ConservaIdentidadYVersion()
    {
        var id = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var acceptance = TermsAcceptance.Restore(id, accountId, "2026-11", Now);

        Assert.Equal(id, acceptance.Id);
        Assert.Equal(accountId, acceptance.AccountId);
    }
}
