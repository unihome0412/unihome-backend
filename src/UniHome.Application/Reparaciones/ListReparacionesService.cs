using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.Reparaciones.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Reparaciones;

namespace UniHome.Application.Reparaciones;

/// <summary>
/// EP-M11-02 (HU-064): reportes de reparación de una unidad propia o donde la
/// cuenta tiene vínculo activo, con el nombre de quien lo levantó (nunca su
/// correo ni teléfono).
/// </summary>
public sealed class ListReparacionesService
{
    private readonly IUnitRepository _units;
    private readonly IOccupancyLinkRepository _links;
    private readonly IReparacionRepository _reparaciones;
    private readonly IAccountRepository _accounts;

    public ListReparacionesService(
        IUnitRepository units,
        IOccupancyLinkRepository links,
        IReparacionRepository reparaciones,
        IAccountRepository accounts)
    {
        _units = units;
        _links = links;
        _reparaciones = reparaciones;
        _accounts = accounts;
    }

    public async Task<OperationResult<IReadOnlyList<ReparacionView>>> ListAsync(
        Guid accountId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<IReadOnlyList<ReparacionView>>.NotFound();
        }

        var hasAccess = accountId == unit.OwnerAccountId;

        if (!hasAccess)
        {
            var link = await _links.FindAsync(accountId, unitId, cancellationToken).ConfigureAwait(false);
            hasAccess = link is not null && link.Status == OccupancyStatus.Activo;
        }

        if (!hasAccess)
        {
            return OperationResult<IReadOnlyList<ReparacionView>>.Forbidden();
        }

        var reparaciones = await _reparaciones
            .ListByUnitAsync(unitId, cancellationToken)
            .ConfigureAwait(false);

        var views = new List<ReparacionView>(reparaciones.Count);

        foreach (var reparacion in reparaciones)
        {
            var reporter = await _accounts
                .FindByIdAsync(reparacion.ReportedByAccountId, cancellationToken)
                .ConfigureAwait(false);

            views.Add(new ReparacionView(reparacion, reporter?.FullName ?? "Cuenta no disponible"));
        }

        return OperationResult<IReadOnlyList<ReparacionView>>.Success(views);
    }
}

public sealed record ReparacionView(Reparacion Reparacion, string ReportedByFullName);
