using UniHome.Domain.Rentals;

namespace UniHome.Application.Rentals.Payments;

/// <summary>EP-M10 (HU-056): periodo resuelto junto con el estado de pago de cada inquilino.</summary>
public sealed record RentPaymentsSummary(RentPeriod Period, IReadOnlyList<TenantPaymentView> Tenants);
