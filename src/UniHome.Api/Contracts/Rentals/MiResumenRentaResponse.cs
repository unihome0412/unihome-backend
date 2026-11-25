using UniHome.Application.Rentals.Payments;
using UniHome.Domain.Rentals;

namespace UniHome.Api.Contracts.Rentals;

public sealed record ResumenPropietarioItemResponse(
    Guid UnitId,
    decimal RentaTotalMxn,
    decimal RecaudadoMxn,
    decimal PendienteMxn,
    bool DivisionPendiente)
{
    public static ResumenPropietarioItemResponse FromView(OwnerRentSummaryItemView view) =>
        new(view.UnitId, view.RentaTotalMxn, view.RecaudadoMxn, view.PendienteMxn, view.DivisionPendiente);
}

public sealed record ResumenInquilinoItemResponse(
    Guid UnitId,
    decimal CuotaMxn,
    decimal PagadoMxn,
    decimal SaldoMxn,
    RentPaymentState Estado)
{
    public static ResumenInquilinoItemResponse FromView(TenantRentSummaryItemView view) =>
        new(view.UnitId, view.CuotaMxn, view.PagadoMxn, view.SaldoMxn, view.Estado);
}

public sealed record MiResumenRentaResponse(
    string Periodo,
    IReadOnlyList<ResumenPropietarioItemResponse> ComoPropietario,
    IReadOnlyList<ResumenInquilinoItemResponse> ComoInquilino,
    string Aviso)
{
    public static MiResumenRentaResponse FromView(MyRentSummaryView view) =>
        new(
            view.Periodo,
            view.ComoPropietario.Select(ResumenPropietarioItemResponse.FromView).ToArray(),
            view.ComoInquilino.Select(ResumenInquilinoItemResponse.FromView).ToArray(),
            RentNotices.PagosInformativos);
}
