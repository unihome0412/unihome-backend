using MongoDB.Bson.Serialization.Attributes;

namespace UniHome.Infrastructure.Persistence.Mongo.OccupancyLinks;

/// <summary>
/// Un documento por par inquilino/unidad. <c>_id</c> determinista
/// (<c>inquilino:unidad</c>) garantiza unicidad nativa sin depender de un índice
/// declarado, igual que <c>FavoriteDocument</c>.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class OccupancyLinkDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement(MongoOccupancySchema.LinkTenantField)]
    public string TenantAccountId { get; set; } = string.Empty;

    [BsonElement(MongoOccupancySchema.LinkUnitField)]
    public string UnitId { get; set; } = string.Empty;

    [BsonElement(MongoOccupancySchema.LinkStatusField)]
    public string Status { get; set; } = string.Empty;

    [BsonElement(MongoOccupancySchema.LinkJoinedAtField)]
    public DateTime JoinedAt { get; set; }

    [BsonElement(MongoOccupancySchema.LinkEndedAtField)]
    [BsonIgnoreIfNull]
    public DateTime? EndedAt { get; set; }

    [BsonElement(MongoOccupancySchema.LinkEndedReasonField)]
    [BsonIgnoreIfNull]
    public string? EndedReason { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class UnitInvitationDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement(MongoOccupancySchema.InvitationUnitField)]
    public string UnitId { get; set; } = string.Empty;

    [BsonElement(MongoOccupancySchema.InvitationCodeField)]
    public string Code { get; set; } = string.Empty;

    [BsonElement(MongoOccupancySchema.InvitationCreatedAtField)]
    public DateTime CreatedAt { get; set; }

    [BsonElement(MongoOccupancySchema.InvitationExpiresAtField)]
    public DateTime ExpiresAt { get; set; }
}
