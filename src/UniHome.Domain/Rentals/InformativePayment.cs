namespace UniHome.Domain.Rentals;

/// <summary>
/// EP-M10 (HU-055): registro informativo de un pago recibido. UniHome no
/// procesa dinero (HU-090); esto es solo una bitácora que el titular lleva
/// manualmente. Varios pagos pueden existir para el mismo
/// (periodo, inquilino) — se van sumando contra la cuota (HU-056); un pago
/// que exceda la cuota no se rechaza, solo deja de tener saldo pendiente.
/// </summary>
public sealed class InformativePayment
{
    private InformativePayment(
        Guid id,
        Guid rentPeriodId,
        Guid tenantAccountId,
        decimal amountMxn,
        DateOnly receivedOn,
        DateTimeOffset registeredAt)
    {
        Id = id;
        RentPeriodId = rentPeriodId;
        TenantAccountId = tenantAccountId;
        AmountMxn = amountMxn;
        ReceivedOn = receivedOn;
        RegisteredAt = registeredAt;
    }

    public Guid Id { get; }
    public Guid RentPeriodId { get; }
    public Guid TenantAccountId { get; }
    public decimal AmountMxn { get; }
    public DateOnly ReceivedOn { get; }
    public DateTimeOffset RegisteredAt { get; }

    public static InformativePayment Register(
        Guid rentPeriodId,
        Guid tenantAccountId,
        decimal amountMxn,
        DateOnly receivedOn,
        DateTimeOffset now)
    {
        if (rentPeriodId == Guid.Empty)
        {
            throw new ArgumentException("El periodo de renta es obligatorio.", nameof(rentPeriodId));
        }

        if (tenantAccountId == Guid.Empty)
        {
            throw new ArgumentException("El inquilino es obligatorio.", nameof(tenantAccountId));
        }

        if (amountMxn <= 0)
        {
            throw new ArgumentException("El monto debe ser mayor que cero.", nameof(amountMxn));
        }

        if (receivedOn > DateOnly.FromDateTime(now.UtcDateTime))
        {
            throw new ArgumentException("La fecha de recepción no puede ser futura.", nameof(receivedOn));
        }

        return new InformativePayment(Guid.NewGuid(), rentPeriodId, tenantAccountId, amountMxn, receivedOn, now);
    }

    public static InformativePayment Restore(
        Guid id,
        Guid rentPeriodId,
        Guid tenantAccountId,
        decimal amountMxn,
        DateOnly receivedOn,
        DateTimeOffset registeredAt) =>
        new(id, rentPeriodId, tenantAccountId, amountMxn, receivedOn, registeredAt);
}
