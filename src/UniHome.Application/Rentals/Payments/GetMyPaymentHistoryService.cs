using UniHome.Application.Common;
using UniHome.Application.Rentals.Abstractions;
using UniHome.Application.Units.Abstractions;

namespace UniHome.Application.Rentals.Payments;

/// <summary>
/// EP-M10-06 (HU-058): el historial de pagos informativos propios de una
/// unidad, a través de todos los periodos. A diferencia de HU-057, no exige
/// vínculo activo: el inquilino sigue viendo lo que se le registró aunque ya
/// haya dejado la unidad. Solo filtra por la propia cuenta, nunca expone
/// pagos de otro inquilino.
/// </summary>
public sealed class GetMyPaymentHistoryService
{
    private readonly IUnitRepository _units;
    private readonly IRentPeriodRepository _rentPeriods;
    private readonly IInformativePaymentRepository _payments;

    public GetMyPaymentHistoryService(
        IUnitRepository units,
        IRentPeriodRepository rentPeriods,
        IInformativePaymentRepository payments)
    {
        _units = units;
        _rentPeriods = rentPeriods;
        _payments = payments;
    }

    public async Task<OperationResult<IReadOnlyList<PaymentHistoryItemView>>> GetAsync(
        Guid tenantAccountId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        var unit = await _units.FindByIdAsync(unitId, cancellationToken).ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<IReadOnlyList<PaymentHistoryItemView>>.NotFound();
        }

        var periods = await _rentPeriods
            .ListByUnitAsync(unitId, cancellationToken)
            .ConfigureAwait(false);

        var items = new List<PaymentHistoryItemView>();

        foreach (var period in periods)
        {
            var payments = await _payments
                .ListByRentPeriodAsync(period.Id, cancellationToken)
                .ConfigureAwait(false);

            items.AddRange(payments
                .Where(payment => payment.TenantAccountId == tenantAccountId)
                .Select(payment => new PaymentHistoryItemView(
                    period.Periodo, payment.AmountMxn, payment.ReceivedOn, payment.RegisteredAt)));
        }

        var ordered = items
            .OrderByDescending(item => item.Periodo, StringComparer.Ordinal)
            .ThenByDescending(item => item.FechaRecepcion)
            .ToArray();

        return OperationResult<IReadOnlyList<PaymentHistoryItemView>>.Success(ordered);
    }
}
