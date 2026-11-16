using System.Diagnostics;
using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Verification.Abstractions;
using UniHome.Domain.Verification;

namespace UniHome.Application.Verification.Admin;

public sealed record DocumentAccessUrlView(
    string Url,
    DateTimeOffset ExpiraEn,
    TipoDocumentoVerificacion Tipo);

/// <summary>
/// EP-M06-06 (HU-016/HU-088): emite un enlace firmado de corta vida. La emisión
/// queda auditada y el enlace sigue exigiendo sesión administrativa.
/// </summary>
public sealed class IssueDocumentAccessUrlService
{
    private readonly IVerificationRepository _expedientes;
    private readonly IDocumentUrlSigner _signer;
    private readonly IDocumentAccessUrlBuilder _urls;
    private readonly IVerificationAuditRepository _audit;
    private readonly ISystemClock _clock;

    public IssueDocumentAccessUrlService(
        IVerificationRepository expedientes,
        IDocumentUrlSigner signer,
        IDocumentAccessUrlBuilder urls,
        IVerificationAuditRepository audit,
        ISystemClock clock)
    {
        _expedientes = expedientes;
        _signer = signer;
        _urls = urls;
        _audit = audit;
        _clock = clock;
    }

    public async Task<OperationResult<DocumentAccessUrlView>> IssueAsync(
        Guid administratorAccountId,
        Guid expedienteId,
        TipoDocumentoVerificacion documentType,
        CancellationToken cancellationToken = default)
    {
        var expediente = await _expedientes
            .FindByIdAsync(expedienteId, cancellationToken)
            .ConfigureAwait(false);
        var document = expediente?.FindDocument(documentType);

        if (expediente is null || document is null)
        {
            return OperationResult<DocumentAccessUrlView>.NotFound();
        }

        var now = _clock.UtcNow;
        var grant = _signer.Create(
            expediente.Id,
            document.Tipo,
            document.Version,
            DocumentUrlVariant.Full,
            now);

        await _audit
            .AppendAsync(
                new VerificationAuditEntry(
                    Guid.NewGuid(),
                    expediente.Id,
                    expediente.Folio,
                    administratorAccountId,
                    "Administrador",
                    VerificationAuditActions.DocumentUrlIssued,
                    document.Tipo,
                    document.Version,
                    Detail: "Enlace temporal de documento emitido.",
                    now,
                    Activity.Current?.Id),
                cancellationToken)
            .ConfigureAwait(false);

        return OperationResult<DocumentAccessUrlView>.Success(
            new DocumentAccessUrlView(
                _urls.Build(grant.Token),
                grant.ExpiresAt,
                document.Tipo));
    }
}


