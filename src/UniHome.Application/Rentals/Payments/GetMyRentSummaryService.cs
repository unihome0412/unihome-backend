using UniHome.Application.Common;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.Rentals.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Rentals;

namespace UniHome.Application.Rentals.Payments;

/// <summary>
/// EP-M10-07 (HU-060/061): resumen mensual de renta de la cuenta para un
/// periodo, combinando ambos roles en una sola consulta: como titular (por
/// cada unidad propia con renta configurada en el periodo, lo recaudado y lo
/// pendiente, sin el desglose por inquilino de HU-056) y como inquilino (por
/// cada unidad donde la cuenta tiene vínculo activo, su propia cuota, lo
/// pagado y su saldo, igual que HU-057). Una cuenta sin unidades propias ni
/// vínculos activos recibe ambas listas vacías, nunca un error: es "mi
/// resumen", no el recurso de una unidad.
/// </summary>
public sealed class GetMyRentSummaryService
{
    private readonly IUnitRepository _units;
    private readonly IOccupancyLinkRepository _links;
    private readonly IInformativePaymentRepository _payments;
    private readonly RentPeriodResolver _resolver;

    public GetMyRentSummaryService(
        IUnitRepository units,
        IOccupancyLinkRepository links,
        IInformativePaymentRepository payments,
        RentPeriodResolver resolver)
    {
        _units = units;
        _links = links;
        _payments = payments;
        _resolver = resolver;
    }

    public async Task<OperationResult<MyRentSummaryView>> GetAsync(
        Guid accountId,
        string periodo,
        CancellationToken cancellationToken = default)
    {
        if (!RentPeriod.IsValidPeriodo(periodo))
        {
            return OperationResult<MyRentSummaryView>.ValidationFailed(
                "periodo", "El periodo debe tener el formato \"yyyy-MM\".");
        }

        var ownedUnits = await _units.ListByOwnerAsync(accountId, cancellationToken).ConfigureAwait(false);
        var ownerItems = new List<OwnerRentSummaryItemView>();

        foreach (var unit in ownedUnits)
        {
            var period = await _resolver.ResolveAsync(unit.Id, periodo, cancellationToken).ConfigureAwait(false);

            if (period is null)
            {
                continue;
            }

            var recaudado = (await _payments
                .ListByRentPeriodAsync(period.Id, cancellationToken)
                .ConfigureAwait(false))
                .Sum(payment => payment.AmountMxn);

            ownerItems.Add(new OwnerRentSummaryItemView(
                unit.Id, period.TotalMxn, recaudado, period.TotalMxn - recaudado, period.IsSplitPending));
        }

        var activeLinks = await _links
            .ListActiveByTenantAsync(accountId, cancellationToken)
            .ConfigureAwait(false);
        var tenantItems = new List<TenantRentSummaryItemView>();

        foreach (var link in activeLinks)
        {
            var period = await _resolver.ResolveAsync(link.UnitId, periodo, cancellationToken).ConfigureAwait(false);
            var share = period?.FindShare(accountId);

            if (period is null || share is null)
            {
                continue;
            }

            var paid = (await _payments
                .ListByRentPeriodAsync(period.Id, cancellationToken)
                .ConfigureAwait(false))
                .Where(payment => payment.TenantAccountId == accountId)
                .Sum(payment => payment.AmountMxn);
            var state = RentPeriod.ComputePaymentState(share.AmountMxn, paid);

            tenantItems.Add(new TenantRentSummaryItemView(link.UnitId, share.AmountMxn, paid, paid - share.AmountMxn, state));
        }

        return OperationResult<MyRentSummaryView>.Success(
            new MyRentSummaryView(periodo, ownerItems, tenantItems));
    }
}
