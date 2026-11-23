using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.Profiles;
using UniHome.Application.Units.Abstractions;

namespace UniHome.Application.OccupancyLinks.ListTenants;

/// <summary>
/// EP-M09-06 (HU-094): inquilinos activos de una unidad propia, con nombre y
/// fecha de vínculo. Nunca expone correo ni teléfono; es lo que permite al
/// propietario obtener el <c>tenantAccountId</c> para dar de baja a alguien.
/// </summary>
public sealed class ListUnitTenantsService
{
    private readonly IUnitRepository _units;
    private readonly IOccupancyLinkRepository _links;
    private readonly IAccountRepository _accounts;
    private readonly PublishingEligibilityService _eligibility;

    public ListUnitTenantsService(
        IUnitRepository units,
        IOccupancyLinkRepository links,
        IAccountRepository accounts,
        PublishingEligibilityService eligibility)
    {
        _units = units;
        _links = links;
        _accounts = accounts;
        _eligibility = eligibility;
    }

    public async Task<OperationResult<IReadOnlyList<UnitTenantView>>> ListAsync(
        Guid ownerAccountId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        if (!await _eligibility.IsEligibleAsync(ownerAccountId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<IReadOnlyList<UnitTenantView>>.Forbidden();
        }

        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<IReadOnlyList<UnitTenantView>>.NotFound();
        }

        if (unit.OwnerAccountId != ownerAccountId)
        {
            return OperationResult<IReadOnlyList<UnitTenantView>>.Forbidden();
        }

        var activeLinks = await _links
            .ListActiveByUnitAsync(unitId, cancellationToken)
            .ConfigureAwait(false);
        var views = new List<UnitTenantView>(activeLinks.Count);

        foreach (var link in activeLinks)
        {
            var tenant = await _accounts
                .FindByIdAsync(link.TenantAccountId, cancellationToken)
                .ConfigureAwait(false);

            if (tenant is null)
            {
                continue;
            }

            views.Add(new UnitTenantView(tenant.Id, tenant.FullName, link.JoinedAt));
        }

        return OperationResult<IReadOnlyList<UnitTenantView>>.Success(views);
    }
}

public sealed record UnitTenantView(Guid AccountId, string NombreCompleto, DateTimeOffset FechaVinculo);
