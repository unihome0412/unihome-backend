using UniHome.Application.Units.Abstractions;

namespace UniHome.IntegrationTests.TestSupport;

/// <summary>Doble exclusivo de pruebas; no se registra en producción.</summary>
public sealed class InMemoryUnitPhotoStorage : IUnitPhotoStorage
{
    private readonly object _gate = new();
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public Task<StoredUnitPhoto> SaveAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        content.CopyTo(memory);
        var bytes = memory.ToArray();
        var key = $"test-photo-{Guid.NewGuid():N}";

        lock (_gate)
        {
            _files[key] = bytes;
        }

        return Task.FromResult(new StoredUnitPhoto(
            key,
            contentType,
            bytes.LongLength,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))));
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _files.Remove(storageKey);
        }

        return Task.CompletedTask;
    }

    public string GetReadUrl(string storageKey) => $"https://photos.test/{storageKey}";

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _files.Count;
            }
        }
    }
}
