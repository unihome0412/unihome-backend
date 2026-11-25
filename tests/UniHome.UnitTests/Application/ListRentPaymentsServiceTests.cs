using UniHome.Application.Common;
using UniHome.Application.Rentals;
using UniHome.Application.Rentals.Payments;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Rentals;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class ListRentPaymentsServiceTests
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
        ListRentPaymentsService Service,
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
        var service = new ListRentPaymentsService(units, payments, resolver, eligibility);

        return new Sut(service, units, links, rentPeriods, payments, owner, unit);
    }

    [Fact]
    public async Task ListAsync_SinPagos_TodosPendientes()
    {
        var sut = CreateSut();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var period = RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now);
        period.ApplyEqualSplit([tenantA, tenantB], Now);
        sut.RentPeriods.Items.Add(period);

        var result = await sut.Service.ListAsync(sut.Owner.Id, sut.Unit.Id, "2026-11");

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.All(result.Value!.Tenants, tenant => Assert.Equal(RentPaymentState.Pendiente, tenant.Estado));
    }

    [Fact]
    public async Task ListAsync_PagoParcialYCompleto_ClasificaCorrectamente()
    {
        var sut = CreateSut();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var period = RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now);
        period.ApplyEqualSplit([tenantA, tenantB], Now);
        sut.RentPeriods.Items.Add(period);
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        sut.Payments.Items.Add(InformativePayment.Register(period.Id, tenantA, 200m, today, Now));
        sut.Payments.Items.Add(InformativePayment.Register(period.Id, tenantB, 500m, today, Now));

        var result = await sut.Service.ListAsync(sut.Owner.Id, sut.Unit.Id, "2026-11");

        Assert.Equal(OperationStatus.Success, result.Status);
        var tenantAView = result.Value!.Tenants.Single(tenant => tenant.TenantAccountId == tenantA);
        var tenantBView = result.Value!.Tenants.Single(tenant => tenant.TenantAccountId == tenantB);
        Assert.Equal(RentPaymentState.Parcial, tenantAView.Estado);
        Assert.Equal(RentPaymentState.Registrado, tenantBView.Estado);
        Assert.Equal(0m, tenantBView.SaldoMxn);
    }

    [Fact]
    public async Task ListAsync_SobrepagoDejaSaldoAFavor_Registrado()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        var period = RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now);
        period.ApplyEqualSplit([tenant], Now);
        sut.RentPeriods.Items.Add(period);
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        sut.Payments.Items.Add(InformativePayment.Register(period.Id, tenant, 1500m, today, Now));

        var result = await sut.Service.ListAsync(sut.Owner.Id, sut.Unit.Id, "2026-11");

        var view = Assert.Single(result.Value!.Tenants);
        Assert.Equal(RentPaymentState.Registrado, view.Estado);
        Assert.Equal(500m, view.SaldoMxn);
    }

    [Fact]
    public async Task ListAsync_DivisionPendiente_DebeDevolverUnprocessable()
    {
        var sut = CreateSut();
        sut.RentPeriods.Items.Add(RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now));

        var result = await sut.Service.ListAsync(sut.Owner.Id, sut.Unit.Id, "2026-11");

        Assert.Equal(OperationStatus.Unprocessable, result.Status);
    }

    [Fact]
    public async Task ListAsync_SinPeriodoNiAnterior_DebeDevolverNotFound()
    {
        var sut = CreateSut();

        var result = await sut.Service.ListAsync(sut.Owner.Id, sut.Unit.Id, "2026-11");

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ListAsync_UnidadAjena_DebeDevolverForbidden()
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
        var service = new ListRentPaymentsService(units, payments, resolver, eligibility);

        var result = await service.ListAsync(caller.Id, unit.Id, "2026-11");

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }
}
