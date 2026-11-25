using UniHome.Application.Common;
using UniHome.Application.Rentals.Payments;
using UniHome.Domain.Rentals;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class GetMyPaymentHistoryServiceTests
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
        GetMyPaymentHistoryService Service,
        FakeUnitRepository Units,
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
        var rentPeriods = new FakeRentPeriodRepository();
        var payments = new FakeInformativePaymentRepository();
        var service = new GetMyPaymentHistoryService(units, rentPeriods, payments);

        return new Sut(service, units, rentPeriods, payments, unit);
    }

    [Fact]
    public async Task GetAsync_ConPagosEnVariosPeriodos_DevuelveSoloLosPropiosOrdenadosDescendente()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var periodOctubre = RentPeriod.Configure(sut.Unit.Id, "2026-10", 1000m, 5, AllConcepts(), Now);
        var periodNoviembre = RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now);
        sut.RentPeriods.Items.Add(periodOctubre);
        sut.RentPeriods.Items.Add(periodNoviembre);
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        sut.Payments.Items.Add(InformativePayment.Register(periodOctubre.Id, tenant, 500m, today, Now));
        sut.Payments.Items.Add(InformativePayment.Register(periodNoviembre.Id, tenant, 600m, today, Now));
        sut.Payments.Items.Add(InformativePayment.Register(periodNoviembre.Id, otherTenant, 700m, today, Now));

        var result = await sut.Service.GetAsync(tenant, sut.Unit.Id);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("2026-11", result.Value[0].Periodo);
        Assert.Equal(600m, result.Value[0].MontoMxn);
        Assert.Equal("2026-10", result.Value[1].Periodo);
        Assert.Equal(500m, result.Value[1].MontoMxn);
    }

    [Fact]
    public async Task GetAsync_InquilinoSinPagos_DevuelveListaVacia()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        var period = RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now);
        sut.RentPeriods.Items.Add(period);

        var result = await sut.Service.GetAsync(tenant, sut.Unit.Id);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetAsync_InquilinoYaDejoLaUnidad_SigueViendoSuHistorial()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        var period = RentPeriod.Configure(sut.Unit.Id, "2026-11", 1000m, 5, AllConcepts(), Now);
        sut.RentPeriods.Items.Add(period);
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        sut.Payments.Items.Add(InformativePayment.Register(period.Id, tenant, 500m, today, Now));

        // Nunca se agrega un vínculo activo para el inquilino: simula que ya dejó la unidad.
        var result = await sut.Service.GetAsync(tenant, sut.Unit.Id);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task GetAsync_UnidadInexistente_DebeDevolverNotFound()
    {
        var sut = CreateSut();

        var result = await sut.Service.GetAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }
}
