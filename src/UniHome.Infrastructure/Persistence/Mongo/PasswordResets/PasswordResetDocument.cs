using MongoDB.Bson.Serialization.Attributes;

namespace UniHome.Infrastructure.Persistence.Mongo.PasswordResets;

/// <summary>
/// Documento de una solicitud de recuperación. Usa los nombres físicos del modelo
/// existente (<c>accountId</c>, <c>tokenHash</c>, <c>expiresAt</c>, ...) y solo
/// almacena el hash SHA-256 del token. <c>_id</c> es un Guid en formato N.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class PasswordResetDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement(MongoIdentitySchema.AccountRecoveryAccountIdField)]
    public string AccountId { get; set; } = string.Empty;

    [BsonElement(MongoIdentitySchema.AccountRecoveryTokenHashField)]
    public string TokenHash { get; set; } = string.Empty;

    [BsonElement(MongoIdentitySchema.AccountRecoveryExpiresAtUtcField)]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("consumedAt")]
    public DateTime? ConsumedAt { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("failedAttempts")]
    public int FailedAttempts { get; set; }
}
