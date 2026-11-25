using UniHome.Application.Summary;

namespace UniHome.Api.Contracts.Summary;

public sealed record ResumenResponse(
    string Periodo,
    decimal RentasRegistradasDelMesMxn,
    int NumeroPropiedades,
    int ReparacionesAbiertas)
{
    public static ResumenResponse FromView(MySummaryView view) =>
        new(view.Periodo, view.RentasRegistradasDelMesMxn, view.NumeroPropiedades, view.ReparacionesAbiertas);
}
