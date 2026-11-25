namespace UniHome.Domain.Notifications;

/// <summary>HU-059 y HU-066. El esquema admite más tipos sin migrar.</summary>
public enum NotificationType
{
    RecordatorioRenta,
    ReparacionActualizada
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
        Guid? reparacionId,
        DateTimeOffset createdAt)
    {
        Id = id;
        RecipientAccountId = recipientAccountId;
        Type = type;
        Message = message;
        UnitId = unitId;
        Periodo = periodo;
        ReparacionId = reparacionId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public Guid RecipientAccountId { get; }
    public NotificationType Type { get; }
    public string Message { get; }
    public Guid? UnitId { get; }
    public string? Periodo { get; }

    /// <summary>HU-066: referencia al reporte de reparación actualizado; null para otros tipos.</summary>
    public Guid? ReparacionId { get; }
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
            reparacionId: null,
            now);
    }

    /// <summary>
    /// HU-066: notifica a un destinatario (titular o inquilino de la unidad,
    /// distinto de quien cambió el estado) que un reporte de reparación de su
    /// unidad se actualizó. A diferencia del recordatorio de renta, cada
    /// cambio de estado es un evento propio: el id es aleatorio, no
    /// determinista, para no colapsar varias actualizaciones en una sola.
    /// </summary>
    public static Notification CreateReparacionStatusUpdate(
        Guid recipientAccountId,
        Guid unitId,
        Guid reparacionId,
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

        if (reparacionId == Guid.Empty)
        {
            throw new ArgumentException("El reporte de reparación es obligatorio.", nameof(reparacionId));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("El mensaje es obligatorio.", nameof(message));
        }

        return new Notification(
            Guid.NewGuid(),
            recipientAccountId,
            NotificationType.ReparacionActualizada,
            message,
            unitId,
            periodo: null,
            reparacionId,
            now);
    }

    public static Notification Restore(
        Guid id,
        Guid recipientAccountId,
        NotificationType type,
        string message,
        Guid? unitId,
        string? periodo,
        Guid? reparacionId,
        DateTimeOffset createdAt) =>
        new(id, recipientAccountId, type, message, unitId, periodo, reparacionId, createdAt);
}
