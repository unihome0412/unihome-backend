using UniHome.Domain.Common;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;

namespace UniHome.UnitTests.Domain;

public sealed class PropertyAndUnitTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Property_EdificioSinPisoNumero_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() => Property.Create(
            Guid.NewGuid(),
            "Edificio Centro",
            "Av. Juárez 100",
            "Centro",
            PropertyType.Edificio,
            Guid.NewGuid(),
            19.43,
            -99.13,
            floor: null,
            unitNumber: null,
            Now));
    }

    [Fact]
    public void Property_CasaIgnoraPisoYNumero()
    {
        var property = Property.Create(
            Guid.NewGuid(),
            "Casa Centro",
            "Av. Juárez 100",
            "Centro",
            PropertyType.Casa,
            Guid.NewGuid(),
            null,
            null,
            "4",
            "402",
            Now);

        Assert.Null(property.Floor);
        Assert.Null(property.UnitNumber);
        Assert.Equal(PublicationState.Borrador, property.PublicationState);
    }

    [Fact]
    public void Unit_IndividualFuerzaUnLugar()
    {
        var unit = Unit.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            UnitType.Habitacion,
            Occupancy.Individual,
            places: 4,
            3500,
            12,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.Amueblado,
            ["Cama"],
            template: null,
            Now);

        Assert.Equal(1, unit.Places);
    }

    [Fact]
    public void Unit_CompartidaConservaLugares()
    {
        var unit = Unit.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            UnitType.Habitacion,
            Occupancy.Compartida,
            places: 3,
            2500,
            15,
            ForWhom.SoloMujeres,
            BathroomType.Compartido,
            FurnitureType.Parcial,
            ["Cama", "Escritorio"],
            template: null,
            Now);

        Assert.Equal(3, unit.Places);
        Assert.Equal(["Cama", "Escritorio"], unit.FurnitureItems);
    }

    [Fact]
    public void Unit_SinMueblesIgnoraLista()
    {
        var unit = Unit.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            UnitType.Estudio,
            Occupancy.Individual,
            1,
            4000,
            20,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.SinMuebles,
            ["Cama", "Sofá"],
            template: null,
            Now);

        Assert.Empty(unit.FurnitureItems);
    }

    [Fact]
    public void ServiceCharge_AparteSinMonto_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() =>
            ServiceCharge.Create(ServiceName.Internet, ServiceMode.Aparte, null));
    }

    [Fact]
    public void ServiceCharge_IncluidoNoConservaMonto()
    {
        var service = ServiceCharge.Create(ServiceName.Agua, ServiceMode.Incluido, 100);

        Assert.Null(service.Amount);
    }

    [Fact]
    public void Unit_NoPermiteServiciosDuplicados()
    {
        var unit = Unit.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            UnitType.Habitacion,
            Occupancy.Individual,
            1,
            3500,
            12,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.Amueblado,
            [],
            null,
            Now);

        Assert.Throws<ArgumentException>(() => unit.ConfigureServices(
        [
            ServiceCharge.Create(ServiceName.Internet, ServiceMode.Incluido, null),
            ServiceCharge.Create(ServiceName.Internet, ServiceMode.Aparte, 300)
        ]));
    }

    [Fact]
    public void GeoDistance_DebeCalcularDistanciaConocida()
    {
        var distance = GeoDistance.Kilometers(19.4326, -99.1332, 19.4326, -99.1332);

        Assert.Equal(0, distance, 3);
    }
}
