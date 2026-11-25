using UniHome.Domain.Rentals;

namespace UniHome.Application.Rentals.Payments;

/// <summary>EP-M10 (HU-056): estado de pago de un inquilino en un periodo.</summary>
public sealed record TenantPaymentView(
    Guid TenantAccountId,
    decimal CuotaMxn,
    decimal PagadoMxn,
    decimal SaldoMxn,
    RentPaymentState Estado);
