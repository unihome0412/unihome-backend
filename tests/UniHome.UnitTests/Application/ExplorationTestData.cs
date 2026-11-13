using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.Domain.Universities;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

internal static class ExplorationTestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);
    public static readonly Guid UniversityId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static FakeUniversityRepository UniversityRepository()
    {
        var repository = new FakeUniversityRepository
        {
            Location = new GeoCoordinate(19.0, -99.0)
        };
        repository.Items.Add(University.Restore(
            UniversityId,
            "Universidad de prueba",
            true,
            Now));
        return repository;
    }

    public static Property CreateProperty(
        Guid ownerId,
        string name,
        double latitude,
        double longitude) =>
        Property.Create(
            ownerId,
            name,
            $"Calle {name}",
            "Centro",
            PropertyType.Casa,
            UniversityId,
            latitude,
            longitude,
            null,
            null,
            Now);

    public static Unit CreateUnit(
        Guid ownerId,
        Guid propertyId,
        decimal price,
        Occupancy occupancy = Occupancy.Individual,
        ForWhom forWhom = ForWhom.Mixto,
        PublicationState state = PublicationState.Aprobada,
        UnitAvailability availability = UnitAvailability.Disponible,
        bool withPhoto = true,
        bool withServices = true)
    {
        var unit = Unit.Restore(
            Guid.NewGuid(),
            propertyId,
            ownerId,
            UniversityId,
            UnitType.Habitacion,
            occupancy,
            occupancy == Occupancy.Individual ? 1 : 3,
            price,
            12,
            forWhom,
            BathroomType.Privado,
            FurnitureType.Amueblado,
            ["Cama"],
            null,
            "Cerca",
            "No fumar",
            withServices
                ?
                [
                    ServiceCharge.Create(ServiceName.Agua, ServiceMode.Incluido, null),
                    ServiceCharge.Create(ServiceName.Internet, ServiceMode.Aparte, 300)
                ]
                : [],
            withPhoto
                ?
                [
                    new UnitPhoto("foto-1", "image/png", 10, "hash-1", 0, true, Now),
                    new UnitPhoto("foto-2", "image/png", 10, "hash-2", 1, false, Now)
                ]
                : [],
            state,
            Now,
            availability);
        return unit;
    }
}
