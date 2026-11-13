using UniHome.Application.Profiles.GetProfile;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class GetProfileServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Get_DebeDevolverDatosPropiosEIndicadoresDeNavegacion()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("ana@uni.edu");
        await accounts.AddAsync(account);
        var service = new GetProfileService(accounts, new FixedClock(Now));

        var profile = await service.GetAsync(account.Id);

        Assert.NotNull(profile);
        Assert.Equal("Ana López", profile.Account.FullName);
        Assert.Equal("ana@uni.edu", profile.Account.Email.Value);
        Assert.Equal(AccountLabel.UsuarioUniHome, profile.Label);
        Assert.False(profile.IsLinkedToUnit);
        Assert.True(profile.CanPublish);
    }

    [Fact]
    public async Task Get_MenorDeEdad_NoPuedePublicar()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create(
            "menor@uni.edu",
            birthDate: new DateOnly(2010, 1, 1));
        await accounts.AddAsync(account);
        var service = new GetProfileService(accounts, new FixedClock(Now));

        var profile = await service.GetAsync(account.Id);

        Assert.NotNull(profile);
        Assert.False(profile.CanPublish);
    }

    [Fact]
    public async Task Get_CuentaInexistente_DebeDevolverNull()
    {
        var service = new GetProfileService(
            new InMemoryAccountRepository(),
            new FixedClock(Now));

        Assert.Null(await service.GetAsync(Guid.NewGuid()));
    }
}
