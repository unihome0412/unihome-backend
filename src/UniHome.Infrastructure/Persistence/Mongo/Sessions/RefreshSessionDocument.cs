using MongoDB.Bson.Serialization.Attributes;

namespace UniHome.Infrastructure.Persistence.Mongo.Sessions;

/// <summary>
/// Documento de una sesión de dispositivo. Guarda solo el hash del refresh vigente
/// y las huellas de tokens rotados; nunca el valor en claro.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class RefreshSessionDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("cuentaId")]
    public string AccountId { get; set; } = string.Empty;

    [BsonElement("hashRefreshActual")]
    public string CurrentTokenHash { get; set; } = string.Empty;

    [BsonElement("refreshEmitidoEnUtc")]
    public DateTime CurrentTokenIssuedAtUtc { get; set; }

    [BsonElement("expiraEnUtc")]
    public DateTime ExpiresAtUtc { get; set; }

    [BsonElement("tokensUsados")]
    public List<UsedRefreshTokenDocument> UsedTokens { get; set; } = [];

    [BsonElement("revocadaEnUtc")]
    public DateTime? RevokedAtUtc { get; set; }

    [BsonElement("motivoRevocacion")]
    public string? RevokedReason { get; set; }

    [BsonElement("creadaEnUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("ultimaRotacionEnUtc")]
    public DateTime LastRotatedAtUtc { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class UsedRefreshTokenDocument
{
    [BsonElement("hash")]
    public string TokenHash { get; set; } = string.Empty;

    [BsonElement("usadoEnUtc")]
    public DateTime UsedAtUtc { get; set; }
}
