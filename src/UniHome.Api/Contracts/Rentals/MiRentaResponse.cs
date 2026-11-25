using UniHome.Application.Rentals.Payments;
using UniHome.Domain.Rentals;

namespace UniHome.Api.Contracts.Rentals;

public sealed record MiRentaResponse(
    Guid UnitId,
    string Periodo,
    DateOnly FechaCorte,
    decimal CuotaMxn,
    decimal PagadoMxn,
    decimal SaldoMxn,
    RentPaymentState Estado,
    string Aviso)
{
    public static MiRentaResponse FromView(MyRentShareView view) =>
        new(
            view.Period.UnitId,
            view.Period.Periodo,
            RentPeriod.ComputeCutoffDate(view.Period.Periodo, view.Period.CutoffDay),
            view.CuotaMxn,
            view.PagadoMxn,
            view.SaldoMxn,
            view.Estado,
            RentNotices.PagosInformativos);
}
