using UniHome.Domain.Verification;

namespace UniHome.Application.Verification.Abstractions;

public sealed record VerificationAuditEntry(
    Guid Id,
    Guid ExpedienteId,
    string Folio,
    Guid ActorAccountId,
    string ActorRole,
    string Action,
    TipoDocumentoVerificacion? DocumentType,
    int? DocumentVersion,
    string? Detail,
    DateTimeOffset Timestamp,
    string? TraceId);

public interface IVerificationAuditRepository
{
    Task AppendAsync(
        VerificationAuditEntry entry,
        CancellationToken cancellationToken = default);
}

public static class VerificationAuditActions
{
    public const string DocumentUploaded = "DocumentoSubido";
    public const string AnalysisRecorded = "AnalisisRegistrado";
    public const string PublicationSubmitted = "PublicacionEnRevision";
    public const string ExpedienteViewed = "ExpedienteConsultado";
    public const string DocumentUrlIssued = "UrlDocumentoEmitida";
    public const string DocumentViewed = "DocumentoConsultado";
    public const string ExpedienteApproved = "ExpedienteAprobado";
    public const string ExpedienteRejected = "ExpedienteRechazado";
    public const string DecisionCompensated = "DecisionCompensada";
}
