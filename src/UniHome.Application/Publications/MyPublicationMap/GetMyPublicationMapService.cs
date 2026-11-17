using UniHome.Application.Common;
using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;

namespace UniHome.Application.Publications.MyPublicationMap;

/// <summary>
/// EP-M05-10 (HU-028): pin privado de la primera publicación En revisión. Solo
/// el propietario autenticado puede obtenerlo; no se expone en M04.
/// </summary>
public sealed class GetMyPublicationMapService
{
    public const string InReviewLabel = "Tu espacio · En revisión";

    private readonly IUnitRepository _units;
    private readonly IPropertyRepository _properties;

    public GetMyPublicationMapService(
        IUnitRepository units,
        IPropertyRepository properties)
    {
        _units = units;
        _properties = properties;
    }

    public async Task<OperationResult<MyPublicationMapView>> GetAsync(
        Guid ownerAccountId,
        CancellationToken cancellationToken = default)
    {
        var unit = await _units
            .FindFirstOwnedAsync(ownerAccountId, cancellationToken)
            .ConfigureAwait(false);

        if (unit is null || unit.PublicationState != PublicationState.EnRevision)
        {
            return OperationResult<MyPublicationMapView>.NotFound();
        }

        var property = await _properties
            .FindByIdAsync(unit.PropertyId, cancellationToken)
            .ConfigureAwait(false);

        if (property is null)
        {
            return OperationResult<MyPublicationMapView>.NotFound();
        }

        if (property.Latitude is null || property.Longitude is null)
        {
            return OperationResult<MyPublicationMapView>.Unprocessable();
        }

        return OperationResult<MyPublicationMapView>.Success(
            new MyPublicationMapView(
                unit.Id,
                property.Latitude.Value,
                property.Longitude.Value,
                unit.PricePerPlace,
                unit.Type,
                InReviewLabel,
                unit.PublicationState));
    }
}

public sealed record MyPublicationMapView(
    Guid Id,
    double Latitud,
    double Longitud,
    decimal PrecioMensualMxn,
    UnitType TipoUnidad,
    string Etiqueta,
    PublicationState EstadoPublicacion);
