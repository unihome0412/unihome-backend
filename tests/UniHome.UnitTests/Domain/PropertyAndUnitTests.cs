using UniHome.Domain.Accounts;
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

    [Theory]
    [InlineData(UnitTemplate.HabitacionIndividual, UnitType.Habitacion, Occupancy.Individual, 1)]
    [InlineData(UnitTemplate.HabitacionCompartida, UnitType.Habitacion, Occupancy.Compartida, 2)]
    [InlineData(UnitTemplate.DepartamentoCompleto, UnitType.Departamento, Occupancy.Individual, 1)]
    [InlineData(UnitTemplate.Estudio, UnitType.Estudio, Occupancy.Individual, 1)]
    public void UnitTemplates_DebenRellenarValoresTipicos(
        UnitTemplate template,
        UnitType type,
        Occupancy occupancy,
        int places)
    {
        var defaults = UnitTemplates.Get(template);

        Assert.Equal(type, defaults.Type);
        Assert.Equal(occupancy, defaults.Occupancy);
        Assert.Equal(places, defaults.Places);
        Assert.Equal(ForWhom.Mixto, defaults.ForWhom);
        Assert.Equal(BathroomType.Privado, defaults.Bathroom);
        Assert.Equal(FurnitureType.Amueblado, defaults.Furniture);
    }

    [Fact]
    public void Property_ConUnaSolaCoordenada_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() => Property.Create(
            Guid.NewGuid(),
            "Casa",
            "Calle 1",
            "Centro",
            PropertyType.Casa,
            Guid.NewGuid(),
            latitude: 19.43,
            longitude: null,
            floor: null,
            unitNumber: null,
            Now));
    }

    [Fact]
    public void Property_ConCoordenadasFueraDeRango_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() => Property.Create(
            Guid.NewGuid(),
            "Casa",
            "Calle 1",
            "Centro",
            PropertyType.Casa,
            Guid.NewGuid(),
            latitude: 100,
            longitude: -99,
            floor: null,
            unitNumber: null,
            Now));
    }

    [Fact]
    public void Unit_CompartidaConUnLugar_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() => Unit.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            UnitType.Habitacion,
            Occupancy.Compartida,
            places: 1,
            3500,
            12,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.Amueblado,
            [],
            null,
            Now));
    }

    [Fact]
    public void Unit_ConMuebleFueraDelCatalogo_DebeRechazarse()
    {
        Assert.Throws<ArgumentException>(() => Unit.Create(
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
            ["Jacuzzi"],
            null,
            Now));
    }

    [Fact]
    public void Unit_NoDebeSuperarDiezFotos()
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
        var photos = Enumerable.Range(0, Unit.MaxPhotos + 1)
            .Select(index => new UnitPhoto(
                $"key-{index}", "image/png", 10, "hash", index, index == 0, Now))
            .ToArray();

        Assert.Throws<ArgumentException>(() => unit.AppendPhotos(photos));
    }

    [Fact]
    public void AgeEligibility_DebeRespetarElCumpleanosDelAnio()
    {
        Assert.False(AgeEligibility.IsAdult(
            new DateOnly(2008, 12, 1),
            new DateTimeOffset(2026, 11, 13, 0, 0, 0, TimeSpan.Zero)));
        Assert.True(AgeEligibility.IsAdult(
            new DateOnly(2008, 11, 13),
            new DateTimeOffset(2026, 11, 13, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void GeoDistance_DebeCalcularDistanciaConocida()
    {
        var distance = GeoDistance.Kilometers(19.4326, -99.1332, 19.4326, -99.1332);

        Assert.Equal(0, distance, 3);
    }
}
