using UniHome.Application.Verification.Abstractions;

namespace UniHome.Application.Verification.Abstractions;

public sealed record DocumentPreviewContent(
    byte[] Content,
    string ContentType,
    bool IsThumbnail);

/// <summary>
/// Genera una vista previa para la bandeja administrativa. Para imágenes produce
/// una miniatura JPEG; para PDF no hay rasterizador local aprobado y se devuelve
/// el PDF original como vista previa, sin declarar una miniatura inexistente.
/// </summary>
public interface IDocumentPreviewGenerator
{
    Task<DocumentPreviewContent> CreateAsync(
        VerificationDocumentContent document,
        bool thumbnail,
        CancellationToken cancellationToken = default);
}
