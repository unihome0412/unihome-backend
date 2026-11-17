using MongoDB.Bson.Serialization.Attributes;

namespace UniHome.Infrastructure.Persistence.Mongo.Verification;

[BsonIgnoreExtraElements]
public sealed class VerificationExpedienteDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("folio")]
    public string Folio { get; set; } = string.Empty;

    [BsonElement("ownerAccountId")]
    public string OwnerAccountId { get; set; } = string.Empty;

    [BsonElement("publicationId")]
    public string PublicationId { get; set; } = string.Empty;

    [BsonElement("propertyId")]
    public string PropertyId { get; set; } = string.Empty;

    [BsonElement("estado")]
    public string Estado { get; set; } = string.Empty;

    [BsonElement("ine")]
    public VerificationDocumentMetadataDocument? Ine { get; set; }

    [BsonElement("comprobante")]
    public VerificationDocumentMetadataDocument? Comprobante { get; set; }

    [BsonElement("esSospechoso")]
    public bool EsSospechoso { get; set; }

    [BsonElement("senalesRiesgo")]
    public List<string> SenalesRiesgo { get; set; } = [];

    [BsonElement("resultadoComparacionNombres")]
    public string ResultadoComparacionNombres { get; set; } = "NoDisponible";

    [BsonElement("motivoRechazo")]
    public string? MotivoRechazo { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [BsonElement("submittedAt")]
    public DateTime? SubmittedAt { get; set; }

    [BsonElement("decision")]
    public VerificationDecisionDocument? Decision { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class VerificationDocumentMetadataDocument
{
    [BsonElement("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [BsonElement("storageKey")]
    public string StorageKey { get; set; } = string.Empty;

    [BsonElement("originalFileName")]
    public string OriginalFileName { get; set; } = string.Empty;

    [BsonElement("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [BsonElement("sizeBytes")]
    public long SizeBytes { get; set; }

    [BsonElement("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [BsonElement("version")]
    public int Version { get; set; }

    [BsonElement("uploadedAt")]
    public DateTime UploadedAt { get; set; }

    [BsonElement("nombreOcr")]
    public string? NombreOcr { get; set; }

    [BsonElement("estadoOcr")]
    public string EstadoOcr { get; set; } = string.Empty;

    [BsonElement("estadoEdicion")]
    public string EstadoEdicion { get; set; } = string.Empty;

    [BsonElement("edicionDetectada")]
    public bool EdicionDetectada { get; set; }

    [BsonElement("elaDisponible")]
    public bool ElaDisponible { get; set; }

    [BsonElement("metadatosDisponibles")]
    public bool MetadatosDisponibles { get; set; }

    [BsonElement("senalesEdicion")]
    public List<string> SenalesEdicion { get; set; } = [];

    [BsonElement("detalleAnalisis")]
    public string? DetalleAnalisis { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class VerificationDecisionDocument
{
    [BsonElement("estado")]
    public string Estado { get; set; } = string.Empty;

    [BsonElement("actorAccountId")]
    public string ActorAccountId { get; set; } = string.Empty;

    [BsonElement("decidedAt")]
    public DateTime DecidedAt { get; set; }

    [BsonElement("motivo")]
    public string? Motivo { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class VerificationDocumentBlobDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("expedienteId")]
    public string ExpedienteId { get; set; } = string.Empty;

    [BsonElement("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [BsonElement("version")]
    public int Version { get; set; }

    [BsonElement("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [BsonElement("originalFileName")]
    public string OriginalFileName { get; set; } = string.Empty;

    [BsonElement("sizeBytes")]
    public long SizeBytes { get; set; }

    [BsonElement("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [BsonElement("nonce")]
    public byte[] Nonce { get; set; } = [];

    [BsonElement("tag")]
    public byte[] Tag { get; set; } = [];

    [BsonElement("ciphertext")]
    public byte[] Ciphertext { get; set; } = [];

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class VerificationAuditDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("expedienteId")]
    public string ExpedienteId { get; set; } = string.Empty;

    [BsonElement("folio")]
    public string Folio { get; set; } = string.Empty;

    [BsonElement("actorAccountId")]
    public string ActorAccountId { get; set; } = string.Empty;

    [BsonElement("actorRole")]
    public string ActorRole { get; set; } = string.Empty;

    [BsonElement("accion")]
    public string Accion { get; set; } = string.Empty;

    [BsonElement("tipoDocumento")]
    public string? TipoDocumento { get; set; }

    [BsonElement("documentVersion")]
    public int? DocumentVersion { get; set; }

    [BsonElement("detail")]
    public string? Detail { get; set; }

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; }

    [BsonElement("traceId")]
    public string? TraceId { get; set; }
}
