using UniHome.Application.Common;
using UniHome.Application.Rentals;
using UniHome.Application.Rentals.Payments;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Rentals;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class GetMyRentShareServiceTests
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
        GetMyRentShareService Service,
        FakeUnitRepository Units,
        FakeOccupancyLinkRepository Links,
        FakeRentPeriodRepository RentPeriods,
        FakeInformativePaymentRepository Payments,
        UniHome.Domain.Units.Unit Unit);

    private static Sut CreateSut()
    {
        var owner = PublicationServiceTestData.Adult();
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        var rentPeriods = new FakeRentPeriodRepository();
        var payments = new FakeInformativePaymentRepository();
        var resolver = new RentPeriodResolver(rentPeriods, links, new FixedClock(Now));
        var service = new GetMyRentShareService(units, links, payments, resolver);

        return new Sut(service, units, links, rentPeriods, payments, unit);
    }

    [Fact]
    public async Task GetAsync_InquilinoActivoConCuota_DevuelveSaldo()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenant, sut.Unit.Id, Now));
        var period = RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now);
        period.ApplyEqualSplit([tenant], Now);
        sut.RentPeriods.Items.Add(period);
        sut.Payments.Items.Add(InformativePayment.Register(
            period.Id, tenant, 400m, DateOnly.FromDateTime(Now.UtcDateTime), Now));

        var result = await sut.Service.GetAsync(tenant, sut.Unit.Id, "2026-11");

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(1000m, result.Value!.CuotaMxn);
        Assert.Equal(400m, result.Value.PagadoMxn);
        Assert.Equal(-600m, result.Value.SaldoMxn);
        Assert.Equal(RentPaymentState.Parcial, result.Value.Estado);
    }

    [Fact]
    public async Task GetAsync_SinVinculoActivo_DebeDevolverForbidden()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        var period = RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now);
        period.ApplyEqualSplit([tenant], Now);
        sut.RentPeriods.Items.Add(period);

        var result = await sut.Service.GetAsync(tenant, sut.Unit.Id, "2026-11");

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task GetAsync_DivisionPendiente_DebeDevolverUnprocessable()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenant, sut.Unit.Id, Now));
        sut.RentPeriods.Items.Add(RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now));

        var result = await sut.Service.GetAsync(tenant, sut.Unit.Id, "2026-11");

        Assert.Equal(OperationStatus.Unprocessable, result.Status);
    }

    [Fact]
    public async Task GetAsync_InquilinoActivoSinCuotaAsignada_DebeDevolverNotFound()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenant, sut.Unit.Id, Now));
        var period = RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now);
        period.ApplyEqualSplit([otherTenant], Now);
        sut.RentPeriods.Items.Add(period);

        var result = await sut.Service.GetAsync(tenant, sut.Unit.Id, "2026-11");

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetAsync_SinPeriodoNiAnterior_DebeDevolverNotFound()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenant, sut.Unit.Id, Now));

        var result = await sut.Service.GetAsync(tenant, sut.Unit.Id, "2026-11");

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetAsync_UnidadInexistente_DebeDevolverNotFound()
    {
        var sut = CreateSut();

        var result = await sut.Service.GetAsync(Guid.NewGuid(), Guid.NewGuid(), "2026-11");

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }
}
