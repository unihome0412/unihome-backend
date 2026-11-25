using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.Notifications;

/// <summary>
/// Migración M12-001: solo el recordatorio de renta del Sprint 7 (HU-059).
/// El resto de M12 (notificaciones de otros módulos) queda fuera de alcance.
/// </summary>
public static class MongoNotificationsMigration
{
    public const string Version = "M12-001";
    public const string Name = "Notificaciones M12 (recordatorio de renta)";
    public const string Description =
        "Crea notificaciones con índice de destinatario.";

    public static MongoMigrationDefinition Definition { get; } = new(
        Version,
        Name,
        Description,
        MongoNotificationsIndexes.Definitions);
}
