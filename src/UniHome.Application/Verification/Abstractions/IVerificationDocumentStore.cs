using UniHome.Domain.Verification;

namespace UniHome.Application.Verification.Abstractions;

public sealed record StoredVerificationDocument(
    string StorageKey,
    string ContentType,
    long SizeBytes,
    string Sha256);

public sealed record VerificationDocumentContent(
    byte[] Content,
    string ContentType,
    long SizeBytes,
    string Sha256);

/// <summary>
/// Almacenamiento privado de documentos. La implementación aplica cifrado
/// autenticado en reposo; el binario nunca se publica ni se sirve por una URL
/// permanente. La lectura siempre pasa por autorización administrativa y auditoría.
/// </summary>
public interface IVerificationDocumentStore
{
    Task<StoredVerificationDocument> SaveAsync(
        Guid expedienteId,
        TipoDocumentoVerificacion type,
        int version,
        byte[] content,
        string contentType,
        string originalFileName,
        string sha256,
        CancellationToken cancellationToken = default);

    Task<VerificationDocumentContent?> ReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
