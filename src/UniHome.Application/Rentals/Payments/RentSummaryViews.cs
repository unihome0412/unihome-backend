using UniHome.Domain.Rentals;

namespace UniHome.Application.Rentals.Payments;

/// <summary>EP-M10 (HU-060): resumen de una unidad propia en el periodo, sin desglose por inquilino (ver HU-056 para eso).</summary>
public sealed record OwnerRentSummaryItemView(
    Guid UnitId,
    decimal RentaTotalMxn,
    decimal RecaudadoMxn,
    decimal PendienteMxn,
    bool DivisionPendiente);

/// <summary>EP-M10 (HU-061): resumen de la propia cuota en una unidad donde la cuenta es inquilino activo.</summary>
public sealed record TenantRentSummaryItemView(
    Guid UnitId,
    decimal CuotaMxn,
    decimal PagadoMxn,
    decimal SaldoMxn,
    RentPaymentState Estado);

/// <summary>EP-M10 (HU-060/061): resumen mensual de renta de la cuenta, como titular y como inquilino.</summary>
public sealed record MyRentSummaryView(
    string Periodo,
    IReadOnlyList<OwnerRentSummaryItemView> ComoPropietario,
    IReadOnlyList<TenantRentSummaryItemView> ComoInquilino);
