using UniHome.Application.Common;
using UniHome.Application.OccupancyLinks;
using UniHome.Application.OccupancyLinks.SetMaintenance;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class SetUnitMaintenanceServiceTests
{
    private static readonly DateTimeOffset Now = PublicationServiceTestData.Now;

    [Fact]
    public async Task SetAsync_Activa_MarcaMantenimientoSinImportarOcupacion()
    {
        var accounts = new InMemoryAccountRepository();
        var owner = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, owner);
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        var service = new SetUnitMaintenanceService(
            units, new UnitAvailabilityRecalculator(units, links), eligibility);

        var result = await service.SetAsync(owner.Id, unit.Id, enMantenimiento: true);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(UnitAvailability.Mantenimiento, result.Value!.Availability);
    }

    [Fact]
    public async Task SetAsync_Desactiva_RecalculaDesdeVinculosActivos()
    {
        var accounts = new InMemoryAccountRepository();
        var owner = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, owner);
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        unit.SetAvailability(UnitAvailability.Mantenimiento);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        links.Items.Add(OccupancyLink.CreateActive(Guid.NewGuid(), unit.Id, Now));
        var service = new SetUnitMaintenanceService(
            units, new UnitAvailabilityRecalculator(units, links), eligibility);

        var result = await service.SetAsync(owner.Id, unit.Id, enMantenimiento: false);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(UnitAvailability.Ocupado, result.Value!.Availability);
    }

    [Fact]
    public async Task SetAsync_Desactiva_SinVinculos_QuedaDisponible()
    {
        var accounts = new InMemoryAccountRepository();
        var owner = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, owner);
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        unit.SetAvailability(UnitAvailability.Mantenimiento);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        var service = new SetUnitMaintenanceService(
            units, new UnitAvailabilityRecalculator(units, links), eligibility);

        var result = await service.SetAsync(owner.Id, unit.Id, enMantenimiento: false);

        Assert.Equal(UnitAvailability.Disponible, result.Value!.Availability);
    }

    [Fact]
    public async Task SetAsync_UnidadAjena_DebeDevolverForbidden()
    {
        var accounts = new InMemoryAccountRepository();
        var caller = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, caller);
        var property = PublicationServiceTestData.CreateProperty(Guid.NewGuid(), Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(
            property.Id, property.OwnerAccountId, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        var service = new SetUnitMaintenanceService(
            units, new UnitAvailabilityRecalculator(units, links), eligibility);

        var result = await service.SetAsync(caller.Id, unit.Id, enMantenimiento: true);

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task SetAsync_UnidadInexistente_DebeDevolverNotFound()
    {
        var accounts = new InMemoryAccountRepository();
        var owner = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, owner);
        var units = new FakeUnitRepository();
        var links = new FakeOccupancyLinkRepository();
        var service = new SetUnitMaintenanceService(
            units, new UnitAvailabilityRecalculator(units, links), eligibility);

        var result = await service.SetAsync(owner.Id, Guid.NewGuid(), enMantenimiento: true);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }
}
