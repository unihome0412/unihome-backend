using System.Diagnostics;
using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Verification.Abstractions;
using UniHome.Application.Verification.Documents;
using UniHome.Domain.Verification;

namespace UniHome.Application.Verification.Admin;

/// <summary>
/// EP-M06-05 (HU-016/HU-018/HU-020/HU-021/HU-022): detalle administrativo con
/// OCR, indicador de edición, señales de riesgo y enlaces temporales. El acceso
/// queda auditado antes de entregar datos personales.
/// </summary>
public sealed class GetExpedienteDetailService
{
    private readonly IVerificationRepository _expedientes;
    private readonly IAccountRepository _accounts;
    private readonly IDocumentUrlSigner _signer;
    private readonly IDocumentAccessUrlBuilder _urls;
    private readonly IVerificationAuditRepository _audit;
    private readonly ISystemClock _clock;

    public GetExpedienteDetailService(
        IVerificationRepository expedientes,
        IAccountRepository accounts,
        IDocumentUrlSigner signer,
        IDocumentAccessUrlBuilder urls,
        IVerificationAuditRepository audit,
        ISystemClock clock)
    {
        _expedientes = expedientes;
        _accounts = accounts;
        _signer = signer;
        _urls = urls;
        _audit = audit;
        _clock = clock;
    }

    public async Task<OperationResult<ExpedienteDetalleView>> GetAsync(
        Guid administratorAccountId,
        Guid expedienteId,
        CancellationToken cancellationToken = default)
    {
        var expediente = await _expedientes
            .FindByIdAsync(expedienteId, cancellationToken)
            .ConfigureAwait(false);

        if (expediente is null)
        {
            return OperationResult<ExpedienteDetalleView>.NotFound();
        }

        var account = await _accounts
            .FindByIdAsync(expediente.OwnerAccountId, cancellationToken)
            .ConfigureAwait(false);

        var ine = expediente.FindDocument(TipoDocumentoVerificacion.Ine);
        var comprobante = expediente.FindDocument(TipoDocumentoVerificacion.Comprobante);
        var now = _clock.UtcNow;

        var ineUrl = ine is null
            ? null
            : CreateThumbnailUrl(expediente.Id, ine, now);
        var comprobanteUrl = comprobante is null
            ? null
            : CreateThumbnailUrl(expediente.Id, comprobante, now);

        await _audit
            .AppendAsync(
                new VerificationAuditEntry(
                    Guid.NewGuid(),
                    expediente.Id,
                    expediente.Folio,
                    administratorAccountId,
                    "Administrador",
                    VerificationAuditActions.ExpedienteViewed,
                    DocumentType: null,
                    DocumentVersion: null,
                    Detail: "Detalle de expediente consultado.",
                    now,
                    Activity.Current?.Id),
                cancellationToken)
            .ConfigureAwait(false);

        var editionAvailable = expediente.Documents.Any(document =>
            document.EstadoEdicion == EstadoAnalisisDocumento.Completado);
        var editionDetected = expediente.Documents.Any(document => document.EdicionDetectada);
        var elaAvailable = expediente.Documents.Any(document => document.ElaDisponible);
        var metadataAvailable = expediente.Documents.Any(document => document.MetadatosDisponibles);
        var analysisDetail = expediente.Documents
            .Select(document => document.DetalleAnalisis)
            .Where(detail => !string.IsNullOrWhiteSpace(detail)).Select(detail => detail!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return OperationResult<ExpedienteDetalleView>.Success(
            new ExpedienteDetalleView(
                expediente.Id,
                expediente.Folio,
                account?.FullName ?? "Solicitante no disponible",
                account?.Email.Value,
                expediente.CreatedAt,
                expediente.State,
                ineUrl,
                comprobanteUrl,
                ine?.NombreOcr,
                comprobante?.NombreOcr,
                expediente.NameComparison,
                new IndicadorEdicionView(
                    editionDetected,
                    editionAvailable,
                    IndicadorEdicionView.Warning,
                    elaAvailable,
                    metadataAvailable,
                    analysisDetail),
                expediente.IsSuspicious,
                expediente.RiskIndicator.Signals,
                expediente.AutomaticReviewCompleted,
                expediente.RejectionReason));
    }

    private string CreateThumbnailUrl(
        Guid expedienteId,
        DocumentoVerificacion document,
        DateTimeOffset now)
    {
        var grant = _signer.Create(
            expedienteId,
            document.Tipo,
            document.Version,
            DocumentUrlVariant.Thumbnail,
            now);

        return _urls.Build(grant.Token);
    }
}
