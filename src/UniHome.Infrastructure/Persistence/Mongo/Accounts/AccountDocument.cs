using MongoDB.Bson.Serialization.Attributes;

namespace UniHome.Infrastructure.Persistence.Mongo.Accounts;

/// <summary>
/// Documento de persistencia de una cuenta. Mantiene el dominio libre de atributos
/// de MongoDB y almacena el correo normalizado como campo indexado de forma única.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class AccountDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("nombreCompleto")]
    public string FullName { get; set; } = string.Empty;

    [BsonElement(MongoIdentitySchema.AccountsNormalizedEmailField)]
    public string NormalizedEmail { get; set; } = string.Empty;

    [BsonElement("fechaNacimiento")]
    public string BirthDate { get; set; } = string.Empty;

    [BsonElement("genero")]
    public string Gender { get; set; } = string.Empty;

    [BsonElement("hashContrasena")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("aceptacionPrivacidad")]
    public PrivacyAcceptanceDocument PrivacyAcceptance { get; set; } = new();

    [BsonElement("estado")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("correoConfirmado")]
    public bool EmailConfirmed { get; set; }

    [BsonElement("creadoEnUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("correoConfirmadoEnUtc")]
    public DateTime? EmailConfirmedAtUtc { get; set; }

    [BsonElement("verificacionCorreo")]
    public VerificationCodeDocument? EmailVerification { get; set; }

    [BsonElement("intentosReenvioUtc")]
    public List<DateTime> VerificationResendAttemptsUtc { get; set; } = [];
}

[BsonIgnoreExtraElements]
public sealed class PrivacyAcceptanceDocument
{
    [BsonElement("version")]
    public string Version { get; set; } = string.Empty;

    [BsonElement("aceptadaEnUtc")]
    public DateTime AcceptedAtUtc { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class VerificationCodeDocument
{
    [BsonElement("hash")]
    public string CodeHash { get; set; } = string.Empty;

    [BsonElement("emitidoEnUtc")]
    public DateTime IssuedAtUtc { get; set; }

    [BsonElement("expiraEnUtc")]
    public DateTime ExpiresAtUtc { get; set; }

    [BsonElement("consumidoEnUtc")]
    public DateTime? ConsumedAtUtc { get; set; }
}
