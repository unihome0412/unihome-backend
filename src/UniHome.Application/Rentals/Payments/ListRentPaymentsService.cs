using UniHome.Application.Common;
using UniHome.Application.Profiles;
using UniHome.Application.Rentals.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Rentals;

namespace UniHome.Application.Rentals.Payments;

/// <summary>
/// EP-M10-04 (HU-056): para cada inquilino con cuota asignada, resume cuánto
/// se le ha registrado, su saldo y su estado (pendiente/parcial/registrado).
/// Exige una división aplicada: sin cuotas no hay con qué comparar los pagos.
/// </summary>
public sealed class ListRentPaymentsService
{
    private readonly IUnitRepository _units;
    private readonly IInformativePaymentRepository _payments;
    private readonly RentPeriodResolver _resolver;
    private readonly PublishingEligibilityService _eligibility;

    public ListRentPaymentsService(
        IUnitRepository units,
        IInformativePaymentRepository payments,
        RentPeriodResolver resolver,
        PublishingEligibilityService eligibility)
    {
        _units = units;
        _payments = payments;
        _resolver = resolver;
        _eligibility = eligibility;
    }

    public async Task<OperationResult<RentPaymentsSummary>> ListAsync(
        Guid ownerAccountId,
        Guid unitId,
        string periodo,
        CancellationToken cancellationToken = default)
    {
        if (!await _eligibility.IsEligibleAsync(ownerAccountId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<RentPaymentsSummary>.Forbidden();
        }

        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<RentPaymentsSummary>.NotFound();
        }

        if (unit.OwnerAccountId != ownerAccountId)
        {
            return OperationResult<RentPaymentsSummary>.Forbidden();
        }

        var period = await _resolver.ResolveAsync(unitId, periodo, cancellationToken).ConfigureAwait(false);

        if (period is null)
        {
            return OperationResult<RentPaymentsSummary>.NotFound();
        }

        if (period.IsSplitPending)
        {
            return OperationResult<RentPaymentsSummary>.Unprocessable();
        }

        var payments = await _payments
            .ListByRentPeriodAsync(period.Id, cancellationToken)
            .ConfigureAwait(false);

        var tenants = period.Shares
            .Select(share =>
            {
                var paid = payments
                    .Where(payment => payment.TenantAccountId == share.TenantAccountId)
                    .Sum(payment => payment.AmountMxn);
                var state = RentPeriod.ComputePaymentState(share.AmountMxn, paid);
                return new TenantPaymentView(
                    share.TenantAccountId, share.AmountMxn, paid, paid - share.AmountMxn, state);
            })
            .ToArray();

        return OperationResult<RentPaymentsSummary>.Success(new RentPaymentsSummary(period, tenants));
    }
}
