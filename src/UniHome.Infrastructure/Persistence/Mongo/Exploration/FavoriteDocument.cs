using MongoDB.Bson.Serialization.Attributes;

namespace UniHome.Infrastructure.Persistence.Mongo.Exploration;

[BsonIgnoreExtraElements]
public sealed class FavoriteDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement(MongoExplorationSchema.FavoriteAccountField)]
    public string AccountId { get; set; } = string.Empty;

    [BsonElement(MongoExplorationSchema.FavoriteUnitField)]
    public string UnitId { get; set; } = string.Empty;

    [BsonElement(MongoExplorationSchema.FavoriteCreatedAtField)]
    public DateTime CreatedAt { get; set; }
}
