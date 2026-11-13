using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Common;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;

namespace UniHome.Application.Exploration;

public static class ExplorationProjection
{
    public const decimal WalkingThresholdKm = 1.5m;

    public static decimal? Distance(Property property, GeoCoordinate? universityLocation)
    {
        if (property.Latitude is null || property.Longitude is null ||
            universityLocation is null || !universityLocation.IsValid)
        {
            return null;
        }

        var distance = GeoDistance.Kilometers(
            property.Latitude.Value,
            property.Longitude.Value,
            universityLocation.Latitude,
            universityLocation.Longitude);

        return Math.Round((decimal)distance, 2, MidpointRounding.AwayFromZero);
    }

    public static string? TravelMode(decimal? distanceKm) => distanceKm switch
    {
        null => null,
        <= WalkingThresholdKm => "a pie",
        _ => "en transporte"
    };

    public static string CurrentState(Unit unit) =>
        unit.PublicationState == PublicationState.Aprobada
            ? unit.Availability.ToString()
            : unit.PublicationState.ToString();

    public static bool IsPublic(Unit unit) =>
        unit.PublicationState == PublicationState.Aprobada &&
        unit.Availability == UnitAvailability.Disponible;

    public static UnitCardView BuildCard(
        Unit unit,
        decimal? distanceKm,
        bool? isFavorite,
        UnitPhotoUrlResolver photos) =>
        new(
            unit.Id,
            photos.Primary(unit),
            unit.PricePerPlace,
            unit.Type,
            distanceKm,
            TravelMode(distanceKm),
            unit.ForWhom,
            ArrendadorVerificado: false,
            isFavorite);

    public static async Task<Dictionary<Guid, Property>> LoadPropertiesAsync(
        IPropertyRepository properties,
        IEnumerable<Unit> units,
        CancellationToken cancellationToken)
    {
        var ids = units
            .Select(unit => unit.PropertyId)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        var items = await properties
            .FindByIdsAsync(ids, cancellationToken)
            .ConfigureAwait(false);
        return items.ToDictionary(property => property.Id);
    }

    public static async Task<GeoCoordinate?> LoadLocationAsync(
        IUniversityRepository universities,
        Guid universityId,
        CancellationToken cancellationToken) =>
        await universities
            .GetLocationAsync(universityId, cancellationToken)
            .ConfigureAwait(false);
}
