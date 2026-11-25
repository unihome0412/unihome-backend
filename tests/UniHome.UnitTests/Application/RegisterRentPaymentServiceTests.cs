using UniHome.Application.Common;
using UniHome.Application.Rentals;
using UniHome.Application.Rentals.Payments;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Rentals;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class RegisterRentPaymentServiceTests
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
        RegisterRentPaymentService Service,
        FakeUnitRepository Units,
        FakeOccupancyLinkRepository Links,
        FakeRentPeriodRepository RentPeriods,
        FakeInformativePaymentRepository Payments,
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
        var payments = new FakeInformativePaymentRepository();
        var resolver = new RentPeriodResolver(rentPeriods, links, new FixedClock(Now));
        var service = new RegisterRentPaymentService(
            units, links, payments, resolver, eligibility, new FixedClock(Now));

        return new Sut(service, units, links, rentPeriods, payments, owner, unit);
    }

    [Fact]
    public async Task RegisterAsync_InquilinoActivo_RegistraElPago()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenant, sut.Unit.Id, Now));
        sut.RentPeriods.Items.Add(RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now));

        var result = await sut.Service.RegisterAsync(
            sut.Owner.Id, sut.Unit.Id, "2026-11", tenant, 500m, DateOnly.FromDateTime(Now.UtcDateTime));

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(500m, result.Value!.AmountMxn);
        Assert.Single(sut.Payments.Items);
    }

    [Fact]
    public async Task RegisterAsync_InquilinoSinVinculoActivo_DebeRechazarse()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        sut.RentPeriods.Items.Add(RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now));

        var result = await sut.Service.RegisterAsync(
            sut.Owner.Id, sut.Unit.Id, "2026-11", tenant, 500m, DateOnly.FromDateTime(Now.UtcDateTime));

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task RegisterAsync_MontoMayorQueLaCuota_NoSeRechaza()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenant, sut.Unit.Id, Now));
        sut.RentPeriods.Items.Add(RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now));

        var result = await sut.Service.RegisterAsync(
            sut.Owner.Id, sut.Unit.Id, "2026-11", tenant, 5000m, DateOnly.FromDateTime(Now.UtcDateTime));

        Assert.Equal(OperationStatus.Success, result.Status);
    }

    [Fact]
    public async Task RegisterAsync_MontoNoPositivoOFechaFutura_DebeRechazarse()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenant, sut.Unit.Id, Now));
        sut.RentPeriods.Items.Add(RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now));

        var negative = await sut.Service.RegisterAsync(
            sut.Owner.Id, sut.Unit.Id, "2026-11", tenant, -1m, DateOnly.FromDateTime(Now.UtcDateTime));
        var future = await sut.Service.RegisterAsync(
            sut.Owner.Id, sut.Unit.Id, "2026-11", tenant, 100m,
            DateOnly.FromDateTime(Now.UtcDateTime).AddDays(1));

        Assert.Equal(OperationStatus.ValidationFailed, negative.Status);
        Assert.Equal(OperationStatus.ValidationFailed, future.Status);
    }

    [Fact]
    public async Task RegisterAsync_SinPeriodoNiAnterior_DebeDevolverNotFound()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenant, sut.Unit.Id, Now));

        var result = await sut.Service.RegisterAsync(
            sut.Owner.Id, sut.Unit.Id, "2026-11", tenant, 500m, DateOnly.FromDateTime(Now.UtcDateTime));

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task RegisterAsync_UnidadAjena_DebeDevolverForbidden()
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
        var payments = new FakeInformativePaymentRepository();
        var resolver = new RentPeriodResolver(rentPeriods, links, new FixedClock(Now));
        var service = new RegisterRentPaymentService(
            units, links, payments, resolver, eligibility, new FixedClock(Now));

        var result = await service.RegisterAsync(
            caller.Id, unit.Id, "2026-11", Guid.NewGuid(), 500m, DateOnly.FromDateTime(Now.UtcDateTime));

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }
}
