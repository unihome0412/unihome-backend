using UniHome.Application.Common;
using UniHome.Application.OccupancyLinks;
using UniHome.Application.OccupancyLinks.LeaveUnit;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class LeaveUnitServiceTests
{
    private static readonly DateTimeOffset Now = PublicationServiceTestData.Now;

    [Fact]
    public async Task LeaveAsync_ConConfirmacion_FinalizaVinculoYLiberaLugar()
    {
        var owner = PublicationServiceTestData.Adult();
        var tenant = Guid.NewGuid();
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        Assert.NotNull(await units.TryReserveSlotAsync(unit.Id, unit.PropertyId));
        unit.SetAvailability(UnitAvailability.Ocupado);
        var links = new FakeOccupancyLinkRepository();
        links.Items.Add(OccupancyLink.CreateActive(tenant, unit.Id, Now));
        var service = new LeaveUnitService(
            units, links, new UnitAvailabilityRecalculator(units), new FixedClock(Now));

        var result = await service.LeaveAsync(tenant, unit.Id, confirmacion: true);

        Assert.Equal(OperationStatus.Success, result.Status);
        var link = await links.FindAsync(tenant, unit.Id);
        Assert.Equal(OccupancyStatus.Finalizado, link!.Status);
        Assert.Equal(OccupancyEndReason.AbandonoInquilino, link.EndedReason);
        Assert.Equal(0, unit.OccupiedPlaces);
        Assert.Equal(UnitAvailability.Disponible, unit.Availability);
    }

    [Fact]
    public async Task LeaveAsync_DosVeces_NoLiberaElMismoLugarDosVeces()
    {
        var owner = PublicationServiceTestData.Adult("owner-leave-twice@uni.edu");
        var tenant = Guid.NewGuid();
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        Assert.NotNull(await units.TryReserveSlotAsync(unit.Id, unit.PropertyId));
        var links = new FakeOccupancyLinkRepository();
        links.Items.Add(OccupancyLink.CreateActive(tenant, unit.Id, Now));
        var service = new LeaveUnitService(
            units, links, new UnitAvailabilityRecalculator(units), new FixedClock(Now));

        var first = await service.LeaveAsync(tenant, unit.Id, confirmacion: true);
        var second = await service.LeaveAsync(tenant, unit.Id, confirmacion: true);

        Assert.Equal(OperationStatus.Success, first.Status);
        Assert.Equal(OperationStatus.NotFound, second.Status);
        Assert.Equal(0, unit.OccupiedPlaces);
    }

    [Fact]
    public async Task ReleaseSlotAsync_SinLugaresOcupados_NuncaQuedaNegativo()
    {
        var owner = PublicationServiceTestData.Adult("owner-release@uni.edu");
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);

        var first = await units.ReleaseSlotAsync(unit.Id, unit.PropertyId);
        var second = await units.ReleaseSlotAsync(unit.Id, unit.PropertyId);

        Assert.Equal(0, first);
        Assert.Equal(0, second);
        Assert.Equal(0, unit.OccupiedPlaces);
    }

    [Fact]
    public async Task LeaveAsync_SinConfirmacion_DebeDevolverValidationFailed()
    {
        var units = new FakeUnitRepository();
        var links = new FakeOccupancyLinkRepository();
        var service = new LeaveUnitService(
            units, links, new UnitAvailabilityRecalculator(units), new FixedClock(Now));

        var result = await service.LeaveAsync(Guid.NewGuid(), Guid.NewGuid(), confirmacion: false);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.Contains("confirmacion", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task LeaveAsync_SinVinculoActivo_DebeDevolverNotFound()
    {
        var owner = PublicationServiceTestData.Adult();
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        var service = new LeaveUnitService(
            units, links, new UnitAvailabilityRecalculator(units), new FixedClock(Now));

        var result = await service.LeaveAsync(Guid.NewGuid(), unit.Id, confirmacion: true);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task LeaveAsync_UnidadInexistente_DebeDevolverNotFound()
    {
        var units = new FakeUnitRepository();
        var links = new FakeOccupancyLinkRepository();
        var service = new LeaveUnitService(
            units, links, new UnitAvailabilityRecalculator(units), new FixedClock(Now));

        var result = await service.LeaveAsync(Guid.NewGuid(), Guid.NewGuid(), confirmacion: true);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }
}
