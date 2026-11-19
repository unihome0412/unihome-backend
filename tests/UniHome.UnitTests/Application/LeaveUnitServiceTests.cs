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
        unit.SetAvailability(UnitAvailability.Ocupado);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        links.Items.Add(OccupancyLink.CreateActive(tenant, unit.Id, Now));
        var service = new LeaveUnitService(
            units, links, new UnitAvailabilityRecalculator(units, links), new FixedClock(Now));

        var result = await service.LeaveAsync(tenant, unit.Id, confirmacion: true);

        Assert.Equal(OperationStatus.Success, result.Status);
        var link = await links.FindAsync(tenant, unit.Id);
        Assert.Equal(OccupancyStatus.Finalizado, link!.Status);
        Assert.Equal(OccupancyEndReason.AbandonoInquilino, link.EndedReason);
        Assert.Equal(UnitAvailability.Disponible, unit.Availability);
    }

    [Fact]
    public async Task LeaveAsync_SinConfirmacion_DebeDevolverValidationFailed()
    {
        var units = new FakeUnitRepository();
        var links = new FakeOccupancyLinkRepository();
        var service = new LeaveUnitService(
            units, links, new UnitAvailabilityRecalculator(units, links), new FixedClock(Now));

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
            units, links, new UnitAvailabilityRecalculator(units, links), new FixedClock(Now));

        var result = await service.LeaveAsync(Guid.NewGuid(), unit.Id, confirmacion: true);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task LeaveAsync_UnidadInexistente_DebeDevolverNotFound()
    {
        var units = new FakeUnitRepository();
        var links = new FakeOccupancyLinkRepository();
        var service = new LeaveUnitService(
            units, links, new UnitAvailabilityRecalculator(units, links), new FixedClock(Now));

        var result = await service.LeaveAsync(Guid.NewGuid(), Guid.NewGuid(), confirmacion: true);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }
}
