namespace UniHome.Infrastructure.Storage;

internal sealed record CloudinaryDownload(int StatusCode, byte[]? Content, string? ContentType);

/// <summary>
/// Puerto interno que aísla el SDK oficial para poder probar errores, timeout y
/// limpieza sin red. La implementación real está en CloudinaryPhotoClient.
/// </summary>
internal interface ICloudinaryPhotoClient
{
    Task<string> UploadAsync(
        string fileName,
        string contentType,
        byte[] content,
        string publicId,
        CancellationToken cancellationToken = default);

    Task<CloudinaryDownload> DownloadAsync(
        string publicId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string publicId,
        CancellationToken cancellationToken = default);
}
