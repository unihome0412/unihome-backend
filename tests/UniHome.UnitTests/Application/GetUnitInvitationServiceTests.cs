using UniHome.Application.Common;
using UniHome.Application.OccupancyLinks.GetInvitation;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class GetUnitInvitationServiceTests
{
    private static readonly DateTimeOffset Now = PublicationServiceTestData.Now;

    [Fact]
    public async Task GetAsync_SinInvitacionPrevia_GeneraUnaNueva()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var property = PublicationServiceTestData.CreateProperty(account.Id, Guid.NewGuid());
        var properties = new FakePropertyRepository();
        properties.Items.Add(property);
        var units = new FakeUnitRepository();
        var unit = PublicationServiceTestData.UnitFor(property.Id, account.Id, property.UniversityId);
        units.Items.Add(unit);
        var invitations = new FakeUnitInvitationRepository();
        var service = new GetUnitInvitationService(
            units,
            invitations,
            new SequentialUnitInvitationCodeGenerator("ABCD2345"),
            eligibility,
            new FixedClock(Now));

        var result = await service.GetAsync(account.Id, unit.Id);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("ABCD2345", result.Value!.Codigo);
        Assert.Equal(Now.AddDays(7), result.Value.VigenciaHasta);
        Assert.Single(invitations.Items);
    }

    [Fact]
    public async Task GetAsync_ConInvitacionVigente_DevuelveLaMismaSinGenerarOtra()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var property = PublicationServiceTestData.CreateProperty(account.Id, Guid.NewGuid());
        var properties = new FakePropertyRepository();
        properties.Items.Add(property);
        var units = new FakeUnitRepository();
        var unit = PublicationServiceTestData.UnitFor(property.Id, account.Id, property.UniversityId);
        units.Items.Add(unit);
        var invitations = new FakeUnitInvitationRepository();
        var generator = new SequentialUnitInvitationCodeGenerator("FIRST001", "SECOND02");
        var service = new GetUnitInvitationService(
            units, invitations, generator, eligibility, new FixedClock(Now));

        var first = await service.GetAsync(account.Id, unit.Id);
        var second = await service.GetAsync(account.Id, unit.Id);

        Assert.Equal("FIRST001", first.Value!.Codigo);
        Assert.Equal("FIRST001", second.Value!.Codigo);
        Assert.Single(invitations.Items);
    }

    [Fact]
    public async Task GetAsync_ConInvitacionVencida_GeneraUnaNuevaYConservaLaAnterior()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var property = PublicationServiceTestData.CreateProperty(account.Id, Guid.NewGuid());
        var properties = new FakePropertyRepository();
        properties.Items.Add(property);
        var units = new FakeUnitRepository();
        var unit = PublicationServiceTestData.UnitFor(property.Id, account.Id, property.UniversityId);
        units.Items.Add(unit);
        var invitations = new FakeUnitInvitationRepository();
        var generator = new SequentialUnitInvitationCodeGenerator("FIRST001", "SECOND02");
        var clock = new MutableClock(Now);
        var service = new GetUnitInvitationService(units, invitations, generator, eligibility, clock);

        var first = await service.GetAsync(account.Id, unit.Id);
        clock.UtcNow = Now.AddDays(8);
        var second = await service.GetAsync(account.Id, unit.Id);

        Assert.Equal("FIRST001", first.Value!.Codigo);
        Assert.Equal("SECOND02", second.Value!.Codigo);
        Assert.Equal(2, invitations.Items.Count);
    }

    [Fact]
    public async Task GetAsync_UnidadAjena_DebeDevolverForbidden()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var property = PublicationServiceTestData.CreateProperty(Guid.NewGuid(), Guid.NewGuid());
        var properties = new FakePropertyRepository();
        properties.Items.Add(property);
        var units = new FakeUnitRepository();
        var unit = PublicationServiceTestData.UnitFor(
            property.Id, property.OwnerAccountId, property.UniversityId);
        units.Items.Add(unit);
        var service = new GetUnitInvitationService(
            units,
            new FakeUnitInvitationRepository(),
            new SequentialUnitInvitationCodeGenerator(),
            eligibility,
            new FixedClock(Now));

        var result = await service.GetAsync(account.Id, unit.Id);

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task GetAsync_UnidadInexistente_DebeDevolverNotFound()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var service = new GetUnitInvitationService(
            new FakeUnitRepository(),
            new FakeUnitInvitationRepository(),
            new SequentialUnitInvitationCodeGenerator(),
            eligibility,
            new FixedClock(Now));

        var result = await service.GetAsync(account.Id, Guid.NewGuid());

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }
}
