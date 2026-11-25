using UniHome.Application.Common;
using UniHome.Application.Rentals;
using UniHome.Application.Rentals.Payments;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Rentals;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class GetMyRentSummaryServiceTests
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
        GetMyRentSummaryService Service,
        FakeUnitRepository Units,
        FakeOccupancyLinkRepository Links,
        FakeRentPeriodRepository RentPeriods,
        FakeInformativePaymentRepository Payments);

    private static Sut CreateSut()
    {
        var units = new FakeUnitRepository();
        var links = new FakeOccupancyLinkRepository();
        var rentPeriods = new FakeRentPeriodRepository();
        var payments = new FakeInformativePaymentRepository();
        var resolver = new RentPeriodResolver(rentPeriods, links, new FixedClock(Now));
        var service = new GetMyRentSummaryService(units, links, payments, resolver);

        return new Sut(service, units, links, rentPeriods, payments);
    }

    [Fact]
    public async Task GetAsync_ComoPropietario_ResumeRecaudadoYPendiente()
    {
        var sut = CreateSut();
        var owner = PublicationServiceTestData.Adult();
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        sut.Units.Items.Add(unit);
        var tenant = Guid.NewGuid();
        var period = RentPeriod.Configure(unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now);
        period.ApplyEqualSplit([tenant], Now);
        sut.RentPeriods.Items.Add(period);
        sut.Payments.Items.Add(InformativePayment.Register(
            period.Id, tenant, 400m, DateOnly.FromDateTime(Now.UtcDateTime), Now));

        var result = await sut.Service.GetAsync(owner.Id, "2026-11");

        Assert.Equal(OperationStatus.Success, result.Status);
        var item = Assert.Single(result.Value!.ComoPropietario);
        Assert.Equal(unit.Id, item.UnitId);
        Assert.Equal(1000m, item.RentaTotalMxn);
        Assert.Equal(400m, item.RecaudadoMxn);
        Assert.Equal(600m, item.PendienteMxn);
        Assert.False(item.DivisionPendiente);
        Assert.Empty(result.Value.ComoInquilino);
    }

    [Fact]
    public async Task GetAsync_UnidadPropiaSinRentaConfiguradaEnElPeriodo_SeOmite()
    {
        var sut = CreateSut();
        var owner = PublicationServiceTestData.Adult();
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        sut.Units.Items.Add(unit);

        var result = await sut.Service.GetAsync(owner.Id, "2026-11");

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Empty(result.Value!.ComoPropietario);
    }

    [Fact]
    public async Task GetAsync_ComoInquilino_ResumePropiaCuota()
    {
        var sut = CreateSut();
        var owner = PublicationServiceTestData.Adult();
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        sut.Units.Items.Add(unit);
        var tenant = PublicationServiceTestData.Adult("inquilino@uni.edu");
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenant.Id, unit.Id, Now));
        var period = RentPeriod.Configure(unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now);
        period.ApplyEqualSplit([tenant.Id], Now);
        sut.RentPeriods.Items.Add(period);

        var result = await sut.Service.GetAsync(tenant.Id, "2026-11");

        Assert.Equal(OperationStatus.Success, result.Status);
        var item = Assert.Single(result.Value!.ComoInquilino);
        Assert.Equal(unit.Id, item.UnitId);
        Assert.Equal(1000m, item.CuotaMxn);
        Assert.Equal(RentPaymentState.Pendiente, item.Estado);
        Assert.Empty(result.Value.ComoPropietario);
    }

    [Fact]
    public async Task GetAsync_VinculoActivoConDivisionPendiente_SeOmiteDeComoInquilino()
    {
        var sut = CreateSut();
        var owner = PublicationServiceTestData.Adult();
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        sut.Units.Items.Add(unit);
        var tenant = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenant, unit.Id, Now));
        sut.RentPeriods.Items.Add(RentPeriod.Configure(unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now));

        var result = await sut.Service.GetAsync(tenant, "2026-11");

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Empty(result.Value!.ComoInquilino);
    }

    [Fact]
    public async Task GetAsync_SinUnidadesNiVinculos_DevuelveListasVacias()
    {
        var sut = CreateSut();

        var result = await sut.Service.GetAsync(Guid.NewGuid(), "2026-11");

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Empty(result.Value!.ComoPropietario);
        Assert.Empty(result.Value.ComoInquilino);
    }

    [Fact]
    public async Task GetAsync_PeriodoConFormatoInvalido_DebeDevolverValidationFailed()
    {
        var sut = CreateSut();

        var result = await sut.Service.GetAsync(Guid.NewGuid(), "noviembre");

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
    }
}
