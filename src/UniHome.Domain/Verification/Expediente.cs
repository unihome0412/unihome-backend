namespace UniHome.Domain.Verification;

public sealed record ExpedienteDecision(
    EstadoExpediente Estado,
    Guid ActorAccountId,
    DateTimeOffset DecidedAt,
    string? Motivo);

/// <summary>
/// Expediente de primera publicación (M06). Nace Pendiente, pasa a En revisión
/// solo cuando ambos documentos fueron recibidos y conserva el indicador de
/// riesgo separado del estado. Las decisiones humanas existen en el dominio para
/// el Sprint 5 (HU-024/HU-025), pero no se exponen como endpoints en Sprint 4.
/// </summary>
public sealed class Expediente
{
    public const int MaxFolioLength = 40;
    public const int MaxRejectionReasonLength = 500;

    private readonly Dictionary<TipoDocumentoVerificacion, DocumentoVerificacion> _documents;
    private IndicadorRiesgo _riskIndicator;

    private Expediente(
        Guid id,
        string folio,
        Guid ownerAccountId,
        Guid publicationId,
        Guid propertyId,
        EstadoExpediente state,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? submittedAt,
        IEnumerable<DocumentoVerificacion> documents,
        IndicadorRiesgo riskIndicator,
        ResultadoComparacionNombres nameComparison,
        string? rejectionReason,
        ExpedienteDecision? decision)
    {
        Id = id;
        Folio = folio;
        OwnerAccountId = ownerAccountId;
        PublicationId = publicationId;
        PropertyId = propertyId;
        State = state;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        SubmittedAt = submittedAt;
        _documents = documents.ToDictionary(document => document.Tipo);
        _riskIndicator = riskIndicator;
        NameComparison = nameComparison;
        RejectionReason = rejectionReason;
        Decision = decision;
    }

    public Guid Id { get; }

    public string Folio { get; }

    public Guid OwnerAccountId { get; }

    public Guid PublicationId { get; }

    public Guid PropertyId { get; }

    public EstadoExpediente State { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public ResultadoComparacionNombres NameComparison { get; private set; }

    public string? RejectionReason { get; private set; }

    public ExpedienteDecision? Decision { get; private set; }

    public IReadOnlyCollection<DocumentoVerificacion> Documents => _documents.Values.ToArray();

    public IndicadorRiesgo RiskIndicator => _riskIndicator;

    public bool IsSuspicious => _riskIndicator.IsSuspicious;

    public bool DocumentsReceived =>
        _documents.ContainsKey(TipoDocumentoVerificacion.Ine) &&
        _documents.ContainsKey(TipoDocumentoVerificacion.Comprobante);

    public bool AutomaticReviewCompleted =>
        DocumentsReceived &&
        _documents.Values.All(document => document.RevisionAutomaticaCompletada);

    public bool DecisionPending => State is EstadoExpediente.Pendiente or EstadoExpediente.EnRevision;

    public static Expediente Create(
        Guid id,
        string folio,
        Guid ownerAccountId,
        Guid publicationId,
        Guid propertyId,
        DateTimeOffset now)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El identificador del expediente es obligatorio.", nameof(id));
        }

        if (ownerAccountId == Guid.Empty || publicationId == Guid.Empty || propertyId == Guid.Empty)
        {
            throw new ArgumentException("El solicitante y su primera publicación son obligatorios.");
        }

        return new Expediente(
            id,
            NormalizeFolio(folio),
            ownerAccountId,
            publicationId,
            propertyId,
            EstadoExpediente.Pendiente,
            now,
            now,
            submittedAt: null,
            documents: [],
            IndicadorRiesgo.Empty,
            ResultadoComparacionNombres.NoDisponible,
            rejectionReason: null,
            decision: null);
    }

    public static Expediente Restore(
        Guid id,
        string folio,
        Guid ownerAccountId,
        Guid publicationId,
        Guid propertyId,
        EstadoExpediente state,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? submittedAt,
        IEnumerable<DocumentoVerificacion>? documents,
        IEnumerable<string>? riskSignals,
        ResultadoComparacionNombres nameComparison,
        string? rejectionReason,
        ExpedienteDecision? decision) =>
        new(
            id,
            NormalizeFolio(folio),
            ownerAccountId,
            publicationId,
            propertyId,
            state,
            createdAt,
            updatedAt,
            submittedAt,
            documents ?? [],
            IndicadorRiesgo.Create(riskSignals),
            nameComparison,
            rejectionReason,
            decision);

    public DocumentoVerificacion GetRequiredDocument(TipoDocumentoVerificacion type) =>
        _documents.TryGetValue(type, out var document)
            ? document
            : throw new KeyNotFoundException("El expediente no contiene ese documento.");

    public DocumentoVerificacion? FindDocument(TipoDocumentoVerificacion type) =>
        _documents.GetValueOrDefault(type);

    /// <summary>
    /// Adjunta o sustituye un documento. Un mismo hash es idempotente. Mientras
    /// el expediente está En revisión no se permite cambiar la evidencia; una
    /// decisión favorable es terminal para esta ruta. La transición a En revisión
    /// ocurre únicamente cuando ambos documentos están presentes.
    /// </summary>
    public DocumentoVerificacion AddOrReplaceDocument(
        DocumentoVerificacion document,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (State == EstadoExpediente.Aprobado)
        {
            throw new InvalidOperationException("El expediente ya fue aprobado.");
        }

        if (_documents.TryGetValue(document.Tipo, out var existing))
        {
            if (string.Equals(existing.Sha256, document.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return existing;
            }

            if (State == EstadoExpediente.EnRevision)
            {
                throw new InvalidOperationException(
                    "El expediente está en revisión y no admite cambios de evidencia.");
            }

            if (document.Version <= existing.Version)
            {
                throw new ArgumentException(
                    "La nueva versión del documento debe ser mayor que la anterior.",
                    nameof(document));
            }
        }
        else if (document.Version != 1)
        {
            throw new ArgumentException(
                "El primer documento de cada tipo debe tener versión 1.",
                nameof(document));
        }

        _documents[document.Tipo] = document;
        RecomputeRiskIndicator();

        if (DocumentsReceived)
        {
            if (State == EstadoExpediente.Pendiente || State == EstadoExpediente.Rechazado)
            {
                State = EstadoExpediente.EnRevision;
                SubmittedAt = now;
                RejectionReason = null;
                Decision = null;
            }
        }

        UpdatedAt = now;
        return document;
    }

    /// <summary>
    /// Registra el resultado orientativo del análisis. Nunca cambia el estado ni
    /// aprueba/rechaza: el indicador de riesgo es independiente (HU-020/022).
    /// </summary>
    public bool RecordAnalysis(
        TipoDocumentoVerificacion type,
        int version,
        string? ocrName,
        EstadoAnalisisDocumento ocrStatus,
        EstadoAnalisisDocumento editionStatus,
        bool editionDetected,
        bool elaAvailable,
        bool metadataAvailable,
        IEnumerable<string>? editionSignals,
        string? analysisDetail,
        DateTimeOffset now)
    {
        if (!_documents.TryGetValue(type, out var current) || current.Version != version)
        {
            return false;
        }

        _documents[type] = current.WithAnalysis(
            ocrName,
            ocrStatus,
            editionStatus,
            editionDetected,
            elaAvailable,
            metadataAvailable,
            editionSignals,
            analysisDetail);

        RecomputeRiskIndicator();
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// Decisión humana favorable (HU-024, Sprint 5). Solo es válida desde
    /// En revisión; cualquier otra transición se rechaza.
    /// </summary>
    public void Approve(Guid administratorAccountId, DateTimeOffset now)
    {
        if (administratorAccountId == Guid.Empty)
        {
            throw new ArgumentException("El administrador es obligatorio.", nameof(administratorAccountId));
        }

        EnsurePendingHumanDecision();
        State = EstadoExpediente.Aprobado;
        Decision = new ExpedienteDecision(State, administratorAccountId, now, null);
        RejectionReason = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// Decisión humana desfavorable (HU-025, Sprint 5). Exige motivo y solo es
    /// válida desde En revisión.
    /// </summary>
    public void Reject(string? reason, Guid administratorAccountId, DateTimeOffset now)
    {
        if (administratorAccountId == Guid.Empty)
        {
            throw new ArgumentException("El administrador es obligatorio.", nameof(administratorAccountId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("El motivo del rechazo es obligatorio.", nameof(reason));
        }

        var normalizedReason = reason.Trim();

        if (normalizedReason.Length > MaxRejectionReasonLength ||
            normalizedReason.Any(char.IsControl))
        {
            throw new ArgumentException("El motivo del rechazo no es válido.", nameof(reason));
        }

        EnsurePendingHumanDecision();
        State = EstadoExpediente.Rechazado;
        RejectionReason = normalizedReason;
        Decision = new ExpedienteDecision(State, administratorAccountId, now, normalizedReason);
        UpdatedAt = now;
    }

    private void EnsurePendingHumanDecision()
    {
        if (State == EstadoExpediente.Aprobado || State == EstadoExpediente.Rechazado)
        {
            throw new InvalidOperationException("El expediente ya fue decidido.");
        }

        if (State != EstadoExpediente.EnRevision)
        {
            throw new InvalidOperationException(
                "El expediente debe estar en revisión antes de una decisión humana.");
        }
    }

    private void RecomputeRiskIndicator()
    {
        var signals = _documents.Values
            .SelectMany(document => document.EditionSignals)
            .ToList();

        if (_documents.Values.Any(document =>
                document.EdicionDetectada && document.EditionSignals.Count == 0))
        {
            signals.Add("El análisis de edición reportó señales sin detalle disponible.");
        }

        _riskIndicator = IndicadorRiesgo.Create(signals);
    }

    private static string NormalizeFolio(string? folio)
    {
        if (string.IsNullOrWhiteSpace(folio))
        {
            throw new ArgumentException("El folio es obligatorio.", nameof(folio));
        }

        var normalized = folio.Trim().ToUpperInvariant();

        if (normalized.Length is < 6 or > MaxFolioLength ||
            !normalized.All(character =>
                char.IsAsciiLetterOrDigit(character) || character == '-'))
        {
            throw new ArgumentException("El folio no es válido.", nameof(folio));
        }

        return normalized;
    }
}
