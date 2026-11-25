using UniHome.Application.Rentals.Payments;
using UniHome.Domain.Rentals;

namespace UniHome.Api.Contracts.Rentals;

public sealed record EstadoInquilinoResponse(
    Guid InquilinoAccountId,
    decimal CuotaMxn,
    decimal PagadoMxn,
    decimal SaldoMxn,
    RentPaymentState Estado);

public sealed record EstadoPagosResponse(
    Guid UnitId,
    string Periodo,
    DateOnly FechaCorte,
    IReadOnlyList<EstadoInquilinoResponse> Inquilinos,
    string Aviso)
{
    public static EstadoPagosResponse FromSummary(RentPaymentsSummary summary) =>
        new(
            summary.Period.UnitId,
            summary.Period.Periodo,
            RentPeriod.ComputeCutoffDate(summary.Period.Periodo, summary.Period.CutoffDay),
            summary.Tenants
                .Select(tenant => new EstadoInquilinoResponse(
                    tenant.TenantAccountId, tenant.CuotaMxn, tenant.PagadoMxn, tenant.SaldoMxn, tenant.Estado))
                .ToArray(),
            RentNotices.PagosInformativos);
}
