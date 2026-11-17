using UniHome.Application.Common;
using UniHome.Application.Favorites.Abstractions;
using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Universities.Abstractions;
using UniHome.Application.Verification.Abstractions;
using UniHome.Domain.Verification;

namespace UniHome.Application.Exploration;

/// <summary>
/// EP-M04-03 (HU-038/HU-046/HU-047): tarjeta pública de una unidad Aprobada y
/// Disponible. No expone correo, teléfono ni datos del propietario.
/// </summary>
public sealed class GetUnitCardService
{
    private readonly IUnitRepository _units;
    private readonly IPropertyRepository _properties;
    private readonly IUniversityRepository _universities;
    private readonly IUnitFavoriteRepository _favorites;
    private readonly UnitPhotoUrlResolver _photos;
    private readonly IVerificationRepository _verification;

    public GetUnitCardService(
        IUnitRepository units,
        IPropertyRepository properties,
        IUniversityRepository universities,
        IUnitFavoriteRepository favorites,
        UnitPhotoUrlResolver photos,
        IVerificationRepository verification)
    {
        _units = units;
        _properties = properties;
        _universities = universities;
        _favorites = favorites;
        _photos = photos;
        _verification = verification;
    }

    public async Task<OperationResult<UnitCardView>> GetAsync(
        Guid unitId,
        Guid? accountId,
        CancellationToken cancellationToken = default)
    {
        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null || !ExplorationProjection.IsPublic(unit))
        {
            return OperationResult<UnitCardView>.NotFound();
        }

        var property = await _properties
            .FindByIdAsync(unit.PropertyId, cancellationToken)
            .ConfigureAwait(false);

        if (property is null)
        {
            return OperationResult<UnitCardView>.NotFound();
        }

        var location = await ExplorationProjection
            .LoadLocationAsync(_universities, unit.UniversityId, cancellationToken)
            .ConfigureAwait(false);
        var favorite = accountId is not null &&
                       await _favorites
                           .ExistsAsync(accountId.Value, unitId, cancellationToken)
                           .ConfigureAwait(false);
        var verification = await _verification
            .FindByOwnerAsync(unit.OwnerAccountId, cancellationToken)
            .ConfigureAwait(false);
        var verified = verification?.State == EstadoExpediente.Aprobado;

        return OperationResult<UnitCardView>.Success(
            ExplorationProjection.BuildCard(
                unit,
                ExplorationProjection.Distance(property, location),
                accountId is null ? null : favorite,
                _photos,
                verified: verified));
    }
}
