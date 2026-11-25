using UniHome.Application.Common;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.Rentals.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Rentals;

namespace UniHome.Application.Rentals.Payments;

/// <summary>
/// EP-M10-05 (HU-057): el inquilino consulta su propia cuota del periodo
/// vigente de una unidad, con lo que lleva pagado y su saldo. Exige vínculo
/// activo (a diferencia del historial de HU-058, que sigue visible aunque el
/// inquilino ya haya dejado la unidad) y una división ya aplicada.
/// </summary>
public sealed class GetMyRentShareService
{
    private readonly IUnitRepository _units;
    private readonly IOccupancyLinkRepository _links;
    private readonly IInformativePaymentRepository _payments;
    private readonly RentPeriodResolver _resolver;

    public GetMyRentShareService(
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

    public async Task<OperationResult<MyRentShareView>> GetAsync(
        Guid tenantAccountId,
        Guid unitId,
        string periodo,
        CancellationToken cancellationToken = default)
    {
        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<MyRentShareView>.NotFound();
        }

        var activeLinks = await _links
            .ListActiveByUnitAsync(unitId, cancellationToken)
            .ConfigureAwait(false);

        if (!activeLinks.Any(link => link.TenantAccountId == tenantAccountId))
        {
            return OperationResult<MyRentShareView>.Forbidden();
        }

        var period = await _resolver.ResolveAsync(unitId, periodo, cancellationToken).ConfigureAwait(false);

        if (period is null)
        {
            return OperationResult<MyRentShareView>.NotFound();
        }

        if (period.IsSplitPending)
        {
            return OperationResult<MyRentShareView>.Unprocessable();
        }

        var share = period.FindShare(tenantAccountId);

        if (share is null)
        {
            return OperationResult<MyRentShareView>.NotFound();
        }

        var payments = await _payments
            .ListByRentPeriodAsync(period.Id, cancellationToken)
            .ConfigureAwait(false);
        var paid = payments
            .Where(payment => payment.TenantAccountId == tenantAccountId)
            .Sum(payment => payment.AmountMxn);
        var state = RentPeriod.ComputePaymentState(share.AmountMxn, paid);

        return OperationResult<MyRentShareView>.Success(
            new MyRentShareView(period, share.AmountMxn, paid, paid - share.AmountMxn, state));
    }
}
