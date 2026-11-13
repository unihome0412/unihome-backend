using UniHome.Application.Common;
using UniHome.Application.Exploration;
using UniHome.Application.Favorites.Abstractions;
using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Universities.Abstractions;

namespace UniHome.Application.Favorites;

/// <summary>
/// EP-M07-03 (HU-085): favoritos propios paginados. La unidad permanece aunque
/// deje de ser pública y la tarjeta refleja su estado actual.
/// </summary>
public sealed class ListFavoritesService
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private readonly IUnitFavoriteRepository _favorites;
    private readonly IUnitRepository _units;
    private readonly IPropertyRepository _properties;
    private readonly IUniversityRepository _universities;
    private readonly UnitPhotoUrlResolver _photos;

    public ListFavoritesService(
        IUnitFavoriteRepository favorites,
        IUnitRepository units,
        IPropertyRepository properties,
        IUniversityRepository universities,
        UnitPhotoUrlResolver photos)
    {
        _favorites = favorites;
        _units = units;
        _properties = properties;
        _universities = universities;
        _photos = photos;
    }

    public async Task<OperationResult<ExplorationPage<FavoriteView>>> ListAsync(
        Guid accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (page < 1)
        {
            errors["page"] = ["La página debe ser mayor o igual que 1."];
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            errors["pageSize"] =
            [
                $"El tamaño de página debe estar entre 1 y {MaxPageSize}."
            ];
        }

        if (errors.Count > 0)
        {
            return OperationResult<ExplorationPage<FavoriteView>>.ValidationFailed(errors);
        }

        var favorites = await _favorites
            .ListAsync(accountId, page, pageSize, cancellationToken)
            .ConfigureAwait(false);

        if (favorites.Items.Count == 0)
        {
            return OperationResult<ExplorationPage<FavoriteView>>.Success(
                new ExplorationPage<FavoriteView>([], favorites.Total));
        }

        var units = await _units
            .FindByIdsAsync(
                favorites.Items.Select(favorite => favorite.UnitId).ToArray(),
                cancellationToken)
            .ConfigureAwait(false);
        var unitById = units.ToDictionary(unit => unit.Id);
        var properties = await _properties
            .FindByIdsAsync(
                units.Select(unit => unit.PropertyId).Distinct().ToArray(),
                cancellationToken)
            .ConfigureAwait(false);
        var propertyById = properties.ToDictionary(property => property.Id);
        var locationByUniversity = new Dictionary<Guid, GeoCoordinate?>();

        foreach (var universityId in units.Select(unit => unit.UniversityId).Distinct())
        {
            locationByUniversity[universityId] = await _universities
                .GetLocationAsync(universityId, cancellationToken)
                .ConfigureAwait(false);
        }

        var items = new List<FavoriteView>(favorites.Items.Count);

        foreach (var favorite in favorites.Items)
        {
            if (!unitById.TryGetValue(favorite.UnitId, out var unit) ||
                !propertyById.TryGetValue(unit.PropertyId, out var property))
            {
                continue;
            }

            locationByUniversity.TryGetValue(unit.UniversityId, out var location);
            var distance = ExplorationProjection.Distance(property, location);

            items.Add(new FavoriteView(
                unit.Id,
                new FavoriteCardView(
                    unit.Id,
                    _photos.Primary(unit),
                    unit.PricePerPlace,
                    unit.Type,
                    distance,
                    ExplorationProjection.TravelMode(distance),
                    unit.ForWhom,
                    ArrendadorVerificado: false,
                    EsFavorito: true,
                    ExplorationProjection.CurrentState(unit))));
        }

        return OperationResult<ExplorationPage<FavoriteView>>.Success(
            new ExplorationPage<FavoriteView>(items, favorites.Total));
    }
}
