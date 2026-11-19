using UniHome.Application.Properties.MyProperties;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class GetMyPropertiesServiceTests
{
    private static readonly DateTimeOffset Now = PublicationServiceTestData.Now;

    [Fact]
    public async Task ListAsync_AgrupaPorPropiedadYCuentaDisponibles()
    {
        var ownerId = Guid.NewGuid();
        var universityId = Guid.NewGuid();
        var properties = new FakePropertyRepository();
        var property = PublicationServiceTestData.CreateProperty(ownerId, universityId);
        properties.Items.Add(property);

        var units = new FakeUnitRepository();
        units.Items.Add(RestoreUnit(property.Id, ownerId, universityId, UnitAvailability.Disponible));
        units.Items.Add(RestoreUnit(property.Id, ownerId, universityId, UnitAvailability.Mantenimiento));

        var service = new GetMyPropertiesService(properties, units);
        var result = await service.ListAsync(ownerId, null, null);

        var view = Assert.Single(result);
        Assert.Equal(property.Id, view.Id);
        Assert.Equal(2, view.TotalHabitaciones);
        Assert.Equal(1, view.HabitacionesDisponibles);
        Assert.Equal(2, view.Habitaciones.Count);
    }

    [Fact]
    public async Task ListAsync_SinHabitaciones_DevuelveCerosYListaVacia()
    {
        var ownerId = Guid.NewGuid();
        var properties = new FakePropertyRepository();
        var property = PublicationServiceTestData.CreateProperty(ownerId, Guid.NewGuid());
        properties.Items.Add(property);

        var service = new GetMyPropertiesService(properties, new FakeUnitRepository());
        var result = await service.ListAsync(ownerId, null, null);

        var view = Assert.Single(result);
        Assert.Equal(0, view.TotalHabitaciones);
        Assert.Equal(0, view.HabitacionesDisponibles);
        Assert.Empty(view.Habitaciones);
    }

    [Fact]
    public async Task ListAsync_FiltraPorTipoPropiedad()
    {
        var ownerId = Guid.NewGuid();
        var universityId = Guid.NewGuid();
        var properties = new FakePropertyRepository();
        var house = Property.Create(
            ownerId, "Casa", "Calle 1", "Centro", PropertyType.Casa,
            universityId, 19.0, -99.0, null, null, Now);
        var building = Property.Create(
            ownerId, "Depa", "Calle 2", "Centro", PropertyType.Departamento,
            universityId, 19.0, -99.0, null, null, Now);
        properties.Items.Add(house);
        properties.Items.Add(building);

        var service = new GetMyPropertiesService(properties, new FakeUnitRepository());
        var result = await service.ListAsync(ownerId, PropertyType.Departamento, null);

        var view = Assert.Single(result);
        Assert.Equal(building.Id, view.Id);
    }

    [Fact]
    public async Task ListAsync_FiltraPorEstadoHabitacion()
    {
        var ownerId = Guid.NewGuid();
        var universityId = Guid.NewGuid();
        var properties = new FakePropertyRepository();
        var withMaintenance = PublicationServiceTestData.CreateProperty(ownerId, universityId);
        properties.Items.Add(withMaintenance);

        var units = new FakeUnitRepository();
        units.Items.Add(
            RestoreUnit(withMaintenance.Id, ownerId, universityId, UnitAvailability.Mantenimiento));

        var service = new GetMyPropertiesService(properties, units);

        var matching = await service.ListAsync(ownerId, null, UnitAvailability.Mantenimiento);
        Assert.Single(matching);

        var nonMatching = await service.ListAsync(ownerId, null, UnitAvailability.Disponible);
        Assert.Empty(nonMatching);
    }

    [Fact]
    public async Task ListAsync_SoloDevuelvePropiedadesDelTitular()
    {
        var ownerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        var universityId = Guid.NewGuid();
        var properties = new FakePropertyRepository();
        properties.Items.Add(PublicationServiceTestData.CreateProperty(otherOwnerId, universityId));

        var service = new GetMyPropertiesService(properties, new FakeUnitRepository());
        var result = await service.ListAsync(ownerId, null, null);

        Assert.Empty(result);
    }

    private static Unit RestoreUnit(
        Guid propertyId,
        Guid ownerId,
        Guid universityId,
        UnitAvailability availability) =>
        Unit.Restore(
            Guid.NewGuid(),
            propertyId,
            ownerId,
            universityId,
            UnitType.Habitacion,
            Occupancy.Individual,
            1,
            3500,
            12,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.Amueblado,
            furnitureItems: ["Cama"],
            template: null,
            zoneInfo: null,
            rules: null,
            services: null,
            photos: null,
            PublicationState.Borrador,
            Now,
            availability);
}
