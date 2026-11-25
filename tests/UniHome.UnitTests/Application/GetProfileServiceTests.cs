using UniHome.Application.Profiles.GetProfile;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Properties;
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
        var service = new GetProfileService(
            accounts, new FakeUnitRepository(), new FakeOccupancyLinkRepository(), new FakeUniversityRepository(), new FixedClock(Now));

        var profile = await service.GetAsync(account.Id);

        Assert.NotNull(profile);
        Assert.Equal("Ana López", profile.Account.FullName);
        Assert.Equal("ana@uni.edu", profile.Account.Email.Value);
        Assert.Equal(AccountLabel.UsuarioUniHome, profile.Label);
        Assert.False(profile.IsLinkedToUnit);
        Assert.True(profile.CanPublish);
    }

    [Fact]
    public async Task Get_ConVinculoActivo_DebeMarcarVinculadoAUnidad()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create("inquilino@uni.edu");
        await accounts.AddAsync(account);
        var occupancyLinks = new FakeOccupancyLinkRepository();
        occupancyLinks.Items.Add(OccupancyLink.CreateActive(account.Id, Guid.NewGuid(), Now));
        var service = new GetProfileService(
            accounts, new FakeUnitRepository(), occupancyLinks, new FakeUniversityRepository(), new FixedClock(Now));

        var profile = await service.GetAsync(account.Id);

        Assert.NotNull(profile);
        Assert.True(profile.IsLinkedToUnit);
    }

    [Fact]
    public async Task Get_MenorDeEdad_NoPuedePublicar()
    {
        var accounts = new InMemoryAccountRepository();
        var account = AccountTestData.Create(
            "menor@uni.edu",
            birthDate: new DateOnly(2010, 1, 1));
        await accounts.AddAsync(account);
        var service = new GetProfileService(accounts, new FakeUnitRepository(), new FakeOccupancyLinkRepository(), new FakeUniversityRepository(), new FixedClock(Now));

        var profile = await service.GetAsync(account.Id);

        Assert.NotNull(profile);
        Assert.False(profile.CanPublish);
    }

    [Fact]
    public async Task Get_CuentaInexistente_DebeDevolverNull()
    {
        var service = new GetProfileService(
            new InMemoryAccountRepository(),
            new FakeUnitRepository(),
            new FakeOccupancyLinkRepository(),
            new FakeUniversityRepository(), new FixedClock(Now));

        Assert.Null(await service.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Get_PublicacionEnRevision_DebeEtiquetarYNoPermitirNuevaPublicacion()
    {
        var accounts = new InMemoryAccountRepository();
        var units = new FakeUnitRepository();
        var account = AccountTestData.Create("revision@uni.edu");
        await accounts.AddAsync(account);
        units.Items.Add(ExplorationTestData.CreateUnit(
            account.Id,
            Guid.NewGuid(),
            3000,
            state: PublicationState.EnRevision));
        var service = new GetProfileService(accounts, units, new FakeOccupancyLinkRepository(), new FakeUniversityRepository(), new FixedClock(Now));

        var profile = await service.GetAsync(account.Id);

        Assert.NotNull(profile);
        Assert.Equal(AccountLabel.PublicacionEnRevision, profile.Label);
        Assert.False(profile.CanPublish);
    }

    [Fact]
    public async Task Get_PublicacionRechazada_DebeExponerEstadoYNoPermitirNuevaPublicacion()
    {
        var accounts = new InMemoryAccountRepository();
        var units = new FakeUnitRepository();
        var account = AccountTestData.Create("rechazada@uni.edu");
        await accounts.AddAsync(account);
        units.Items.Add(ExplorationTestData.CreateUnit(
            account.Id,
            Guid.NewGuid(),
            3000,
            state: PublicationState.Rechazada));
        var service = new GetProfileService(accounts, units, new FakeOccupancyLinkRepository(), new FakeUniversityRepository(), new FixedClock(Now));

        var profile = await service.GetAsync(account.Id);

        Assert.NotNull(profile);
        Assert.Equal(AccountLabel.UsuarioUniHome, profile.Label);
        Assert.Equal(PublicationState.Rechazada, profile.FirstPublicationState);
        Assert.False(profile.CanPublish);
    }

    [Fact]
    public async Task Get_PublicacionAprobada_DebeEtiquetarConEspaciosPublicados()
    {
        var accounts = new InMemoryAccountRepository();
        var units = new FakeUnitRepository();
        var account = AccountTestData.Create("aprobada@uni.edu");
        await accounts.AddAsync(account);
        units.Items.Add(ExplorationTestData.CreateUnit(
            account.Id,
            Guid.NewGuid(),
            3000,
            state: PublicationState.Aprobada));
        var service = new GetProfileService(accounts, units, new FakeOccupancyLinkRepository(), new FakeUniversityRepository(), new FixedClock(Now));

        var profile = await service.GetAsync(account.Id);

        Assert.NotNull(profile);
        Assert.Equal(AccountLabel.ConEspaciosPublicados, profile.Label);
        Assert.False(profile.CanPublish);
    }
}
