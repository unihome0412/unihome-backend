using UniHome.Application.Common;
using UniHome.Application.Units.Detail;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class GetUnitDetailForOwnerServiceTests
{
    [Fact]
    public async Task GetAsync_TitularDeLaUnidad_DevuelveLaUnidad()
    {
        var owner = PublicationServiceTestData.Adult();
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var service = new GetUnitDetailForOwnerService(units);

        var result = await service.GetAsync(owner.Id, unit.Id);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(unit.Id, result.Value!.Id);
    }

    [Fact]
    public async Task GetAsync_UnidadAjena_DebeDevolverForbidden()
    {
        var owner = PublicationServiceTestData.Adult();
        var property = PublicationServiceTestData.CreateProperty(owner.Id, Guid.NewGuid());
        var unit = PublicationServiceTestData.UnitFor(property.Id, owner.Id, property.UniversityId);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var service = new GetUnitDetailForOwnerService(units);

        var result = await service.GetAsync(Guid.NewGuid(), unit.Id);

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task GetAsync_UnidadInexistente_DebeDevolverNotFound()
    {
        var units = new FakeUnitRepository();
        var service = new GetUnitDetailForOwnerService(units);

        var result = await service.GetAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(OperationStatus.NotFound, result.Status);
    }
}
