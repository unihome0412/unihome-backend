using UniHome.Application.Rentals.Payments;

namespace UniHome.Api.Contracts.Rentals;

public sealed record PagoHistorialItemResponse(
    string Periodo,
    decimal MontoMxn,
    DateOnly FechaRecepcion,
    DateTimeOffset RegistradoEnUtc)
{
    public static PagoHistorialItemResponse FromView(PaymentHistoryItemView view) =>
        new(view.Periodo, view.MontoMxn, view.FechaRecepcion, view.RegistradoEnUtc);
}

public sealed record MisPagosResponse(IReadOnlyList<PagoHistorialItemResponse> Pagos, string Aviso)
{
    public static MisPagosResponse FromItems(IReadOnlyList<PaymentHistoryItemView> items) =>
        new(items.Select(PagoHistorialItemResponse.FromView).ToArray(), RentNotices.PagosInformativos);
}
