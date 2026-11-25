using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.Profiles;
using UniHome.Application.Rentals.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Rentals;

namespace UniHome.Application.Rentals.Split;

/// <summary>
/// EP-M10-02 (HU-053/HU-054): divide la renta de un periodo (existente o
/// heredado vía <see cref="RentPeriodResolver"/>) entre los inquilinos activos
/// de la unidad. Igual reparte en partes iguales; Personalizada usa las
/// cuotas de la solicitud, que deben cubrir exactamente la renta total y
/// corresponder a inquilinos con vínculo activo.
/// </summary>
public sealed class SplitRentPeriodService
{
    private readonly IUnitRepository _units;
    private readonly IOccupancyLinkRepository _links;
    private readonly IRentPeriodRepository _rentPeriods;
    private readonly RentPeriodResolver _resolver;
    private readonly PublishingEligibilityService _eligibility;
    private readonly ISystemClock _clock;

    public SplitRentPeriodService(
        IUnitRepository units,
        IOccupancyLinkRepository links,
        IRentPeriodRepository rentPeriods,
        RentPeriodResolver resolver,
        PublishingEligibilityService eligibility,
        ISystemClock clock)
    {
        _units = units;
        _links = links;
        _rentPeriods = rentPeriods;
        _resolver = resolver;
        _eligibility = eligibility;
        _clock = clock;
    }

    public async Task<OperationResult<RentPeriod>> SplitAsync(
        Guid ownerAccountId,
        Guid unitId,
        string periodo,
        RentSplitMethod method,
        IReadOnlyList<TenantShare>? customShares,
        CancellationToken cancellationToken = default)
    {
        if (!await _eligibility.IsEligibleAsync(ownerAccountId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<RentPeriod>.Forbidden();
        }

        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<RentPeriod>.NotFound();
        }

        if (unit.OwnerAccountId != ownerAccountId)
        {
            return OperationResult<RentPeriod>.Forbidden();
        }

        var period = await _resolver.ResolveAsync(unitId, periodo, cancellationToken).ConfigureAwait(false);

        if (period is null)
        {
            return OperationResult<RentPeriod>.NotFound();
        }

        var activeLinks = await _links
            .ListActiveByUnitAsync(unitId, cancellationToken)
            .ConfigureAwait(false);
        var activeTenantIds = activeLinks.Select(link => link.TenantAccountId).ToHashSet();
        var now = _clock.UtcNow;

        try
        {
            if (method == RentSplitMethod.Igual)
            {
                period.ApplyEqualSplit(activeTenantIds, now);
            }
            else
            {
                if (customShares is null || customShares.Count == 0)
                {
                    return OperationResult<RentPeriod>.ValidationFailed(
                        "cuotas",
                        "Se necesita al menos una cuota para la división personalizada.");
                }

                if (customShares.Any(share => !activeTenantIds.Contains(share.TenantAccountId)))
                {
                    return OperationResult<RentPeriod>.ValidationFailed(
                        "cuotas",
                        "Cada cuota debe corresponder a un inquilino con vínculo activo en la unidad.");
                }

                period.ApplyCustomSplit(customShares, now);
            }
        }
        catch (ArgumentException exception)
        {
            return OperationResult<RentPeriod>.ValidationFailed("cuotas", exception.Message);
        }

        await _rentPeriods.SaveAsync(period, cancellationToken).ConfigureAwait(false);
        return OperationResult<RentPeriod>.Success(period);
    }
}
