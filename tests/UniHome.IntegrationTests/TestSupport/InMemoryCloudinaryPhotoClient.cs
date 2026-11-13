using UniHome.Infrastructure.Storage;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>
/// Doble exclusivo de pruebas para el SDK Cloudinary. Permite verificar que el
/// endpoint usa CloudinaryUnitPhotoStorage sin red ni credenciales reales.
/// </summary>
internal sealed class InMemoryCloudinaryPhotoClient : ICloudinaryPhotoClient
{
    private readonly object _gate = new();
    private readonly Dictionary<string, byte[]> _assets = new(StringComparer.Ordinal);

    public int UploadCount
    {
        get
        {
            lock (_gate)
            {
                return _assets.Count;
            }
        }
    }

    public Task<string> UploadAsync(
        string fileName,
        string contentType,
        byte[] content,
        string publicId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _assets[publicId] = content.ToArray();
        }

        return Task.FromResult(publicId);
    }

    public Task<CloudinaryDownload> DownloadAsync(
        string publicId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(
                _assets.TryGetValue(publicId, out var content)
                    ? new CloudinaryDownload(200, content.ToArray(), "image/png")
                    : new CloudinaryDownload(404, null, null));
        }
    }

    public Task DeleteAsync(
        string publicId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _assets.Remove(publicId);
        }

        return Task.CompletedTask;
    }
}
