using UniHome.Domain.Rentals;

namespace UniHome.Api.Contracts.Rentals;

public sealed record PagoResponse(
    Guid InquilinoAccountId,
    decimal MontoMxn,
    DateOnly FechaRecepcion,
    DateTimeOffset RegistradoEnUtc,
    string Aviso)
{
    public static PagoResponse FromDomain(InformativePayment payment) =>
        new(
            payment.TenantAccountId,
            payment.AmountMxn,
            payment.ReceivedOn,
            payment.RegisteredAt,
            RentNotices.PagosInformativos);
}
