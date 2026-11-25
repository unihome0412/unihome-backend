using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UniHome.Infrastructure.Persistence.Mongo.Rentals;

[BsonIgnoreExtraElements]
public sealed class RentPeriodDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement(MongoRentalsSchema.RentPeriodUnitField)]
    public string UnitId { get; set; } = string.Empty;

    [BsonElement(MongoRentalsSchema.RentPeriodPeriodoField)]
    public string Periodo { get; set; } = string.Empty;

    [BsonElement(MongoRentalsSchema.RentPeriodTotalField)]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TotalMxn { get; set; }

    [BsonElement(MongoRentalsSchema.RentPeriodCutoffDayField)]
    public int CutoffDay { get; set; }

    [BsonElement(MongoRentalsSchema.RentPeriodConceptsField)]
    public List<RentConceptDocument> Concepts { get; set; } = [];

    [BsonElement(MongoRentalsSchema.RentPeriodSplitMethodField)]
    [BsonIgnoreIfNull]
    public string? SplitMethod { get; set; }

    [BsonElement(MongoRentalsSchema.RentPeriodSharesField)]
    public List<TenantShareDocument> Shares { get; set; } = [];

    [BsonElement(MongoRentalsSchema.RentPeriodCreatedAtField)]
    public DateTime CreatedAt { get; set; }

    [BsonElement(MongoRentalsSchema.RentPeriodUpdatedAtField)]
    public DateTime UpdatedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class RentConceptDocument
{
    [BsonElement(MongoRentalsSchema.ConceptNameField)]
    public string Nombre { get; set; } = string.Empty;

    [BsonElement(MongoRentalsSchema.ConceptFixedField)]
    public bool EsFijo { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class TenantShareDocument
{
    [BsonElement(MongoRentalsSchema.ShareTenantField)]
    public string TenantAccountId { get; set; } = string.Empty;

    [BsonElement(MongoRentalsSchema.ShareAmountField)]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal AmountMxn { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class InformativePaymentDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement(MongoRentalsSchema.PaymentRentPeriodField)]
    public string RentPeriodId { get; set; } = string.Empty;

    [BsonElement(MongoRentalsSchema.PaymentTenantField)]
    public string TenantAccountId { get; set; } = string.Empty;

    [BsonElement(MongoRentalsSchema.PaymentAmountField)]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal AmountMxn { get; set; }

    [BsonElement(MongoRentalsSchema.PaymentReceivedOnField)]
    public DateTime ReceivedOn { get; set; }

    [BsonElement(MongoRentalsSchema.PaymentRegisteredAtField)]
    public DateTime RegisteredAt { get; set; }
}
