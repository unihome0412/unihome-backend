using System.Diagnostics;
using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Verification.Abstractions;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.Domain.Verification;

namespace UniHome.Application.Verification.Admin;

/// <summary>
/// EP-M06-07/EP-M06-08 (HU-024/HU-025): decisión humana explícita sobre un
/// expediente En revisión. La operación es idempotente solo en el sentido de
/// rechazar duplicados; nunca aprueba o rechaza por señales automáticas.
///
/// Consistencia: el expediente se actualiza con CAS antes que la unidad. Si la
/// unidad o la auditoría fallan, se compensa la decisión para no dejar un estado
/// administrativo sin efecto. Si la compensación también falla, se devuelve 503
/// y la publicación permanece oculta (nunca se publica por accidente).
/// </summary>
public sealed class DecideExpedienteService
{
    private readonly IVerificationRepository _expedientes;
    private readonly IUnitRepository _units;
    private readonly IVerificationAuditRepository _audit;
    private readonly ISystemClock _clock;

    public DecideExpedienteService(
        IVerificationRepository expedientes,
        IUnitRepository units,
        IVerificationAuditRepository audit,
        ISystemClock clock)
    {
        _expedientes = expedientes;
        _units = units;
        _audit = audit;
        _clock = clock;
    }

    public Task<OperationResult<Expediente>> ApproveAsync(
        Guid administratorAccountId,
        Guid expedienteId,
        CancellationToken cancellationToken = default) =>
        DecideAsync(
            administratorAccountId,
            expedienteId,
            decision: ExpedienteDecisionKind.Approve,
            reason: null,
            cancellationToken);

    public Task<OperationResult<Expediente>> RejectAsync(
        Guid administratorAccountId,
        Guid expedienteId,
        string? reason,
        CancellationToken cancellationToken = default) =>
        DecideAsync(
            administratorAccountId,
            expedienteId,
            decision: ExpedienteDecisionKind.Reject,
            reason,
            cancellationToken);

    private async Task<OperationResult<Expediente>> DecideAsync(
        Guid administratorAccountId,
        Guid expedienteId,
        ExpedienteDecisionKind decision,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (administratorAccountId == Guid.Empty)
        {
            return OperationResult<Expediente>.Forbidden();
        }

        var expediente = await _expedientes
            .FindByIdAsync(expedienteId, cancellationToken)
            .ConfigureAwait(false);

        if (expediente is null)
        {
            return OperationResult<Expediente>.NotFound();
        }

        if (expediente.State != EstadoExpediente.EnRevision ||
            !expediente.DocumentsReceived)
        {
            return OperationResult<Expediente>.Conflict();
        }

        var unit = await _units
            .FindByIdAsync(expediente.PublicationId, cancellationToken)
            .ConfigureAwait(false);

        if (unit is null ||
            unit.OwnerAccountId != expediente.OwnerAccountId ||
            unit.PublicationState != PublicationState.EnRevision)
        {
            return OperationResult<Expediente>.Conflict();
        }

        var now = _clock.UtcNow;
        var expectedUpdatedAt = expediente.UpdatedAt;

        try
        {
            if (decision == ExpedienteDecisionKind.Approve)
            {
                expediente.Approve(administratorAccountId, now);
            }
            else
            {
                expediente.Reject(reason, administratorAccountId, now);
            }
        }
        catch (ArgumentException exception)
        {
            return OperationResult<Expediente>.ValidationFailed("motivo", exception.Message);
        }
        catch (InvalidOperationException)
        {
            return OperationResult<Expediente>.Conflict();
        }

        var decisionUpdatedAt = expediente.UpdatedAt;

        if (!await _expedientes
                .TryReplaceAsync(expediente, expectedUpdatedAt, cancellationToken)
                .ConfigureAwait(false))
        {
            return OperationResult<Expediente>.Conflict();
        }

        var nextState = decision == ExpedienteDecisionKind.Approve
            ? PublicationState.Aprobada
            : PublicationState.Rechazada;

        try
        {
            var transitioned = await _units
                .TryTransitionPublicationStateAsync(
                    unit.Id,
                    expediente.OwnerAccountId,
                    PublicationState.EnRevision,
                    nextState,
                    CancellationToken.None)
                .ConfigureAwait(false);

            if (!transitioned)
            {
                var current = await _units
                    .FindByIdAsync(unit.Id, CancellationToken.None)
                    .ConfigureAwait(false);

                if (current?.PublicationState != nextState)
                {
                    await CompensateAsync(
                            expediente,
                            decisionUpdatedAt,
                            nextState,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                    return OperationResult<Expediente>.Conflict();
                }
            }

            await _audit
                .AppendAsync(
                    new VerificationAuditEntry(
                        Guid.NewGuid(),
                        expediente.Id,
                        expediente.Folio,
                        administratorAccountId,
                        "Administrador",
                        decision == ExpedienteDecisionKind.Approve
                            ? VerificationAuditActions.ExpedienteApproved
                            : VerificationAuditActions.ExpedienteRejected,
                        DocumentType: null,
                        DocumentVersion: null,
                        Detail: decision == ExpedienteDecisionKind.Approve
                            ? "Expediente aprobado por decisión administrativa."
                            : $"Expediente rechazado. Motivo: {expediente.RejectionReason}",
                        now,
                        Activity.Current?.Id),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (StorageUnavailableException)
        {
            await CompensateAsync(
                    expediente,
                    decisionUpdatedAt,
                    nextState,
                    CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }

        return OperationResult<Expediente>.Success(expediente);
    }

    private async Task CompensateAsync(
        Expediente expediente,
        DateTimeOffset decisionUpdatedAt,
        PublicationState nextState,
        CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                await _units
                    .TryTransitionPublicationStateAsync(
                        expediente.PublicationId,
                        expediente.OwnerAccountId,
                        nextState,
                        PublicationState.EnRevision,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (StorageUnavailableException)
            {
                // Se comprueba el estado real antes de revertir el expediente.
            }

            var current = await _units
                .FindByIdAsync(expediente.PublicationId, cancellationToken)
                .ConfigureAwait(false);

            if (current?.PublicationState != PublicationState.EnRevision)
            {
                throw new StorageUnavailableException();
            }

            expediente.RevertDecisionForCompensation(_clock.UtcNow);
            var reverted = await _expedientes
                .TryReplaceAsync(expediente, decisionUpdatedAt, cancellationToken)
                .ConfigureAwait(false);

            if (!reverted)
            {
                throw new StorageUnavailableException();
            }

            await _audit
                .AppendAsync(
                    new VerificationAuditEntry(
                        Guid.NewGuid(),
                        expediente.Id,
                        expediente.Folio,
                        Guid.Empty,
                        "Sistema",
                        VerificationAuditActions.DecisionCompensated,
                        DocumentType: null,
                        DocumentVersion: null,
                        Detail: "La decisión se revirtió por un fallo de sincronización.",
                        _clock.UtcNow,
                        Activity.Current?.Id),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (StorageUnavailableException)
        {
            throw;
        }
    }

    private enum ExpedienteDecisionKind
    {
        Approve,
        Reject
    }
}
