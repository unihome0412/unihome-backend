using UniHome.Application.Common;
using UniHome.Application.Rentals;
using UniHome.Application.Rentals.Split;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Rentals;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class SplitRentPeriodServiceTests
{
    private static readonly DateTimeOffset Now = PublicationServiceTestData.Now;

    private static IReadOnlyCollection<RentConcept> AllConcepts() =>
    [
        new RentConcept(RentConceptName.Renta, true),
        new RentConcept(RentConceptName.Mantenimiento, true),
        new RentConcept(RentConceptName.Luz, false),
        new RentConcept(RentConceptName.Gas, false)
    ];

    private sealed record Sut(
        SplitRentPeriodService Service,
        FakeUnitRepository Units,
        FakeOccupancyLinkRepository Links,
        FakeRentPeriodRepository RentPeriods,
        UniHome.Domain.Accounts.Account Owner,
        UniHome.Domain.Units.Unit Unit);

    private static Sut CreateSut()
    {
        var accounts = new InMemoryAccountRepository();
        var owner = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, owner);
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        var rentPeriods = new FakeRentPeriodRepository();
        var resolver = new RentPeriodResolver(rentPeriods, links, new FixedClock(Now));
        var service = new SplitRentPeriodService(
            units, links, rentPeriods, resolver, eligibility, new FixedClock(Now));

        return new Sut(service, units, links, rentPeriods, owner, unit);
    }

    [Fact]
    public async Task SplitAsync_Igual_DivideEntreInquilinosActivos()
    {
        var sut = CreateSut();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenantA, sut.Unit.Id, Now));
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenantB, sut.Unit.Id, Now));
        sut.RentPeriods.Items.Add(RentPeriod.Configure(sut.Unit.Id, "2026-11", 4000m, 5, AllConcepts(), Now));

        var result = await sut.Service.SplitAsync(
            sut.Owner.Id, sut.Unit.Id, "2026-11", RentSplitMethod.Igual, customShares: null);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(RentSplitMethod.Igual, result.Value!.SplitMethod);
        Assert.All(result.Value!.Shares, share => Assert.Equal(2000m, share.AmountMxn));
    }

    [Fact]
    public async Task SplitAsync_Personalizada_SumaCorrectaYInquilinosActivos_Aplica()
    {
        var sut = CreateSut();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenantA, sut.Unit.Id, Now));
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenantB, sut.Unit.Id, Now));
        sut.RentPeriods.Items.Add(RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now));

        var result = await sut.Service.SplitAsync(
            sut.Owner.Id,
            sut.Unit.Id,
            "2026-11",
            RentSplitMethod.Personalizada,
            [new TenantShare(tenantA, 700m), new TenantShare(tenantB, 300m)]);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(RentSplitMethod.Personalizada, result.Value!.SplitMethod);
    }

    [Fact]
    public async Task SplitAsync_Personalizada_SumaDistinta_DebeRechazarse()
    {
        var sut = CreateSut();
        var tenantA = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenantA, sut.Unit.Id, Now));
        sut.RentPeriods.Items.Add(RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now));

        var result = await sut.Service.SplitAsync(
            sut.Owner.Id,
            sut.Unit.Id,
            "2026-11",
            RentSplitMethod.Personalizada,
            [new TenantShare(tenantA, 400m)]);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task SplitAsync_Personalizada_InquilinoSinVinculoActivo_DebeRechazarse()
    {
        var sut = CreateSut();
        var tenantA = Guid.NewGuid();
        var ajeno = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenantA, sut.Unit.Id, Now));
        sut.RentPeriods.Items.Add(RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now));

        var result = await sut.Service.SplitAsync(
            sut.Owner.Id,
            sut.Unit.Id,
            "2026-11",
            RentSplitMethod.Personalizada,
            [new TenantShare(tenantA, 600m), new TenantShare(ajeno, 400m)]);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task SplitAsync_SinPeriodoNiAnterior_DebeDevolverNotFound()
    {
        var sut = CreateSut();

        var result = await sut.Service.SplitAsync(
            sut.Owner.Id, sut.Unit.Id, "2026-11", RentSplitMethod.Igual, customShares: null);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task SplitAsync_HeredaPeriodoAnterior_MismosInquilinos_DivideSobreElHeredado()
    {
        var sut = CreateSut();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenantA, sut.Unit.Id, Now));
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenantB, sut.Unit.Id, Now));
        var previous = RentPeriod.Configure(sut.Unit.Id, "2026-10", 4000m, 5, AllConcepts(), Now);
        previous.ApplyEqualSplit([tenantA, tenantB], Now);
        sut.RentPeriods.Items.Add(previous);

        var result = await sut.Service.SplitAsync(
            sut.Owner.Id,
            sut.Unit.Id,
            "2026-11",
            RentSplitMethod.Personalizada,
            [new TenantShare(tenantA, 2500m), new TenantShare(tenantB, 1500m)]);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("2026-11", result.Value!.Periodo);
        Assert.Equal(2, sut.RentPeriods.Items.Count(item => item.UnitId == sut.Unit.Id));
    }

    [Fact]
    public async Task SplitAsync_UnidadAjena_DebeDevolverForbidden()
    {
        var accounts = new InMemoryAccountRepository();
        var caller = PublicationServiceTestData.Adult("otra@uni.edu");
        var eligibility = PublicationServiceTestData.Eligibility(accounts, caller);
        var property = PublicationServiceTestData.CreateProperty(Guid.NewGuid(), Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(
            property.Id, property.OwnerAccountId, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        var rentPeriods = new FakeRentPeriodRepository();
        var resolver = new RentPeriodResolver(rentPeriods, links, new FixedClock(Now));
        var service = new SplitRentPeriodService(
            units, links, rentPeriods, resolver, eligibility, new FixedClock(Now));

        var result = await service.SplitAsync(
            caller.Id, unit.Id, "2026-11", RentSplitMethod.Igual, customShares: null);

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }
}
