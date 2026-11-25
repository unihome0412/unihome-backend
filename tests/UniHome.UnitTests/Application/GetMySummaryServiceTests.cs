using UniHome.Application.Rentals;
using UniHome.Application.Summary;
using UniHome.Domain.Properties;
using UniHome.Domain.Rentals;
using UniHome.Domain.Reparaciones;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class GetMySummaryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly string Periodo = "2026-11";

    private static IReadOnlyCollection<RentConcept> AllConcepts() =>
    [
        new RentConcept(RentConceptName.Renta, true),
        new RentConcept(RentConceptName.Mantenimiento, true),
        new RentConcept(RentConceptName.Luz, false),
        new RentConcept(RentConceptName.Gas, false)
    ];

    [Fact]
    public async Task GetAsync_SinPropiedades_DevuelveCeros()
    {
        var service = CreateService(
            out _, out _, out _, out _, out _);

        var view = await service.GetAsync(Guid.NewGuid());

        Assert.Equal(0m, view.RentasRegistradasDelMesMxn);
        Assert.Equal(0, view.NumeroPropiedades);
        Assert.Equal(0, view.ReparacionesAbiertas);
    }

    [Fact]
    public async Task GetAsync_ConPropiedadRentaYReparaciones_AgregaLosTresIndicadores()
    {
        var service = CreateService(
            out var properties, out var units, out var rentPeriods, out var payments, out var reparaciones);
        var ownerId = Guid.NewGuid();

        var property = Property.Create(
            ownerId, "Casa", "Calle 1", "Centro", PropertyType.Casa, Guid.NewGuid(),
            latitude: null, longitude: null, floor: null, unitNumber: null, Now);
        properties.Items.Add(property);

        var unit = Unit.Create(
            property.Id, ownerId, Guid.NewGuid(), UnitType.Habitacion, Occupancy.Individual,
            1, 1500m, 12m, ForWhom.Mixto, BathroomType.Compartido, FurnitureType.SinMuebles,
            furnitureItems: null, template: null, Now);
        units.Items.Add(unit);

        var period = RentPeriod.Configure(unit.Id, Periodo, 1000m, 5, AllConcepts(), Now);
        await rentPeriods.SaveAsync(period);
        await payments.SaveAsync(InformativePayment.Register(
            period.Id, Guid.NewGuid(), 600m, DateOnly.FromDateTime(Now.UtcDateTime), Now));

        reparaciones.Items.Add(Reparacion.Create(unit.Id, ownerId, "Fuga", "Hay una fuga", null, Now));
        var atendido = Reparacion.Create(unit.Id, ownerId, "Foco", "Fundido", null, Now);
        atendido.ChangeStatus(ReparacionEstado.Atendido, Now);
        reparaciones.Items.Add(atendido);

        var view = await service.GetAsync(ownerId);

        Assert.Equal(Periodo, view.Periodo);
        Assert.Equal(600m, view.RentasRegistradasDelMesMxn);
        Assert.Equal(1, view.NumeroPropiedades);
        Assert.Equal(1, view.ReparacionesAbiertas);
    }

    [Fact]
    public async Task GetAsync_SinRentaConfigurada_NoSumaRenta()
    {
        var service = CreateService(
            out var properties, out var units, out _, out _, out _);
        var ownerId = Guid.NewGuid();
        var property = Property.Create(
            ownerId, "Casa", "Calle 1", "Centro", PropertyType.Casa, Guid.NewGuid(),
            latitude: null, longitude: null, floor: null, unitNumber: null, Now);
        properties.Items.Add(property);
        var unit = Unit.Create(
            property.Id, ownerId, Guid.NewGuid(), UnitType.Habitacion, Occupancy.Individual,
            1, 1500m, 12m, ForWhom.Mixto, BathroomType.Compartido, FurnitureType.SinMuebles,
            furnitureItems: null, template: null, Now);
        units.Items.Add(unit);

        var view = await service.GetAsync(ownerId);

        Assert.Equal(0m, view.RentasRegistradasDelMesMxn);
        Assert.Equal(1, view.NumeroPropiedades);
    }

    private static GetMySummaryService CreateService(
        out FakePropertyRepository properties,
        out FakeUnitRepository units,
        out FakeRentPeriodRepository rentPeriods,
        out FakeInformativePaymentRepository payments,
        out FakeReparacionRepository reparaciones)
    {
        properties = new FakePropertyRepository();
        units = new FakeUnitRepository();
        rentPeriods = new FakeRentPeriodRepository();
        payments = new FakeInformativePaymentRepository();
        reparaciones = new FakeReparacionRepository();
        var links = new FakeOccupancyLinkRepository();
        var resolver = new RentPeriodResolver(rentPeriods, links, new FixedClock(Now));

        return new GetMySummaryService(units, properties, resolver, payments, reparaciones, new FixedClock(Now));
    }
}
