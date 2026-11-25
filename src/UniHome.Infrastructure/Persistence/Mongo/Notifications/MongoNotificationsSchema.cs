namespace UniHome.Infrastructure.Persistence.Mongo.Notifications;

/// <summary>Nombres físicos M12. Un cambio exige una migración nueva.</summary>
public static class MongoNotificationsSchema
{
    public const string NotificationsCollection = "notificaciones";

    public const string RecipientField = "destinatarioAccountId";
    public const string TypeField = "tipo";
    public const string MessageField = "mensaje";
    public const string UnitField = "unitId";
    public const string PeriodoField = "periodo";
    public const string ReparacionField = "reparacionId";
    public const string CreatedAtField = "createdAt";

    public const string RecipientIndex = "ix_notificaciones_destinatario";
}
