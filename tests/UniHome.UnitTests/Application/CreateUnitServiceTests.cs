using UniHome.Application.Common;
using UniHome.Application.Units.CreateUnit;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

public sealed class CreateUnitServiceTests
{
    private static readonly DateTimeOffset Now = PublicationServiceTestData.Now;

    [Fact]
    public async Task Create_ConPlantilla_RellenaCamposOmitidosYFuerzaUnLugar()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var property = PublicationServiceTestData.CreateProperty(account.Id, Guid.NewGuid());
        var properties = new FakePropertyRepository();
        properties.Items.Add(property);
        var units = new FakeUnitRepository();
        var service = new CreateUnitService(units, properties, eligibility, new FixedClock(Now));

        var result = await service.CreateAsync(account.Id, property.Id, new CreateUnitCommand(
            TipoUnidad: UnitType.Habitacion,
            Ocupacion: Occupancy.Individual,
            Lugares: 4,
            PrecioMensualPorLugarMxn: 3500,
            TamanoM2: 12,
            ParaQuien: null,
            Bano: null,
            Mobiliario: null,
            Muebles: ["Cama"],
            PlantillaBase: UnitTemplate.HabitacionIndividual));

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal(1, result.Value!.Places);
        Assert.Equal(ForWhom.Mixto, result.Value.ForWhom);
        Assert.Equal(BathroomType.Privado, result.Value.Bathroom);
        Assert.Single(units.Items);
    }

    [Fact]
    public async Task Create_ConPropiedadAjena_DebeDevolverForbidden()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Adult();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var property = PublicationServiceTestData.CreateProperty(Guid.NewGuid(), Guid.NewGuid());
        var properties = new FakePropertyRepository();
        properties.Items.Add(property);
        var service = new CreateUnitService(
            new FakeUnitRepository(),
            properties,
            eligibility,
            new FixedClock(Now));

        var result = await service.CreateAsync(account.Id, property.Id, new CreateUnitCommand(
            UnitType.Habitacion, Occupancy.Individual, 1, 3000, 10,
            ForWhom.Mixto, BathroomType.Privado, FurnitureType.Amueblado,
            [], null));

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task Create_MenorDeEdad_DebeDevolverForbidden()
    {
        var accounts = new InMemoryAccountRepository();
        var account = PublicationServiceTestData.Minor();
        var eligibility = PublicationServiceTestData.Eligibility(accounts, account);
        var property = PublicationServiceTestData.CreateProperty(account.Id, Guid.NewGuid());
        var properties = new FakePropertyRepository();
        properties.Items.Add(property);
        var service = new CreateUnitService(
            new FakeUnitRepository(), properties, eligibility, new FixedClock(Now));

        var result = await service.CreateAsync(account.Id, property.Id, new CreateUnitCommand(
            UnitType.Habitacion, Occupancy.Individual, 1, 3000, 10,
            ForWhom.Mixto, BathroomType.Privado, FurnitureType.Amueblado,
            [], null));

        Assert.Equal(OperationStatus.Forbidden, result.Status);
    }
}
