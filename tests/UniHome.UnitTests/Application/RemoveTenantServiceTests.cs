using UniHome.Application.Common;
using UniHome.Application.OccupancyLinks;
using UniHome.Application.OccupancyLinks.RemoveTenant;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class RemoveTenantServiceTests
{
    private static readonly DateTimeOffset Now = PublicationServiceTestData.Now;

    [Fact]
    public async Task RemoveAsync_ConConfirmacion_FinalizaVinculoYLiberaLugar()
    {
        var accounts = new InMemoryAccountRepository();
        var owner = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, owner);
        var tenant = Guid.NewGuid();
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        unit.SetAvailability(UnitAvailability.Ocupado);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        links.Items.Add(OccupancyLink.CreateActive(tenant, unit.Id, Now));
        var service = new RemoveTenantService(
            units, links, new UnitAvailabilityRecalculator(units, links), eligibility, new FixedClock(Now));

        var result = await service.RemoveAsync(owner.Id, unit.Id, tenant, confirmacion: true);

        Assert.Equal(OperationStatus.Success, result.Status);
        var link = await links.FindAsync(tenant, unit.Id);
        Assert.Equal(OccupancyStatus.Finalizado, link!.Status);
        Assert.Equal(OccupancyEndReason.BajaPorPropietario, link.EndedReason);
        Assert.Equal(UnitAvailability.Disponible, unit.Availability);
    }

    [Fact]
    public async Task RemoveAsync_SinConfirmacion_DebeDevolverValidationFailed()
    {
        var accounts = new InMemoryAccountRepository();
        var owner = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, owner);
        var units = new FakeUnitRepository();
        var links = new FakeOccupancyLinkRepository();
        var service = new RemoveTenantService(
            units, links, new UnitAvailabilityRecalculator(units, links), eligibility, new FixedClock(Now));

        var result = await service.RemoveAsync(owner.Id, Guid.NewGuid(), Guid.NewGuid(), confirmacion: false);

        Assert.Equal(OperationStatus.ValidationFailed, result.Status);
        Assert.Contains("confirmacion", result.ValidationErrors.Keys);
    }

    [Fact]
    public async Task RemoveAsync_UnidadAjena_DebeDevolverForbidden()
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
        var service = new RemoveTenantService(
            units, links, new UnitAvailabilityRecalculator(units, links), eligibility, new FixedClock(Now));

        var result = await service.RemoveAsync(caller.Id, unit.Id, Guid.NewGuid(), confirmacion: true);

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task RemoveAsync_InquilinoNoVinculado_DebeDevolverNotFound()
    {
        var accounts = new InMemoryAccountRepository();
        var owner = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, owner);
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        var service = new RemoveTenantService(
            units, links, new UnitAvailabilityRecalculator(units, links), eligibility, new FixedClock(Now));

        var result = await service.RemoveAsync(owner.Id, unit.Id, Guid.NewGuid(), confirmacion: true);

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task RemoveAsync_UnidadEnMantenimiento_NoCambiaDisponibilidad()
    {
        var accounts = new InMemoryAccountRepository();
        var owner = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, owner);
        var tenant = Guid.NewGuid();
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        unit.SetAvailability(UnitAvailability.Mantenimiento);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        links.Items.Add(OccupancyLink.CreateActive(tenant, unit.Id, Now));
        var service = new RemoveTenantService(
            units, links, new UnitAvailabilityRecalculator(units, links), eligibility, new FixedClock(Now));

        await service.RemoveAsync(owner.Id, unit.Id, tenant, confirmacion: true);

        Assert.Equal(UnitAvailability.Mantenimiento, unit.Availability);
    }
}
