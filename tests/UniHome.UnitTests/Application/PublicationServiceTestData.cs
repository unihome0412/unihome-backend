using UniHome.Application.Profiles;
using UniHome.Domain.Accounts;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Application;

internal static class PublicationServiceTestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    public static PublishingEligibilityService Eligibility(
        InMemoryAccountRepository accounts,
        Account account)
    {
        accounts.AddAsync(account).GetAwaiter().GetResult();
        return new PublishingEligibilityService(accounts, new FixedClock(Now));
    }

    public static Account Adult(string email = "ana@uni.edu") =>
        AccountTestData.Create(email);

    public static Account Minor(string email = "menor@uni.edu") =>
        AccountTestData.Create(email, birthDate: new DateOnly(2010, 1, 1));

    public static Property CreateProperty(Guid ownerId, Guid universityId) =>
        Property.Create(
            ownerId,
            "Casa Centro",
            "Av. Juárez 100",
            "Centro",
            PropertyType.Casa,
            universityId,
            19.4326,
            -99.1332,
            floor: null,
            unitNumber: null,
            Now);

    public static Unit UnitFor(Guid property, Guid ownerId, Guid universityId)
    {
        var unit = Unit.Create(
            property,
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
            ["Cama"],
            template: null,
            Now);
        return unit;
    }

    public static byte[] PngBytes() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFElEQVR42mP4z8DAAMb//0MZUA4Aep8J9x7TI9wAAAAASUVORK5CYII=");
}
