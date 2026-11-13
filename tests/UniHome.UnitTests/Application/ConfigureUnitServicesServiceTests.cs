using UniHome.Application.Common;
using UniHome.Application.Profiles;
using UniHome.Application.Units.Services;
using UniHome.Domain.Accounts;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class ConfigureUnitServicesServiceTests
{
    private static readonly DateTimeOffset Now = PublicationServiceTestData.Now;

    [Fact]
    public async Task Configure_AparteSinMonto_DebeDevolverValidacion()
    {
        var context = CreateContext();
        var service = new ConfigureUnitServicesService(
            context.Units,
            context.Eligibility);

        var result = await service.ConfigureAsync(
            context.Account.Id,
            context.Unit.Id,
            new ConfigureUnitServicesCommand(
            [
                new ServiceChargeInput(ServiceName.Internet, ServiceMode.Aparte, null)
            ]));

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.Contains("servicios", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Configure_ConServiciosValidos_DebeReemplazar()
    {
        var context = CreateContext();
        var service = new ConfigureUnitServicesService(context.Units, context.Eligibility);

        var result = await service.ConfigureAsync(
            context.Account.Id,
            context.Unit.Id,
            new ConfigureUnitServicesCommand(
            [
                new ServiceChargeInput(ServiceName.Agua, ServiceMode.Incluido, null),
                new ServiceChargeInput(ServiceName.Internet, ServiceMode.Aparte, 300)
            ]));

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(2, result.Value!.Services.Count);
        Assert.Null(result.Value.Services[0].Amount);
        Assert.Equal(300, result.Value.Services[1].Amount);
    }

    [Fact]
    public async Task Configure_UnidadAjena_DebeDevolverForbidden()
    {
        var context = CreateContext();
        var service = new ConfigureUnitServicesService(context.Units, context.Eligibility);

        var result = await service.ConfigureAsync(
            Guid.NewGuid(),
            context.Unit.Id,
            new ConfigureUnitServicesCommand([]));

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    private static Context CreateContext()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var property = PublicationServiceTestData.CreateProperty(account.Id, Guid.NewGuid());
        var properties = new FakePropertyRepository();
        properties.Items.Add(property);
        var unit = PublicationServiceTestData.UnitFor(property.Id, account.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        return new Context(account, unit, units, eligibility);
    }

    private sealed record Context(
        Account Account,
        Unit Unit,
        FakeUnitRepository Units,
        PublishingEligibilityService Eligibility);
}
