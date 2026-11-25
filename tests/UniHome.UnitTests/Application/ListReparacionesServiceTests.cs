using UniHome.Application.Common;
using UniHome.Application.Reparaciones;
using UniHome.Domain.Accounts;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class ListReparacionesServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListAsync_ComoPropietario_DevuelveReportesConNombreDeQuienLosLevanto()
    {
        var units = new FakeUnitRepository();
        var unit = CreateUnit(out var ownerId);
        units.Items.Add(unit);
        var accounts = new InMemoryAccountRepository();
        var reporter = AccountTestData.Create("reportante@uni.edu");
        await accounts.AddAsync(reporter);
        var reparaciones = new FakeReparacionRepository();
        reparaciones.Items.Add(UniHome.Domain.Reparaciones.Reparacion.Create(
            unit.Id, reporter.Id, "Fuga", "Hay una fuga", null, Now));
        var service = new ListReparacionesService(
            units, new FakeOccupancyLinkRepository(), reparaciones, accounts);

        var result = await service.ListAsync(ownerId, unit.Id);

        Assert.Equal(OperationStatus.Success, result.Status);
        var item = Assert.Single(result.Value!);
        Assert.Equal("Ana López", item.ReportedByFullName);
    }

    [Fact]
    public async Task ListAsync_ComoInquilinoActivo_DebePermitirAcceso()
    {
        var units = new FakeUnitRepository();
        var unit = CreateUnit(out _);
        units.Items.Add(unit);
        var links = new FakeOccupancyLinkRepository();
        var tenantId = Guid.NewGuid();
        links.Items.Add(OccupancyLink.CreateActive(tenantId, unit.Id, Now));
        var service = new ListReparacionesService(
            units, links, new FakeReparacionRepository(), new InMemoryAccountRepository());

        var result = await service.ListAsync(tenantId, unit.Id);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task ListAsync_UnidadInexistente_DevuelveNotFound()
    {
        var service = new ListReparacionesService(
            new FakeUnitRepository(),
            new FakeOccupancyLinkRepository(),
            new FakeReparacionRepository(),
            new InMemoryAccountRepository());

        var result = await service.ListAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ListAsync_CuentaSinAccesoALaUnidad_DevuelveForbidden()
    {
        var units = new FakeUnitRepository();
        var unit = CreateUnit(out _);
        units.Items.Add(unit);
        var service = new ListReparacionesService(
            units, new FakeOccupancyLinkRepository(), new FakeReparacionRepository(), new InMemoryAccountRepository());

        var result = await service.ListAsync(Guid.NewGuid(), unit.Id);

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task ListAsync_CuentaReportanteDesconocida_UsaTextoPorDefecto()
    {
        var units = new FakeUnitRepository();
        var unit = CreateUnit(out var ownerId);
        units.Items.Add(unit);
        var reparaciones = new FakeReparacionRepository();
        reparaciones.Items.Add(UniHome.Domain.Reparaciones.Reparacion.Create(
            unit.Id, Guid.NewGuid(), "Fuga", "Hay una fuga", null, Now));
        var service = new ListReparacionesService(
            units, new FakeOccupancyLinkRepository(), reparaciones, new InMemoryAccountRepository());

        var result = await service.ListAsync(ownerId, unit.Id);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("Cuenta no disponible", Assert.Single(result.Value!).ReportedByFullName);
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
