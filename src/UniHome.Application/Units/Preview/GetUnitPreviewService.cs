using UniHome.Application.Common;
using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Common;

namespace UniHome.Application.Units.Preview;

/// <summary>
/// EP-M05-06 (HU-080): vista previa propiedad del titular. La distancia solo se
/// calcula con coordenadas reales; el tiempo de traslado queda nulo porque no
/// existe un motor de rutas aprobado.
/// </summary>
public sealed class GetUnitPreviewService
{
    private readonly IUnitRepository _units;
    private readonly IPropertyRepository _properties;
    private readonly IUniversityRepository _universities;
    private readonly IUnitPhotoStorage _storage;

    public GetUnitPreviewService(
        IUnitRepository units,
        IPropertyRepository properties,
        IUniversityRepository universities,
        IUnitPhotoStorage storage)
    {
        _units = units;
        _properties = properties;
        _universities = universities;
        _storage = storage;
    }

    public async Task<OperationResult<UnitPreviewView>> GetAsync(
        Guid ownerAccountId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<UnitPreviewView>.NotFound();
        }

        if (unit.OwnerAccountId != ownerAccountId)
        {
            return OperationResult<UnitPreviewView>.Forbidden();
        }

        var property = await _properties
            .FindByIdAsync(unit.PropertyId, cancellationToken)
            .ConfigureAwait(false);

        if (property is null)
        {
            return OperationResult<UnitPreviewView>.NotFound();
        }

        var primaryPhoto = unit.Photos.FirstOrDefault(photo => photo.IsPrimary) ??
                           unit.Photos.FirstOrDefault();
        var photoUrl = primaryPhoto is null
            ? null
            : _storage.GetReadUrl(primaryPhoto.StorageKey);

        decimal? distanceKm = null;

        if (property.Latitude is not null && property.Longitude is not null)
        {
            var location = await _universities
                .GetLocationAsync(property.UniversityId, cancellationToken)
                .ConfigureAwait(false);

            if (location is not null && location.IsValid)
            {
                var distance = GeoDistance.Kilometers(
                    property.Latitude.Value,
                    property.Longitude.Value,
                    location.Latitude,
                    location.Longitude);
                distanceKm = Math.Round(
                    (decimal)distance,
                    2,
                    MidpointRounding.AwayFromZero);
            }
        }

        return OperationResult<UnitPreviewView>.Success(new UnitPreviewView(
            unit.Id,
            photoUrl,
            unit.PricePerPlace,
            unit.Type,
            distanceKm,
            TiempoTraslado: null,
            unit.ForWhom,
            ArrendadorVerificado: false,
            EsFavorito: null,
            unit.PublicationState));
    }
}
