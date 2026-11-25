using UniHome.Application.Common;
using UniHome.Application.Reparaciones;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Notifications;
using UniHome.Domain.Properties;
using UniHome.Domain.Reparaciones;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class ChangeReparacionStatusServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ChangeStatusAsync_ComoPropietario_ActualizaYNotificaAInquilinosActivos()
    {
        var units = new FakeUnitRepository();
        var unit = CreateUnit(out var ownerId);
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        var tenantId = Guid.NewGuid();
        links.Items.Add(OccupancyLink.CreateActive(tenantId, unit.Id, Now));
        var reparaciones = new FakeReparacionRepository();
        var reparacion = Reparacion.Create(unit.Id, tenantId, "Fuga", "Hay una fuga", null, Now);
        reparaciones.Items.Add(reparacion);
        var notifications = new FakeNotificationRepository();
        var service = new ChangeReparacionStatusService(
            units, links, reparaciones, notifications, new FixedClock(Now.AddHours(1)));

        var result = await service.ChangeStatusAsync(
            ownerId, unit.Id, reparacion.Id, ReparacionEstado.EnProceso);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(ReparacionEstado.EnProceso, result.Value!.Status);
        var notification = Assert.Single(notifications.Items);
        Assert.Equal(tenantId, notification.RecipientAccountId);
        Assert.Equal(NotificationType.ReparacionActualizada, notification.Type);
        Assert.Equal(reparacion.Id, notification.ReparacionId);
    }

    [Fact]
    public async Task ChangeStatusAsync_ElPropioReportanteCambiaElEstado_NoSeAutonotifica()
    {
        var units = new FakeUnitRepository();
        var unit = CreateUnit(out var ownerId);
        units.Items.Add(unit);
        var reparaciones = new FakeReparacionRepository();
        var reparacion = Reparacion.Create(unit.Id, ownerId, "Fuga", "Hay una fuga", null, Now);
        reparaciones.Items.Add(reparacion);
        var notifications = new FakeNotificationRepository();
        var service = new ChangeReparacionStatusService(
            units, new FakeOccupancyLinkRepository(), reparaciones, notifications, new FixedClock(Now));

        var result = await service.ChangeStatusAsync(
            ownerId, unit.Id, reparacion.Id, ReparacionEstado.Atendido);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Empty(notifications.Items);
    }

    [Fact]
    public async Task ChangeStatusAsync_ConElMismoEstado_NoGeneraNotificacion()
    {
        var units = new FakeUnitRepository();
        var unit = CreateUnit(out var ownerId);
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        var tenantId = Guid.NewGuid();
        links.Items.Add(OccupancyLink.CreateActive(tenantId, unit.Id, Now));
        var reparaciones = new FakeReparacionRepository();
        var reparacion = Reparacion.Create(unit.Id, tenantId, "Fuga", "Hay una fuga", null, Now);
        reparaciones.Items.Add(reparacion);
        var notifications = new FakeNotificationRepository();
        var service = new ChangeReparacionStatusService(
            units, links, reparaciones, notifications, new FixedClock(Now));

        var result = await service.ChangeStatusAsync(
            ownerId, unit.Id, reparacion.Id, ReparacionEstado.Pendiente);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Empty(notifications.Items);
    }

    [Fact]
    public async Task ChangeStatusAsync_ComoInquilino_DevuelveForbidden()
    {
        var units = new FakeUnitRepository();
        var unit = CreateUnit(out _);
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        var tenantId = Guid.NewGuid();
        links.Items.Add(OccupancyLink.CreateActive(tenantId, unit.Id, Now));
        var reparaciones = new FakeReparacionRepository();
        var reparacion = Reparacion.Create(unit.Id, tenantId, "Fuga", "Hay una fuga", null, Now);
        reparaciones.Items.Add(reparacion);
        var service = new ChangeReparacionStatusService(
            units, links, reparaciones, new FakeNotificationRepository(), new FixedClock(Now));

        var result = await service.ChangeStatusAsync(
            tenantId, unit.Id, reparacion.Id, ReparacionEstado.Atendido);

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_UnidadInexistente_DevuelveNotFound()
    {
        var service = new ChangeReparacionStatusService(
            new FakeUnitRepository(),
            new FakeOccupancyLinkRepository(),
            new FakeReparacionRepository(),
            new FakeNotificationRepository(),
            new FixedClock(Now));

        var result = await service.ChangeStatusAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ReparacionEstado.Atendido);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_ReporteInexistente_DevuelveNotFound()
    {
        var units = new FakeUnitRepository();
        var unit = CreateUnit(out var ownerId);
        units.Items.Add(unit);
        var service = new ChangeReparacionStatusService(
            units,
            new FakeOccupancyLinkRepository(),
            new FakeReparacionRepository(),
            new FakeNotificationRepository(),
            new FixedClock(Now));

        var result = await service.ChangeStatusAsync(
            ownerId, unit.Id, Guid.NewGuid(), ReparacionEstado.Atendido);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_ReporteDeOtraUnidad_DevuelveNotFound()
    {
        var units = new FakeUnitRepository();
        var unit = CreateUnit(out var ownerId);
        units.Items.Add(unit);
        var reparaciones = new FakeReparacionRepository();
        var reparacion = Reparacion.Create(Guid.NewGuid(), ownerId, "Fuga", "Hay una fuga", null, Now);
        reparaciones.Items.Add(reparacion);
        var service = new ChangeReparacionStatusService(
            units,
            new FakeOccupancyLinkRepository(),
            reparaciones,
            new FakeNotificationRepository(),
            new FixedClock(Now));

        var result = await service.ChangeStatusAsync(
            ownerId, unit.Id, reparacion.Id, ReparacionEstado.Atendido);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    private static Unit CreateUnit(out Guid ownerId)
    {
        ownerId = Guid.NewGuid();
        return Unit.Create(
            Guid.NewGuid(), ownerId, Guid.NewGuid(), UnitType.Habitacion, Occupancy.Compartida,
            3, 1500m, 12m, ForWhom.Mixto, BathroomType.Compartido, FurnitureType.SinMuebles,
            furnitureItems: null, template: null, Now);
    }
}
