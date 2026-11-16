using System.Security.Cryptography;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Units.Photos;

namespace UniHome.Infrastructure.Storage;

/// <summary>
/// Almacenamiento efímero en memoria para los harnesses DAST/RESTler aislados.
/// No escribe en disco, no usa Cloudinary y solo se registra en Development
/// cuando <c>Cloudinary:UseEphemeralStorage=true</c>. Producción mantiene el
/// adaptador real o el modo fail-closed.
/// </summary>
internal sealed class EphemeralUnitPhotoStorage : IUnitPhotoStorage
{
    public const long MaxPhotoBytes = 5 * 1024 * 1024;

    private readonly object _gate = new();
    private readonly Dictionary<string, StoredPhoto> _files = new(StringComparer.Ordinal);

    public Task<StoredUnitPhoto> SaveAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (contentType is not ("image/jpeg" or "image/png"))
        {
            throw new StorageUnavailableException();
        }

        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        var bytes = buffer.ToArray();

        if (bytes.Length == 0 || bytes.LongLength > MaxPhotoBytes)
        {
            throw new StorageUnavailableException();
        }

        var key = $"ephemeral/{Guid.NewGuid():N}";
        var stored = new StoredPhoto(
            bytes,
            contentType,
            Convert.ToHexString(SHA256.HashData(bytes)));

        lock (_gate)
        {
            _files[key] = stored;
        }

        return Task.FromResult(new StoredUnitPhoto(
            key,
            stored.ContentType,
            stored.Bytes.LongLength,
            stored.Sha256));
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        lock (_gate)
        {
            _files.Remove(storageKey);
        }

        return Task.CompletedTask;
    }

    public Task<UnitPhotoContent?> ReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        lock (_gate)
        {
            if (!_files.TryGetValue(storageKey, out var stored))
            {
                return Task.FromResult<UnitPhotoContent?>(null);
            }

            var stream = new MemoryStream(stored.Bytes, writable: false);
            return Task.FromResult<UnitPhotoContent?>(
                new UnitPhotoContent(stream, stored.ContentType, stored.Bytes.LongLength));
        }
    }

    public string GetReadUrl(string storageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        return $"/api/v1/fotos/{PhotoReference.Encode(storageKey)}";
    }

    private sealed record StoredPhoto(byte[] Bytes, string ContentType, string Sha256);
}
