using MongoDB.Bson.Serialization.Attributes;

namespace UniHome.Infrastructure.Persistence.Mongo.Notifications;

[BsonIgnoreExtraElements]
public sealed class NotificationDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement(MongoNotificationsSchema.RecipientField)]
    public string RecipientAccountId { get; set; } = string.Empty;

    [BsonElement(MongoNotificationsSchema.TypeField)]
    public string Type { get; set; } = string.Empty;

    [BsonElement(MongoNotificationsSchema.MessageField)]
    public string Message { get; set; } = string.Empty;

    [BsonElement(MongoNotificationsSchema.UnitField)]
    public string? UnitId { get; set; }

    [BsonElement(MongoNotificationsSchema.PeriodoField)]
    public string? Periodo { get; set; }

    [BsonElement(MongoNotificationsSchema.CreatedAtField)]
    public DateTime CreatedAt { get; set; }
}
