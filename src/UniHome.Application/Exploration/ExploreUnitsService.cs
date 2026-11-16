using UniHome.Application.Common;
using UniHome.Application.Favorites.Abstractions;
using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;

namespace UniHome.Application.Exploration;

/// <summary>
/// EP-M04-01/02 (HU-037..043, HU-047, HU-084): mapa y lista públicos. Solo
/// unidades Aprobadas y Disponibles; la distancia es Haversine y el modo de
/// traslado se deriva del umbral de 1.5 km, sin inventar minutos ni rutas.
/// </summary>
public sealed class ExploreUnitsService
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private readonly IUniversityRepository _universities;
    private readonly IUnitRepository _units;
    private readonly IPropertyRepository _properties;
    private readonly IUnitFavoriteRepository _favorites;
    private readonly UnitPhotoUrlResolver _photos;

    public ExploreUnitsService(
        IUniversityRepository universities,
        IUnitRepository units,
        IPropertyRepository properties,
        IUnitFavoriteRepository favorites,
        UnitPhotoUrlResolver photos)
    {
        _universities = universities;
        _units = units;
        _properties = properties;
        _favorites = favorites;
        _photos = photos;
    }

    public async Task<OperationResult<IReadOnlyList<UnitMapView>>> MapAsync(
        ExplorationQuery query,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateFilters(query);

        if (errors.Count > 0)
        {
            return OperationResult<IReadOnlyList<UnitMapView>>.ValidationFailed(errors);
        }

        var university = await _universities
            .FindByIdAsync(query.UniversidadId, cancellationToken)
            .ConfigureAwait(false);

        if (university is null || !university.IsActive)
        {
            return OperationResult<IReadOnlyList<UnitMapView>>.NotFound();
        }

        var location = await ExplorationProjection
            .LoadLocationAsync(_universities, query.UniversidadId, cancellationToken)
            .ConfigureAwait(false);

        if (query.DistanciaMaxKm is not null && location is null)
        {
            return OperationResult<IReadOnlyList<UnitMapView>>.ValidationFailed(
                "distanciaMaxKm",
                "La universidad no tiene ubicación registrada para calcular distancias.");
        }

        var candidates = await LoadCandidatesAsync(query, location, cancellationToken)
            .ConfigureAwait(false);
        var map = candidates
            .Where(candidate => candidate.Property.Latitude is not null &&
                                candidate.Property.Longitude is not null)
            .Select(candidate => new UnitMapView(
                candidate.Unit.Id,
                candidate.Property.Latitude!.Value,
                candidate.Property.Longitude!.Value,
                candidate.Unit.PricePerPlace))
            .ToArray();

        return OperationResult<IReadOnlyList<UnitMapView>>.Success(map);
    }

    public async Task<OperationResult<ExplorationPage<UnitCardView>>> ListAsync(
        ExplorationQuery query,
        Guid? accountId,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateFilters(query);

        if (query.Page < 1)
        {
            errors["page"] = ["La página debe ser mayor o igual que 1."];
        }

        if (query.PageSize < 1 || query.PageSize > MaxPageSize)
        {
            errors["pageSize"] =
            [
                $"El tamaño de página debe estar entre 1 y {MaxPageSize}."
            ];
        }

        if (errors.Count > 0)
        {
            return OperationResult<ExplorationPage<UnitCardView>>.ValidationFailed(errors);
        }

        var university = await _universities
            .FindByIdAsync(query.UniversidadId, cancellationToken)
            .ConfigureAwait(false);

        if (university is null || !university.IsActive)
        {
            return OperationResult<ExplorationPage<UnitCardView>>.NotFound();
        }

        var location = await ExplorationProjection
            .LoadLocationAsync(_universities, query.UniversidadId, cancellationToken)
            .ConfigureAwait(false);

        if (query.DistanciaMaxKm is not null && location is null)
        {
            return OperationResult<ExplorationPage<UnitCardView>>.ValidationFailed(
                "distanciaMaxKm",
                "La universidad no tiene ubicación registrada para calcular distancias.");
        }

        var candidates = await LoadCandidatesAsync(query, location, cancellationToken)
            .ConfigureAwait(false);
        var ordered = candidates
            .OrderBy(candidate => candidate.DistanceKm is null)
            .ThenBy(candidate => candidate.DistanceKm)
            .ThenBy(candidate => candidate.Unit.PricePerPlace)
            .ThenBy(candidate => candidate.Unit.Id)
            .ToArray();

        var pageItems = ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();
        var favoriteIds = accountId is null || pageItems.Length == 0
            ? new HashSet<Guid>()
            : await _favorites
                .GetFavoriteUnitIdsAsync(
                    accountId.Value,
                    pageItems.Select(candidate => candidate.Unit.Id).ToArray(),
                    cancellationToken)
                .ConfigureAwait(false);

        var cards = pageItems
            .Select(candidate => ExplorationProjection.BuildCard(
                candidate.Unit,
                candidate.DistanceKm,
                accountId is null ? null : favoriteIds.Contains(candidate.Unit.Id),
                _photos))
            .ToArray();

        return OperationResult<ExplorationPage<UnitCardView>>.Success(
            new ExplorationPage<UnitCardView>(cards, ordered.LongLength));
    }

    private async Task<IReadOnlyList<Candidate>> LoadCandidatesAsync(
        ExplorationQuery query,
        GeoCoordinate? location,
        CancellationToken cancellationToken)
    {
        var units = await _units
            .ListPublicCandidatesAsync(
                query.UniversidadId,
                query.PrecioMaxMxn,
                query.SoloIndividual == true,
                query.ParaQuien,
                cancellationToken)
            .ConfigureAwait(false);
        var properties = await ExplorationProjection
            .LoadPropertiesAsync(_properties, units, cancellationToken)
            .ConfigureAwait(false);
        var candidates = new List<Candidate>(units.Count);

        foreach (var unit in units)
        {
            if (!properties.TryGetValue(unit.PropertyId, out var property))
            {
                continue;
            }

            var distance = ExplorationProjection.Distance(property, location);

            if (query.DistanciaMaxKm is not null &&
                (distance is null || distance > query.DistanciaMaxKm))
            {
                continue;
            }

            candidates.Add(new Candidate(unit, property, distance));
        }

        return candidates;
    }

    private static Dictionary<string, string[]> ValidateFilters(ExplorationQuery query)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (query.UniversidadId == Guid.Empty)
        {
            errors["universidadId"] = ["La universidad es obligatoria."];
        }

        if (query.DistanciaMaxKm is <= 0)
        {
            errors["distanciaMaxKm"] = ["La distancia máxima debe ser mayor que cero."];
        }

        if (query.PrecioMaxMxn is <= 0)
        {
            errors["precioMaxMxn"] = ["El precio máximo debe ser mayor que cero."];
        }

        return errors;
    }

    private sealed record Candidate(
        Unit Unit,
        Property Property,
        decimal? DistanceKm);

}
