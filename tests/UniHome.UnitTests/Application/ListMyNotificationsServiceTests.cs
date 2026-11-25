using UniHome.Application.Notifications;
using UniHome.Application.Rentals;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Rentals;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class ListMyNotificationsServiceTests
{
    private static readonly DateTimeOffset Now = PublicationServiceTestData.Now; // 2026-11-13

    private static IReadOnlyCollection<RentConcept> AllConcepts() =>
    [
        new RentConcept(RentConceptName.Renta, true),
        new RentConcept(RentConceptName.Mantenimiento, true),
        new RentConcept(RentConceptName.Luz, false),
        new RentConcept(RentConceptName.Gas, false)
    ];

    private sealed record Sut(
        ListMyNotificationsService Service,
        FakeOccupancyLinkRepository Links,
        FakeRentPeriodRepository RentPeriods,
        FakeInformativePaymentRepository Payments,
        FakeNotificationRepository Notifications);

    private static Sut CreateSut()
    {
        var links = new FakeOccupancyLinkRepository();
        var rentPeriods = new FakeRentPeriodRepository();
        var payments = new FakeInformativePaymentRepository();
        var notifications = new FakeNotificationRepository();
        var resolver = new RentPeriodResolver(rentPeriods, links, new FixedClock(Now));
        var service = new ListMyNotificationsService(
            links, resolver, payments, notifications, new FixedClock(Now));

        return new Sut(service, links, rentPeriods, payments, notifications);
    }

    [Fact]
    public async Task ListAsync_CorteDentroDeTresDiasSinPagar_GeneraRecordatorio()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenant, unitId, Now));
        var period = RentPeriod.Configure(unitId, "2026-11", 1000m, 15, AllConcepts(), Now);
        period.ApplyEqualSplit([tenant], Now);
        sut.RentPeriods.Items.Add(period);

        var result = await sut.Service.ListAsync(tenant);

        var reminder = Assert.Single(result);
        Assert.Equal(unitId, reminder.UnitId);
        Assert.Equal("2026-11", reminder.Periodo);
    }

    [Fact]
    public async Task ListAsync_CorteLejano_NoGeneraRecordatorio()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenant, unitId, Now));
        var period = RentPeriod.Configure(unitId, "2026-11", 1000m, 25, AllConcepts(), Now);
        period.ApplyEqualSplit([tenant], Now);
        sut.RentPeriods.Items.Add(period);

        var result = await sut.Service.ListAsync(tenant);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListAsync_CorteYaPasado_NoGeneraRecordatorio()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenant, unitId, Now));
        var period = RentPeriod.Configure(unitId, "2026-11", 1000m, 1, AllConcepts(), Now);
        period.ApplyEqualSplit([tenant], Now);
        sut.RentPeriods.Items.Add(period);

        var result = await sut.Service.ListAsync(tenant);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListAsync_CuotaYaCubierta_NoGeneraRecordatorio()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenant, unitId, Now));
        var period = RentPeriod.Configure(unitId, "2026-11", 1000m, 15, AllConcepts(), Now);
        period.ApplyEqualSplit([tenant], Now);
        sut.RentPeriods.Items.Add(period);
        sut.Payments.Items.Add(InformativePayment.Register(
            period.Id, tenant, 1000m, DateOnly.FromDateTime(Now.UtcDateTime), Now));

        var result = await sut.Service.ListAsync(tenant);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListAsync_DivisionPendiente_NoGeneraRecordatorio()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenant, unitId, Now));
        sut.RentPeriods.Items.Add(RentPeriod.Configure(unitId, "2026-11", 1000m, 15, AllConcepts(), Now));

        var result = await sut.Service.ListAsync(tenant);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListAsync_SinVinculosActivos_DevuelveVacioSinFallar()
    {
        var sut = CreateSut();

        var result = await sut.Service.ListAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListAsync_LlamadoDosVecesEnLaMismaVentana_NoDuplicaElRecordatorio()
    {
        var sut = CreateSut();
        var tenant = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        sut.Links.Items.Add(OccupancyLink.CreateActive(tenant, unitId, Now));
        var period = RentPeriod.Configure(unitId, "2026-11", 1000m, 15, AllConcepts(), Now);
        period.ApplyEqualSplit([tenant], Now);
        sut.RentPeriods.Items.Add(period);

        await sut.Service.ListAsync(tenant);
        var result = await sut.Service.ListAsync(tenant);

        Assert.Single(result);
        Assert.Single(sut.Notifications.Items);
    }
}
