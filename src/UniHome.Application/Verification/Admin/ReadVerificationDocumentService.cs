using System.Diagnostics;
using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Verification.Abstractions;
using UniHome.Application.Verification.Documents;
using UniHome.Domain.Verification;

namespace UniHome.Application.Verification.Admin;

/// <summary>
/// Consume un enlace firmado, descifra el documento, valida su huella y audita el
/// acceso. Si la auditoría no puede persistirse, el acceso falla cerrado.
/// </summary>
public sealed class ReadVerificationDocumentService
{
    private readonly IVerificationRepository _expedientes;
    private readonly IVerificationDocumentStore _documentStore;
    private readonly IDocumentPreviewGenerator _previews;
    private readonly IDocumentUrlSigner _signer;
    private readonly IVerificationAuditRepository _audit;
    private readonly ISystemClock _clock;

    public ReadVerificationDocumentService(
        IVerificationRepository expedientes,
        IVerificationDocumentStore documentStore,
        IDocumentPreviewGenerator previews,
        IDocumentUrlSigner signer,
        IVerificationAuditRepository audit,
        ISystemClock clock)
    {
        _expedientes = expedientes;
        _documentStore = documentStore;
        _previews = previews;
        _signer = signer;
        _audit = audit;
        _clock = clock;
    }

    public async Task<OperationResult<VerificationDocumentReadView>> ReadAsync(
        Guid administratorAccountId,
        string? token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return OperationResult<VerificationDocumentReadView>.NotFound();
        }

        var now = _clock.UtcNow;
        var access = _signer.Validate(token, now);

        if (access is null)
        {
            return OperationResult<VerificationDocumentReadView>.NotFound();
        }

        var expediente = await _expedientes
            .FindByIdAsync(access.ExpedienteId, cancellationToken)
            .ConfigureAwait(false);
        var document = expediente?.FindDocument(access.DocumentType);

        if (expediente is null || document is null || document.Version != access.Version)
        {
            return OperationResult<VerificationDocumentReadView>.NotFound();
        }

        var stored = await _documentStore
            .ReadAsync(document.StorageKey, cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return OperationResult<VerificationDocumentReadView>.NotFound();
        }

        if (!string.Equals(stored.Sha256, document.Sha256, StringComparison.OrdinalIgnoreCase) ||
            stored.SizeBytes != document.SizeBytes)
        {
            throw new StorageUnavailableException();
        }

        var preview = await _previews
            .CreateAsync(stored, access.Variant == DocumentUrlVariant.Thumbnail, cancellationToken)
            .ConfigureAwait(false);

        await _audit
            .AppendAsync(
                new VerificationAuditEntry(
                    Guid.NewGuid(),
                    expediente.Id,
                    expediente.Folio,
                    administratorAccountId,
                    "Administrador",
                    VerificationAuditActions.DocumentViewed,
                    document.Tipo,
                    document.Version,
                    access.Variant == DocumentUrlVariant.Thumbnail
                        ? "Miniatura consultada."
                        : "Documento consultado.",
                    now,
                    Activity.Current?.Id),
                cancellationToken)
            .ConfigureAwait(false);

        return OperationResult<VerificationDocumentReadView>.Success(
            new VerificationDocumentReadView(
                preview.Content,
                preview.ContentType,
                document.OriginalFileName,
                preview.IsThumbnail));
    }
}
