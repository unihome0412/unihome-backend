using UniHome.Application.Common;
using UniHome.Application.Units.ZoneRules;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class ConfigureUnitZoneRulesServiceTests
{
    private static readonly DateTimeOffset Now = PublicationServiceTestData.Now;

    [Fact]
    public async Task Configure_DatosValidos_DebePersistirZonaYReglas()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var property = PublicationServiceTestData.CreateProperty(account.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, account.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var service = new ConfigureUnitZoneRulesService(units, eligibility);

        var result = await service.ConfigureAsync(
            account.Id,
            unit.Id,
            new ConfigureUnitZoneRulesCommand("Cerca del metro", "No fumar"));

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("Cerca del metro", result.Value!.ZoneInfo);
        Assert.Equal("No fumar", result.Value.Rules);
        Assert.Equal("Cerca del metro", unit.ZoneInfo);
    }

    [Fact]
    public async Task Configure_UnidadAjena_DebeDevolverForbidden()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var property = PublicationServiceTestData.CreateProperty(account.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, account.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var service = new ConfigureUnitZoneRulesService(units, eligibility);

        var result = await service.ConfigureAsync(
            Guid.NewGuid(),
            unit.Id,
            new ConfigureUnitZoneRulesCommand("Zona", "Reglas"));

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }
}
