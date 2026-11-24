namespace UniHome.Domain.Notifications;

/// <summary>Único tipo hoy: HU-059. El esquema admite más tipos sin migrar.</summary>
public enum NotificationType
{
    RecordatorioRenta
}

/// <summary>
/// EP-M12 (HU-059): notificación generada al consultar <c>GET /notificaciones</c>,
/// nunca por un job en segundo plano. El <c>_id</c> determinista de
/// <see cref="CreateRentReminder"/> hace que volver a generarla en la misma
/// ventana sea un upsert idempotente, no un duplicado.
/// </summary>
public sealed class Notification
{
    private Notification(
        Guid id,
        Guid recipientAccountId,
        NotificationType type,
        string message,
        Guid? unitId,
        string? periodo,
        DateTimeOffset createdAt)
    {
        Id = id;
        RecipientAccountId = recipientAccountId;
        Type = type;
        Message = message;
        UnitId = unitId;
        Periodo = periodo;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public Guid RecipientAccountId { get; }
    public NotificationType Type { get; }
    public string Message { get; }
    public Guid? UnitId { get; }
    public string? Periodo { get; }
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// El <c>_id</c> se deriva de (destinatario, unidad, periodo, tipo): la misma
    /// combinación siempre produce el mismo identificador, así que generarla de
    /// nuevo en cada <c>GET</c> no crea un documento adicional.
    /// </summary>
    public static Guid ComputeRentReminderId(Guid recipientAccountId, Guid unitId, string periodo)
    {
        var canonical = $"{recipientAccountId:N}|{unitId:N}|{periodo}|{NotificationType.RecordatorioRenta}";
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(canonical));
        return new Guid(hash[..16]);
    }

    public static Notification CreateRentReminder(
        Guid recipientAccountId,
        Guid unitId,
        string periodo,
        string message,
        DateTimeOffset now)
    {
        if (recipientAccountId == Guid.Empty)
        {
            throw new ArgumentException("El destinatario es obligatorio.", nameof(recipientAccountId));
        }

        if (unitId == Guid.Empty)
        {
            throw new ArgumentException("La unidad es obligatoria.", nameof(unitId));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("El mensaje es obligatorio.", nameof(message));
        }

        return new Notification(
            ComputeRentReminderId(recipientAccountId, unitId, periodo),
            recipientAccountId,
            NotificationType.RecordatorioRenta,
            message,
            unitId,
            periodo,
            now);
    }

    public static Notification Restore(
        Guid id,
        Guid recipientAccountId,
        NotificationType type,
        string message,
        Guid? unitId,
        string? periodo,
        DateTimeOffset createdAt) =>
        new(id, recipientAccountId, type, message, unitId, periodo, createdAt);
}
