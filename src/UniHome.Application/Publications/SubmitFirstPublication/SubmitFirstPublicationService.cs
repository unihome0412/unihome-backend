using System.Diagnostics;
using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Profiles;
using UniHome.Application.Publications.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Verification.Abstractions;
using UniHome.Application.Verification.Documents;
using UniHome.Domain.Properties;
using UniHome.Domain.Verification;

namespace UniHome.Application.Publications.SubmitFirstPublication;

/// <summary>
/// EP-M05-08 (HU-013/HU-027): confirma el envío de la primera publicación a
/// revisión una vez completados términos, fotos y ambos documentos de M06. La
/// unidad queda En revisión y no se publica hasta una decisión favorable.
/// </summary>
public sealed class SubmitFirstPublicationService
{
    private readonly IUnitRepository _units;
    private readonly IVerificationRepository _expedientes;
    private readonly ITermsAcceptanceRepository _terms;
    private readonly IVerificationAuditRepository _audit;
    private readonly PublishingEligibilityService _eligibility;
    private readonly ISystemClock _clock;

    public SubmitFirstPublicationService(
        IUnitRepository units,
        IVerificationRepository expedientes,
        ITermsAcceptanceRepository terms,
        IVerificationAuditRepository audit,
        PublishingEligibilityService eligibility,
        ISystemClock clock)
    {
        _units = units;
        _expedientes = expedientes;
        _terms = terms;
        _audit = audit;
        _eligibility = eligibility;
        _clock = clock;
    }

    public async Task<OperationResult<FirstPublicationSubmissionView>> SubmitAsync(
        Guid ownerAccountId,
        CancellationToken cancellationToken = default)
    {
        if (!await _eligibility
                .IsEligibleAsync(ownerAccountId, cancellationToken)
                .ConfigureAwait(false))
        {
            return OperationResult<FirstPublicationSubmissionView>.Forbidden();
        }

        var unit = await _units
            .FindFirstOwnedAsync(ownerAccountId, cancellationToken)
            .ConfigureAwait(false);

        if (unit is null)
        {
            return OperationResult<FirstPublicationSubmissionView>.NotFound();
        }

        if (!await _terms.ExistsAnyAsync(ownerAccountId, cancellationToken).ConfigureAwait(false))
        {
            return OperationResult<FirstPublicationSubmissionView>.Unprocessable();
        }

        if (unit.Photos.Count == 0)
        {
            return OperationResult<FirstPublicationSubmissionView>.Unprocessable();
        }

        var expediente = await _expedientes
            .FindByOwnerAsync(ownerAccountId, cancellationToken)
            .ConfigureAwait(false);

        if (expediente is null ||
            !expediente.DocumentsReceived ||
            expediente.State == EstadoExpediente.Pendiente)
        {
            return OperationResult<FirstPublicationSubmissionView>.Unprocessable();
        }

        if (expediente.State == EstadoExpediente.Rechazado)
        {
            return OperationResult<FirstPublicationSubmissionView>.Unprocessable();
        }

        if (expediente.State == EstadoExpediente.EnRevision &&
            unit.PublicationState == PublicationState.EnRevision)
        {
            return OperationResult<FirstPublicationSubmissionView>.Success(
                CreateView(unit, expediente.SubmittedAt ?? expediente.UpdatedAt));
        }

        if (expediente.State == EstadoExpediente.Aprobado &&
            unit.PublicationState == PublicationState.Aprobada)
        {
            return OperationResult<FirstPublicationSubmissionView>.Success(
                CreateView(unit, expediente.SubmittedAt ?? expediente.UpdatedAt));
        }

        var wasAlreadySubmitted = unit.PublicationState == PublicationState.EnRevision;

        if (!await _units
                .TrySubmitForReviewAsync(unit.Id, ownerAccountId, cancellationToken)
                .ConfigureAwait(false))
        {
            return OperationResult<FirstPublicationSubmissionView>.Conflict();
        }

        if (!wasAlreadySubmitted)
        {
            var now = _clock.UtcNow;

            await _audit
                .AppendAsync(
                    new VerificationAuditEntry(
                        Guid.NewGuid(),
                        expediente.Id,
                        expediente.Folio,
                        ownerAccountId,
                        "Solicitante",
                        VerificationAuditActions.PublicationSubmitted,
                        DocumentType: null,
                        DocumentVersion: null,
                        Detail: "Primera publicación enviada a revisión.",
                        now,
                        Activity.Current?.Id),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var updated = await _units
            .FindByIdAsync(unit.Id, cancellationToken)
            .ConfigureAwait(false);

        return updated is null
            ? OperationResult<FirstPublicationSubmissionView>.NotFound()
            : OperationResult<FirstPublicationSubmissionView>.Success(
                CreateView(updated, expediente.SubmittedAt ?? _clock.UtcNow));
    }

    private static FirstPublicationSubmissionView CreateView(
        Domain.Units.Unit unit,
        DateTimeOffset submittedAt) =>
        new(unit.Id, unit.PublicationState, submittedAt);
}
