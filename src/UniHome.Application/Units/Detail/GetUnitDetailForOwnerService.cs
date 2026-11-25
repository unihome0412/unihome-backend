using UniHome.Application.Common;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Units;

namespace UniHome.Application.Units.Detail;

/// <summary>
/// EP-M05-07 (HU-035): detalle completo de una unidad propia, para la
/// pantalla de administración a la que el titular llega desde HU-033
/// ("mis-propiedades"). A diferencia de HU-080 (vista previa, una tarjeta),
/// expone todos los campos de la unidad, en cualquier estado de publicación.
/// </summary>
public sealed class GetUnitDetailForOwnerService
{
    private readonly IUnitRepository _units;

    public GetUnitDetailForOwnerService(IUnitRepository units)
    {
        _units = units;
    }

    public async Task<OperationResult<Unit>> GetAsync(
        Guid ownerAccountId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<Unit>.NotFound();
        }

        if (unit.OwnerAccountId != ownerAccountId)
        {
            return OperationResult<Unit>.Forbidden();
        }

        return OperationResult<Unit>.Success(unit);
    }
}
