using MongoDB.Bson.Serialization.Attributes;

namespace UniHome.Infrastructure.Persistence.Mongo.Universities;

/// <summary>
/// Documento del catálogo M03. <c>nombreYCampus</c> conserva el campo único que
/// define HU-075; <c>nombreNormalizado</c> solo se usa para unicidad y búsqueda.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class UniversityDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement(MongoUniversitySchema.NameAndCampusField)]
    public string NameAndCampus { get; set; } = string.Empty;

    [BsonElement(MongoUniversitySchema.NormalizedNameField)]
    public string NormalizedName { get; set; } = string.Empty;

    [BsonElement(MongoUniversitySchema.ActiveField)]
    public bool IsActive { get; set; }

    [BsonElement(MongoUniversitySchema.CreatedAtField)]
    public DateTime CreatedAt { get; set; }
}
