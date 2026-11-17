using UniHome.Domain.Properties;
using UniHome.Domain.Verification;

namespace UniHome.Application.Verification.Documents;

public sealed record DocumentosRecibidosView(bool Ine, bool Comprobante);

public sealed record ExpedienteDecisionView(
    EstadoExpediente Estado,
    DateTimeOffset Fecha,
    string? Motivo);

/// <summary>
/// Respuesta del solicitante. No incluye "Sospechoso", OCR, ELA ni señales:
/// esos campos son exclusivos de la vista administrativa (HU-026).
/// </summary>
public sealed record MiExpedienteView(
    Guid Id,
    string Folio,
    EstadoExpediente Estado,
    DocumentosRecibidosView DocumentosRecibidos,
    bool RevisionAutomaticaCompletada,
    bool DecisionPendiente,
    DateTimeOffset FechaSolicitud,
    ExpedienteDecisionView? Decision)
{
    public static MiExpedienteView FromDomain(Expediente expediente) =>
        new(
            expediente.Id,
            expediente.Folio,
            expediente.State,
            new DocumentosRecibidosView(
                expediente.FindDocument(TipoDocumentoVerificacion.Ine) is not null,
                expediente.FindDocument(TipoDocumentoVerificacion.Comprobante) is not null),
            expediente.AutomaticReviewCompleted,
            expediente.DecisionPending,
            expediente.CreatedAt,
            expediente.Decision is null
                ? null
                : new ExpedienteDecisionView(
                    expediente.Decision.Estado,
                    expediente.Decision.DecidedAt,
                    expediente.Decision.Motivo));
}

public sealed record DocumentUploadView(
    MiExpedienteView Expediente,
    bool DocumentoYaExistente);

public sealed record ExpedienteResumenView(
    Guid Id,
    string Folio,
    string SolicitanteNombre,
    DateTimeOffset Fecha,
    EstadoExpediente Estado,
    bool EsSospechoso);

public sealed record IndicadorEdicionView(
    bool Detectado,
    bool Disponible,
    string Aviso,
    bool ElaDisponible,
    bool MetadatosDisponibles,
    IReadOnlyList<string> Detalle)
{
    public const string Warning =
        "El análisis automático es orientativo y no determina la decisión.";
}

public sealed record ExpedienteDetalleView(
    Guid Id,
    string Folio,
    string SolicitanteNombre,
    string? SolicitanteCorreo,
    DateTimeOffset Fecha,
    EstadoExpediente Estado,
    string? IneUrlMiniatura,
    string? ComprobanteUrlMiniatura,
    string? NombreOcrIne,
    string? NombreOcrComprobante,
    ResultadoComparacionNombres ResultadoComparacionNombres,
    IndicadorEdicionView IndicadorEdicion,
    bool EsSospechoso,
    IReadOnlyList<string> SenalesRiesgo,
    bool RevisionAutomaticaCompletada,
    string? MotivoRechazo);

public sealed record ExpedientePageView(
    IReadOnlyList<ExpedienteResumenView> Items,
    long Total,
    long PendingReviewCount);

public sealed record FirstPublicationSubmissionView(
    Guid PublicacionId,
    PublicationState Estado,
    DateTimeOffset FechaEnvio);

public sealed record VerificationDocumentReadView(
    byte[] Content,
    string ContentType,
    string FileName,
    bool IsThumbnail);
