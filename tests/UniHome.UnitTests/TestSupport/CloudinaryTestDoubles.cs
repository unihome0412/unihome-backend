using UniHome.Application.Units.Abstractions;
using UniHome.Infrastructure.Storage;

namespace UniHome.UnitTests.TestSupport;

internal sealed class FakeCloudinaryPhotoClient : ICloudinaryPhotoClient
{
    public List<(string PublicId, byte[] Content, string ContentType)> Uploaded { get; } = [];
    public List<string> Deleted { get; } = [];
    public byte[]? DownloadContent { get; set; }
    public string DownloadContentType { get; set; } = "image/png";
    public bool UploadFails { get; set; }
    public bool DownloadFails { get; set; }
    public bool DeleteFails { get; set; }
    public bool UploadCancelled { get; set; }

    public Task<string> UploadAsync(
        string fileName,
        string contentType,
        byte[] content,
        string publicId,
        CancellationToken cancellationToken = default)
    {
        if (UploadCancelled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (UploadFails)
        {
            throw new CloudinaryProviderException();
        }

        Uploaded.Add((publicId, content, contentType));
        return Task.FromResult(publicId);
    }

    public Task<CloudinaryDownload> DownloadAsync(
        string publicId,
        CancellationToken cancellationToken = default)
    {
        if (DownloadFails)
        {
            throw new CloudinaryProviderException();
        }

        return Task.FromResult(DownloadContent is null
            ? new CloudinaryDownload(404, null, null)
            : new CloudinaryDownload(200, DownloadContent, DownloadContentType));
    }

    public Task DeleteAsync(
        string publicId,
        CancellationToken cancellationToken = default)
    {
        if (DeleteFails)
        {
            throw new CloudinaryProviderException();
        }

        Deleted.Add(publicId);
        return Task.CompletedTask;
    }
}
