using MongoDB.Bson.Serialization.Attributes;

namespace UniHome.Infrastructure.Persistence.Mongo.Reparaciones;

[BsonIgnoreExtraElements]
public sealed class ReparacionDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement(MongoReparacionesSchema.UnitField)]
    public string UnitId { get; set; } = string.Empty;

    [BsonElement(MongoReparacionesSchema.ReportedByField)]
    public string ReportedByAccountId { get; set; } = string.Empty;

    [BsonElement(MongoReparacionesSchema.TitleField)]
    public string Title { get; set; } = string.Empty;

    [BsonElement(MongoReparacionesSchema.DescriptionField)]
    public string Description { get; set; } = string.Empty;

    [BsonElement(MongoReparacionesSchema.StatusField)]
    public string Status { get; set; } = string.Empty;

    [BsonElement(MongoReparacionesSchema.PhotoField)]
    public ReparacionFotoDocument? Photo { get; set; }

    [BsonElement(MongoReparacionesSchema.CreatedAtField)]
    public DateTime CreatedAt { get; set; }

    [BsonElement(MongoReparacionesSchema.UpdatedAtField)]
    public DateTime UpdatedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ReparacionFotoDocument
{
    [BsonElement(MongoReparacionesSchema.PhotoStorageKeyField)]
    public string StorageKey { get; set; } = string.Empty;

    [BsonElement(MongoReparacionesSchema.PhotoContentTypeField)]
    public string ContentType { get; set; } = string.Empty;

    [BsonElement(MongoReparacionesSchema.PhotoSizeBytesField)]
    public long SizeBytes { get; set; }

    [BsonElement(MongoReparacionesSchema.PhotoSha256Field)]
    public string Sha256 { get; set; } = string.Empty;
}
