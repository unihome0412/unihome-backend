using UniHome.Application.Common;
using UniHome.Application.Favorites.Abstractions;
using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Units;

namespace UniHome.Application.Exploration;

/// <summary>
/// EP-M04-04 (HU-044/HU-045): ficha completa autenticada, con allowlist de
/// campos y sin correo/teléfono del arrendador (HU-071).
/// </summary>
public sealed class GetUnitDetailService
{
    private readonly IUnitRepository _units;
    private readonly IPropertyRepository _properties;
    private readonly IUniversityRepository _universities;
    private readonly IUnitFavoriteRepository _favorites;
    private readonly UnitPhotoUrlResolver _photos;

    public GetUnitDetailService(
        IUnitRepository units,
        IPropertyRepository properties,
        IUniversityRepository universities,
        IUnitFavoriteRepository favorites,
        UnitPhotoUrlResolver photos)
    {
        _units = units;
        _properties = properties;
        _universities = universities;
        _favorites = favorites;
        _photos = photos;
    }

    public async Task<OperationResult<UnitDetailView>> GetAsync(
        Guid unitId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null || !ExplorationProjection.IsPublic(unit))
        {
            return OperationResult<UnitDetailView>.NotFound();
        }

        var property = await _properties
            .FindByIdAsync(unit.PropertyId, cancellationToken)
            .ConfigureAwait(false);

        if (property is null)
        {
            return OperationResult<UnitDetailView>.NotFound();
        }

        var location = await ExplorationProjection
            .LoadLocationAsync(_universities, unit.UniversityId, cancellationToken)
            .ConfigureAwait(false);
        var favorite = await _favorites
            .ExistsAsync(accountId, unitId, cancellationToken)
            .ConfigureAwait(false);
        var distance = ExplorationProjection.Distance(property, location);

        var included = unit.Services
            .Where(service => service.Mode == ServiceMode.Incluido)
            .Select(service => service.Name.ToString())
            .ToArray();
        var separate = unit.Services
            .Where(service => service.Mode == ServiceMode.Aparte)
            .Select(service => new UnitServiceView(
                service.Name.ToString(),
                service.Amount!.Value))
            .ToArray();

        return OperationResult<UnitDetailView>.Success(new UnitDetailView(
            unit.Id,
            _photos.Primary(unit),
            _photos.Gallery(unit),
            unit.PricePerPlace,
            unit.Type,
            distance,
            ExplorationProjection.TravelMode(distance),
            unit.ForWhom,
            ArrendadorVerificado: false,
            favorite,
            unit.Occupancy,
            unit.Places,
            unit.SizeM2,
            unit.Bathroom,
            unit.Furniture,
            included,
            separate,
            property.PropertyType));
    }
}
