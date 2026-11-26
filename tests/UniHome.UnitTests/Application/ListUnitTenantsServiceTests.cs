using UniHome.Application.Common;
using UniHome.Application.OccupancyLinks.ListTenants;
using UniHome.Domain.OccupancyLinks;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

/// <summary>
/// Cierra el hueco de cobertura unitaria detectado en
/// specs/M09-ocupacion/get-unidades-unitId-inquilinos.md (Bloque 1 de la
/// auditoría): hasta ahora <see cref="ListUnitTenantsService"/> solo tenía
/// prueba de integración.
/// </summary>
public sealed class ListUnitTenantsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListAsync_UnidadPropia_DevuelveSoloInquilinosActivosSinCorreoNiTelefono()
    {
        var context = BuildContext(out var owner, out var unit);
        var activeTenant = AccountTestData.Create("inquilino.activo@uni.edu");
        var leftTenant = AccountTestData.Create("inquilino.baja@uni.edu");
        await context.Accounts.AddAsync(activeTenant);
        await context.Accounts.AddAsync(leftTenant);
        context.Links.Items.Add(OccupancyLink.CreateActive(activeTenant.Id, unit.Id, Now));
        var ended = OccupancyLink.CreateActive(leftTenant.Id, unit.Id, Now.AddDays(-5));
        ended.End(OccupancyEndReason.AbandonoInquilino, Now.AddDays(-1));
        context.Links.Items.Add(ended);

        var result = await context.Service.ListAsync(owner.Id, unit.Id);

        Assert.Equal(OperationStatus.Success, result.Status);
        var tenant = Assert.Single(result.Value!);
        Assert.Equal(activeTenant.Id, tenant.AccountId);
        Assert.Equal(activeTenant.FullName, tenant.NombreCompleto);
        Assert.Equal(Now, tenant.FechaVinculo);
    }

    [Fact]
    public async Task ListAsync_UnidadAjena_DebeDevolverForbidden()
    {
        var context = BuildContext(out _, out var unit);
        var otherOwner = AccountTestData.Create("otro.propietario@uni.edu");
        await context.Accounts.AddAsync(otherOwner);

        var result = await context.Service.ListAsync(otherOwner.Id, unit.Id);

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task ListAsync_UnidadInexistente_DebeDevolverNotFound()
    {
        var context = BuildContext(out var owner, out _);

        var result = await context.Service.ListAsync(owner.Id, Guid.NewGuid());

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ListAsync_LlamanteNoElegible_DebeDevolverForbidden()
    {
        var properties = new FakePropertyRepository();
        var units = new FakeUnitRepository();
        var accounts = new InMemoryAccountRepository();
        var links = new FakeOccupancyLinkRepository();

        var owner = PublicationServiceTestData.Minor("propietario.menor@uni.edu");
        var property = ExplorationTestData.CreateProperty(owner.Id, "Casa", 19.0, -99.0);
        var unit = ExplorationTestData.CreateUnit(owner.Id, property.Id, 3000);
        properties.Items.Add(property);
        units.Items.Add(unit);
        var eligibility = PublicationServiceTestData.Eligibility(accounts, owner);
        var service = new ListUnitTenantsService(units, links, accounts, eligibility);

        var result = await service.ListAsync(owner.Id, unit.Id);

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    private static Context BuildContext(out UniHome.Domain.Accounts.Account owner, out UniHome.Domain.Units.Unit unit)
    {
        var properties = new FakePropertyRepository();
        var units = new FakeUnitRepository();
        var accounts = new InMemoryAccountRepository();
        var links = new FakeOccupancyLinkRepository();

        owner = PublicationServiceTestData.Adult("propietario@uni.edu");
        var property = ExplorationTestData.CreateProperty(owner.Id, "Casa", 19.0, -99.0);
        properties.Items.Add(property);
        unit = ExplorationTestData.CreateUnit(owner.Id, property.Id, 3000);
        units.Items.Add(unit);

        var eligibility = PublicationServiceTestData.Eligibility(accounts, owner);
        var service = new ListUnitTenantsService(units, links, accounts, eligibility);

        return new Context(service, accounts, links);
    }

    private sealed record Context(
        ListUnitTenantsService Service,
        InMemoryAccountRepository Accounts,
        FakeOccupancyLinkRepository Links);
}
