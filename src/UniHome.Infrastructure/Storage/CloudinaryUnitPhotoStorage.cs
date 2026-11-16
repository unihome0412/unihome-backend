using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Units.Photos;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Storage;

/// <summary>
/// Almacenamiento real en Cloudinary Free. El binario se sube como
/// authenticated y solo se entrega mediante el proxy autorizado de la API; la
/// URL firmada de Cloudinary no sale del backend. MongoDB conserva únicamente
/// el public_id y los metadatos.
/// </summary>
internal sealed class CloudinaryUnitPhotoStorage : IUnitPhotoStorage
{
    public const long MaxPhotoBytes = 5 * 1024 * 1024;

    private readonly ICloudinaryPhotoClient _client;
    private readonly CloudinaryOptions _options;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public CloudinaryUnitPhotoStorage(
        ICloudinaryPhotoClient client,
        IOptions<CloudinaryOptions> options,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _client = client;
        _options = options.Value;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<StoredUnitPhoto> SaveAsync(
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
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (buffer.Length == 0 || buffer.Length > MaxPhotoBytes)
        {
            throw new StorageUnavailableException();
        }

        var bytes = buffer.ToArray();
        var extension = contentType == "image/png" ? "png" : "jpg";
        var publicId = $"{_options.EffectiveFolder.Trim('/')}/{Guid.NewGuid():N}";
        var hash = Convert.ToHexString(SHA256.HashData(bytes));

        try
        {
            var storedPublicId = await _client
                .UploadAsync(
                    $"foto.{extension}",
                    contentType,
                    bytes,
                    publicId,
                    cancellationToken)
                .ConfigureAwait(false);

            return new StoredUnitPhoto(
                storedPublicId,
                contentType,
                bytes.LongLength,
                hash);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CloudinaryProviderException)
        {
            try
            {
                await _client.DeleteAsync(publicId, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Reconciliaci?n de blobs hu?rfanos; nunca se registra la clave.
            }

            throw new StorageUnavailableException();
        }
    }

    public async Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        try
        {
            await _client
                .DeleteAsync(storageKey, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CloudinaryProviderException)
        {
            throw new StorageUnavailableException();
        }
    }

    public async Task<UnitPhotoContent?> ReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        try
        {
            var download = await _client
                .DownloadAsync(storageKey, cancellationToken)
                .ConfigureAwait(false);

            if (download.StatusCode == 404 || download.Content is null)
            {
                return null;
            }

            return new UnitPhotoContent(
                new MemoryStream(download.Content, writable: false),
                download.ContentType ?? "application/octet-stream",
                download.Content.LongLength);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CloudinaryProviderException)
        {
            throw new StorageUnavailableException();
        }
    }

    public string GetReadUrl(string storageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        var path = $"/api/v1/fotos/{PhotoReference.Encode(storageKey)}";

        if (Uri.TryCreate(_options.PublicBaseUrl, UriKind.Absolute, out var publicBase) &&
            (publicBase.Scheme == Uri.UriSchemeHttps || publicBase.Scheme == Uri.UriSchemeHttp))
        {
            return publicBase.AbsoluteUri.TrimEnd('/') + path;
        }

        var request = _httpContextAccessor?.HttpContext?.Request;

        if (request?.Host.HasValue == true)
        {
            return $"{request.Scheme}://{request.Host.Value}{request.PathBase}{path}";
        }

        return path;
    }
}
