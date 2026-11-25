using UniHome.Domain.Rentals;

namespace UniHome.Application.Rentals.Payments;

/// <summary>EP-M10 (HU-057): la cuota del inquilino en un periodo, con lo pagado y su saldo.</summary>
public sealed record MyRentShareView(RentPeriod Period, decimal CuotaMxn, decimal PagadoMxn, decimal SaldoMxn, RentPaymentState Estado);
