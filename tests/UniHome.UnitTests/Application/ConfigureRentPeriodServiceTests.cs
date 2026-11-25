using UniHome.Application.Common;
using UniHome.Application.Rentals.Configure;
using UniHome.Domain.Rentals;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class ConfigureRentPeriodServiceTests
{
    private static IReadOnlyCollection<RentConcept> AllConcepts() =>
    [
        new RentConcept(RentConceptName.Renta, true),
        new RentConcept(RentConceptName.Mantenimiento, true),
        new RentConcept(RentConceptName.Luz, false),
        new RentConcept(RentConceptName.Gas, false)
    ];

    private static (ConfigureRentPeriodService Service, FakeUnitRepository Units, FakeRentPeriodRepository RentPeriods, UniHome.Domain.Accounts.Account Owner, UniHome.Domain.Units.Unit Unit)
        CreateSut()
    {
        var accounts = new InMemoryAccountRepository();
        var owner = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, owner);
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var rentPeriods = new FakeRentPeriodRepository();
        var service = new ConfigureRentPeriodService(
            units, rentPeriods, eligibility, new FixedClock(PublicationServiceTestData.Now));

        return (service, units, rentPeriods, owner, unit);
    }

    [Fact]
    public async Task ConfigureAsync_PeriodoNuevo_CreaConDivisionPendiente()
    {
        var (service, _, rentPeriods, owner, unit) = CreateSut();

        var result = await service.ConfigureAsync(
            owner.Id, unit.Id, "2026-11", 6000m, 5, AllConcepts());

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.True(result.Value!.IsSplitPending);
        Assert.Single(rentPeriods.Items);
    }

    [Fact]
    public async Task ConfigureAsync_PeriodoExistenteConDivision_ReinicioLaDivision()
    {
        var (service, _, rentPeriods, owner, unit) = CreateSut();
        await service.ConfigureAsync(owner.Id, unit.Id, "2026-11", 6000m, 5, AllConcepts());
        var period = rentPeriods.Items.Single();
        period.ApplyEqualSplit([Guid.NewGuid(), Guid.NewGuid()], PublicationServiceTestData.Now);

        var result = await service.ConfigureAsync(
            owner.Id, unit.Id, "2026-11", 9000m, 10, AllConcepts());

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(9000m, result.Value!.TotalMxn);
        Assert.True(result.Value!.IsSplitPending);
        Assert.Single(rentPeriods.Items);
    }

    [Fact]
    public async Task ConfigureAsync_ConceptosNulos_DebeRechazarse()
    {
        var (service, _, _, owner, unit) = CreateSut();

        var result = await service.ConfigureAsync(
            owner.Id, unit.Id, "2026-11", 6000m, 5, concepts: null);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task ConfigureAsync_PeriodoInvalido_DebeRechazarse()
    {
        var (service, _, _, owner, unit) = CreateSut();

        var result = await service.ConfigureAsync(
            owner.Id, unit.Id, "2026-13", 6000m, 5, AllConcepts());

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task ConfigureAsync_UnidadAjena_DebeDevolverForbidden()
    {
        var accounts = new InMemoryAccountRepository();
        var caller = PublicationServiceTestData.Adult("otra@uni.edu");
        var eligibility = PublicationServiceTestData.Eligibility(accounts, caller);
        var property = PublicationServiceTestData.CreateProperty(Guid.NewGuid(), Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(
            property.Id, property.OwnerAccountId, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var rentPeriods = new FakeRentPeriodRepository();
        var service = new ConfigureRentPeriodService(
            units, rentPeriods, eligibility, new FixedClock(PublicationServiceTestData.Now));

        var result = await service.ConfigureAsync(
            caller.Id, unit.Id, "2026-11", 6000m, 5, AllConcepts());

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task ConfigureAsync_UnidadInexistente_DebeDevolverNotFound()
    {
        var (service, units, _, owner, unit) = CreateSut();
        units.Items.Remove(unit);

        var result = await service.ConfigureAsync(
            owner.Id, unit.Id, "2026-11", 6000m, 5, AllConcepts());

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }
}
