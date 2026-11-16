using System.Diagnostics;
using System.Security.Cryptography;
using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Profiles;
using UniHome.Application.Publications.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Verification.Abstractions;
using UniHome.Application.Verification.Documents;
using UniHome.Domain.Units;
using UniHome.Domain.Verification;

namespace UniHome.Application.Verification.Documents;

/// <summary>
/// EP-M06-01/EP-M06-02: recibe INE o comprobante de propiedad, valida el
/// contenido real, lo cifra en almacenamiento privado, ejecuta el análisis local
/// orientativo y actualiza el expediente. El expediente pasa a En revisión solo
/// cuando ambos documentos existen; el análisis nunca decide por sí solo.
/// </summary>
public sealed class UploadVerificationDocumentService
{
    private const int MaxConcurrencyAttempts = 3;

    private readonly IVerificationRepository _expedientes;
    private readonly IVerificationDocumentStore _documentStore;
    private readonly IVerificationAuditRepository _audit;
    private readonly IDocumentAnalyzer _analyzer;
    private readonly IUnitRepository _units;
    private readonly ITermsAcceptanceRepository _terms;
    private readonly PublishingEligibilityService _eligibility;
    private readonly ISystemClock _clock;

    public UploadVerificationDocumentService(
        IVerificationRepository expedientes,
        IVerificationDocumentStore documentStore,
        IVerificationAuditRepository audit,
        IDocumentAnalyzer analyzer,
        IUnitRepository units,
        ITermsAcceptanceRepository terms,
        PublishingEligibilityService eligibility,
        ISystemClock clock)
    {
        _expedientes = expedientes;
        _documentStore = documentStore;
        _audit = audit;
        _analyzer = analyzer;
        _units = units;
        _terms = terms;
        _eligibility = eligibility;
        _clock = clock;
    }

    public async Task<OperationResult<DocumentUploadView>> UploadAsync(
        Guid ownerAccountId,
        TipoDocumentoVerificacion documentType,
        VerificationDocumentUpload upload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);

        if (!await _eligibility
                .IsEligibleAsync(ownerAccountId, cancellationToken)
                .ConfigureAwait(false))
        {
            return OperationResult<DocumentUploadView>.Forbidden();
        }

        ValidatedVerificationDocument validated;

        try
        {
            validated = VerificationDocumentValidator.Validate(documentType, upload);
        }
        catch (VerificationDocumentValidationException exception)
        {
            return OperationResult<DocumentUploadView>.ValidationFailed(
                "archivo",
                exception.Message);
        }

        for (var attempt = 0; attempt < MaxConcurrencyAttempts; attempt++)
        {
            var context = await GetContextAsync(ownerAccountId, cancellationToken)
                .ConfigureAwait(false);

            if (context is null)
            {
                return OperationResult<DocumentUploadView>.NotFound();
            }

            var (expediente, unit) = context.Value;
            var existing = expediente.FindDocument(documentType);

            if (existing is not null &&
                string.Equals(existing.Sha256, validated.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult<DocumentUploadView>.Success(
                    new DocumentUploadView(MiExpedienteView.FromDomain(expediente), true));
            }

            if (expediente.State == EstadoExpediente.EnRevision)
            {
                return OperationResult<DocumentUploadView>.Conflict();
            }

            if (expediente.State == EstadoExpediente.Aprobado)
            {
                return OperationResult<DocumentUploadView>.Conflict();
            }

            var version = existing is null ? 1 : existing.Version + 1;
            var analysis = await AnalyzeSafelyAsync(validated, cancellationToken)
                .ConfigureAwait(false);
            var stored = await _documentStore
                .SaveAsync(
                    expediente.Id,
                    documentType,
                    version,
                    validated.Content,
                    validated.ContentType,
                    validated.OriginalFileName,
                    validated.Sha256,
                    cancellationToken)
                .ConfigureAwait(false);

            var document = DocumentoVerificacion.Create(
                    documentType,
                    stored.StorageKey,
                    validated.OriginalFileName,
                    stored.ContentType,
                    stored.SizeBytes,
                    stored.Sha256,
                    version,
                    _clock.UtcNow)
                .WithAnalysis(
                    analysis.DetectedName,
                    analysis.OcrStatus,
                    analysis.EditionStatus,
                    analysis.EditionDetected,
                    analysis.ElaAvailable,
                    analysis.MetadataAvailable,
                    analysis.EditionSignals,
                    analysis.UnavailableDetail);

            var expectedUpdatedAt = expediente.UpdatedAt;
            DocumentoVerificacion current;

            try
            {
                current = expediente.AddOrReplaceDocument(document, _clock.UtcNow);
            }
            catch (InvalidOperationException)
            {
                await DeleteQuietlyAsync(stored.StorageKey, cancellationToken).ConfigureAwait(false);
                return OperationResult<DocumentUploadView>.Conflict();
            }

            if (!await _expedientes
                    .TryReplaceAsync(expediente, expectedUpdatedAt, cancellationToken)
                    .ConfigureAwait(false))
            {
                await DeleteQuietlyAsync(stored.StorageKey, cancellationToken).ConfigureAwait(false);
                continue;
            }

            await _audit
                .AppendAsync(
                    CreateAudit(
                        expediente,
                        ownerAccountId,
                        "Solicitante",
                        VerificationAuditActions.DocumentUploaded,
                        documentType,
                        current.Version,
                        "Documento cifrado almacenado."),
                    cancellationToken)
                .ConfigureAwait(false);

            if (expediente.AutomaticReviewCompleted)
            {
                await _audit
                    .AppendAsync(
                        CreateAudit(
                            expediente,
                            ownerAccountId,
                            "Sistema",
                            VerificationAuditActions.AnalysisRecorded,
                            documentType,
                            current.Version,
                            "Análisis automático orientativo completado."),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (expediente.State == EstadoExpediente.EnRevision)
            {
                await TrySubmitPublicationAsync(expediente, unit, ownerAccountId, cancellationToken)
                    .ConfigureAwait(false);
            }

            return OperationResult<DocumentUploadView>.Success(
                new DocumentUploadView(MiExpedienteView.FromDomain(expediente), false));
        }

        return OperationResult<DocumentUploadView>.Conflict();
    }

    private async Task<(Expediente Expediente, Unit Unit)?> GetContextAsync(
        Guid ownerAccountId,
        CancellationToken cancellationToken)
    {
        var expediente = await _expedientes
            .FindByOwnerAsync(ownerAccountId, cancellationToken)
            .ConfigureAwait(false);

        if (expediente is not null)
        {
            var linkedUnit = await _units
                .FindByIdAsync(expediente.PublicationId, cancellationToken)
                .ConfigureAwait(false);

            return linkedUnit is null || linkedUnit.OwnerAccountId != ownerAccountId
                ? null
                : (expediente, linkedUnit);
        }

        var firstUnit = await _units
            .FindFirstOwnedAsync(ownerAccountId, cancellationToken)
            .ConfigureAwait(false);

        if (firstUnit is null)
        {
            return null;
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var id = Guid.NewGuid();
            var folio = $"UH-{_clock.UtcNow.Year}-{id:N}"[..20].ToUpperInvariant();
            var candidate = Expediente.Create(
                id,
                folio,
                ownerAccountId,
                firstUnit.Id,
                firstUnit.PropertyId,
                _clock.UtcNow);

            try
            {
                await _expedientes.AddAsync(candidate, cancellationToken).ConfigureAwait(false);
                return (candidate, firstUnit);
            }
            catch (ExpedienteAlreadyExistsException)
            {
                var existing = await _expedientes
                    .FindByOwnerAsync(ownerAccountId, cancellationToken)
                    .ConfigureAwait(false);

                if (existing is not null)
                {
                    var linkedUnit = await _units
                        .FindByIdAsync(existing.PublicationId, cancellationToken)
                        .ConfigureAwait(false);

                    return linkedUnit is null || linkedUnit.OwnerAccountId != ownerAccountId
                        ? null
                        : (existing, linkedUnit);
                }
            }
        }

        throw new InvalidOperationException("No se pudo crear el expediente.");
    }

    private async Task<DocumentAnalysisResult> AnalyzeSafelyAsync(
        ValidatedVerificationDocument document,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _analyzer
                .AnalyzeAsync(document.Content, document.ContentType, document.Type, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return DocumentAnalysisResult.Unavailable(
                "El análisis automático no está disponible; el documento requiere revisión manual.");
        }
    }

    private async Task TrySubmitPublicationAsync(
        Expediente expediente,
        Unit unit,
        Guid ownerAccountId,
        CancellationToken cancellationToken)
    {
        if (unit.PublicationState == Domain.Properties.PublicationState.EnRevision)
        {
            return;
        }

        if (unit.Photos.Count == 0)
        {
            return;
        }

        if (!await _terms.ExistsAnyAsync(ownerAccountId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (await _units
                .TrySubmitForReviewAsync(unit.Id, ownerAccountId, cancellationToken)
                .ConfigureAwait(false))
        {
            await _audit
                .AppendAsync(
                    CreateAudit(
                        expediente,
                        ownerAccountId,
                        "Sistema",
                        VerificationAuditActions.PublicationSubmitted,
                        documentType: null,
                        documentVersion: null,
                        detail: "Primera publicación en revisión, oculta de las vistas públicas."),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task DeleteQuietlyAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            await _documentStore
                .DeleteAsync(storageKey, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // La limpieza no oculta el conflicto original ni registra la clave.
        }
    }

    private VerificationAuditEntry CreateAudit(
        Expediente expediente,
        Guid actorAccountId,
        string actorRole,
        string action,
        TipoDocumentoVerificacion? documentType,
        int? documentVersion,
        string? detail) =>
        new(
            Guid.NewGuid(),
            expediente.Id,
            expediente.Folio,
            actorAccountId,
            actorRole,
            action,
            documentType,
            documentVersion,
            detail,
            _clock.UtcNow,
            Activity.Current?.Id);
}

