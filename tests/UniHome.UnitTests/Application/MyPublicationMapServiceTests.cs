using UniHome.Application.Common;
using UniHome.Application.Publications.MyPublicationMap;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class MyPublicationMapServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EnRevision_DebeDevolverPinPrivadoConEtiqueta()
    {
        var owner = Guid.NewGuid();
        var property = ExplorationTestData.CreateProperty(owner, "Casa", 19.4326, -99.1332);
        var unit = ExplorationTestData.CreateUnit(
            owner,
            property.Id,
            3000,
            state: PublicationState.EnRevision);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var properties = new FakePropertyRepository();
        properties.Items.Add(property);
        var service = new GetMyPublicationMapService(units, properties);

        var result = await service.GetAsync(owner);

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(unit.Id, result.Value!.Id);
        Assert.Equal(19.4326, result.Value.Latitud);
        Assert.Equal("Tu espacio · En revisión", result.Value.Etiqueta);
        Assert.Equal(PublicationState.EnRevision, result.Value.EstadoPublicacion);
    }

    [Fact]
    public async Task AprobadaODuenoAjeno_DebeDevolverNotFound()
    {
        var owner = Guid.NewGuid();
        var property = ExplorationTestData.CreateProperty(owner, "Casa", 19.4326, -99.1332);
        var unit = ExplorationTestData.CreateUnit(
            owner,
            property.Id,
            3000,
            state: PublicationState.Aprobada);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var properties = new FakePropertyRepository();
        properties.Items.Add(property);
        var service = new GetMyPublicationMapService(units, properties);

        var approved = await service.GetAsync(owner);
        var otherUser = await service.GetAsync(Guid.NewGuid());

        Assert.Equal(OperationStatus.NotFound, approved.Status);
        Assert.Equal(OperationStatus.NotFound, otherUser.Status);
    }

    [Fact]
    public async Task SinUbicacion_DebeSerUnprocessable()
    {
        var owner = Guid.NewGuid();
        var property = Property.Create(
            owner,
            "Casa",
            "Calle",
            "Centro",
            PropertyType.Casa,
            Guid.NewGuid(),
            latitude: null,
            longitude: null,
            null,
            null,
            Now);
        var unit = ExplorationTestData.CreateUnit(
            owner,
            property.Id,
            3000,
            state: PublicationState.EnRevision);
        var units = new FakeUnitRepository();
        units.Items.Add(unit);
        var properties = new FakePropertyRepository();
        properties.Items.Add(property);
        var service = new GetMyPublicationMapService(units, properties);

        var result = await service.GetAsync(owner);

        Assert.Equal(OperationStatus.Unprocessable, result.Status);
    }
}
