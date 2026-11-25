namespace UniHome.Application.Rentals.Payments;

/// <summary>EP-M10 (HU-058): un pago informativo propio, con el periodo al que pertenece.</summary>
public sealed record PaymentHistoryItemView(
    string Periodo,
    decimal MontoMxn,
    DateOnly FechaRecepcion,
    DateTimeOffset RegistradoEnUtc);
