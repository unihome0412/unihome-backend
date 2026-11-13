using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UniHome.Infrastructure.Persistence.Mongo.Publications;

[BsonIgnoreExtraElements]
public sealed class PropertyDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.PropertyOwnerField)]
    public string OwnerAccountId { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.PropertyNameField)]
    public string Name { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.PropertyNameNormalizedField)]
    public string NameNormalized { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.PropertyAddressField)]
    public string Address { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.PropertyAddressNormalizedField)]
    public string AddressNormalized { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.PropertyColoniaField)]
    public string Colonia { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.PropertyColoniaNormalizedField)]
    public string ColoniaNormalized { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.PropertyTypeField)]
    public string PropertyType { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.PropertyUniversityField)]
    public string UniversityId { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.PropertyLocationField)]
    [BsonIgnoreIfNull]
    public MongoGeoPointDocument? Location { get; set; }

    [BsonElement(MongoPublicationSchema.PropertyFloorField)]
    [BsonIgnoreIfNull]
    public string? Floor { get; set; }

    [BsonElement(MongoPublicationSchema.PropertyUnitNumberField)]
    [BsonIgnoreIfNull]
    public string? UnitNumber { get; set; }

    [BsonElement(MongoPublicationSchema.PropertyStateField)]
    public string PublicationState { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.PropertyFirstApprovalField)]
    [BsonIgnoreIfNull]
    public DateTime? FirstApprovalAt { get; set; }

    [BsonElement(MongoPublicationSchema.PropertyCreatedAtField)]
    public DateTime CreatedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class UnitDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.UnitPropertyField)]
    public string PropertyId { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.UnitOwnerField)]
    public string OwnerAccountId { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.UnitUniversityField)]
    public string UniversityId { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.UnitTypeField)]
    public string UnitType { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.UnitOccupancyField)]
    public string Occupancy { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.UnitPlacesField)]
    public int Places { get; set; }

    [BsonElement(MongoPublicationSchema.UnitPriceField)]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal PricePerPlace { get; set; }

    [BsonElement(MongoPublicationSchema.UnitSizeField)]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal SizeM2 { get; set; }

    [BsonElement(MongoPublicationSchema.UnitForWhomField)]
    public string ForWhom { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.UnitBathroomField)]
    public string Bathroom { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.UnitFurnitureField)]
    public string Furniture { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.UnitFurnitureItemsField)]
    public List<string> FurnitureItems { get; set; } = [];

    [BsonElement(MongoPublicationSchema.UnitTemplateField)]
    [BsonIgnoreIfNull]
    public string? Template { get; set; }

    [BsonElement(MongoPublicationSchema.UnitZoneField)]
    [BsonIgnoreIfNull]
    public string? ZoneInfo { get; set; }

    [BsonElement(MongoPublicationSchema.UnitRulesField)]
    [BsonIgnoreIfNull]
    public string? Rules { get; set; }

    [BsonElement(MongoPublicationSchema.UnitServicesField)]
    public List<ServiceChargeDocument> Services { get; set; } = [];

    [BsonElement(MongoPublicationSchema.UnitPhotosField)]
    public List<UnitPhotoDocument> Photos { get; set; } = [];

    [BsonElement(MongoPublicationSchema.UnitStateField)]
    public string PublicationState { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.UnitCreatedAtField)]
    public DateTime CreatedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ServiceChargeDocument
{
    [BsonElement("nombre")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("modalidad")]
    public string Mode { get; set; } = string.Empty;

    [BsonElement("montoMensualMxn")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal? Amount { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class UnitPhotoDocument
{
    [BsonElement("storageKey")]
    public string StorageKey { get; set; } = string.Empty;

    [BsonElement("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [BsonElement("sizeBytes")]
    public long SizeBytes { get; set; }

    [BsonElement("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [BsonElement("orden")]
    public int Order { get; set; }

    [BsonElement("esPrincipal")]
    public bool IsPrimary { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class TermsAcceptanceDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.TermsAccountField)]
    public string AccountId { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.TermsVersionField)]
    public string Version { get; set; } = string.Empty;

    [BsonElement(MongoPublicationSchema.TermsAcceptedAtField)]
    public DateTime AcceptedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class MongoGeoPointDocument
{
    [BsonElement("type")]
    public string Type { get; set; } = "Point";

    /// <summary>Orden GeoJSON: longitud, latitud.</summary>
    [BsonElement("coordinates")]
    public List<double> Coordinates { get; set; } = [];
}
