using System.Diagnostics.CodeAnalysis;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Storage;

/// <summary>
/// Adaptador del SDK oficial CloudinaryDotNet. Las imágenes se suben con
/// delivery type authenticated; las URLs firmadas se generan solo en servidor y
/// nunca se serializan al cliente. El flag incoming strip_profile elimina EXIF
/// (incluida ubicación GPS) antes de almacenar la imagen.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class CloudinaryPhotoClient : ICloudinaryPhotoClient, IDisposable
{
    private const int MaxDownloadBytes = 10 * 1024 * 1024;

    private readonly Cloudinary _cloudinary;
    private readonly HttpClient _httpClient;

    public CloudinaryPhotoClient(IOptions<CloudinaryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value;

        _cloudinary = new Cloudinary(new Account(
            value.CloudName!,
            value.ApiKey!,
            value.ApiSecret!))
        {
            Api =
            {
                Timeout = checked(value.EffectiveTimeoutSeconds * 1000)
            }
        };

        _httpClient = new HttpClient
        {
            Timeout = value.Timeout
        };
    }

    public async Task<string> UploadAsync(
        string fileName,
        string contentType,
        byte[] content,
        string publicId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicId);

        try
        {
            await using var stream = new MemoryStream(content, writable: false);
            var parameters = new ImageUploadParams
            {
                File = new FileDescription(fileName, stream),
                PublicId = publicId,
                Type = "authenticated",
                Overwrite = false,
                UniqueFilename = false,
                UseFilename = false,
                Transformation = new Transformation().Add("flags", "strip_profile")
            };
            var result = await _cloudinary
                .UploadAsync(parameters, cancellationToken)
                .ConfigureAwait(false);

            if (result.Error is not null || string.IsNullOrWhiteSpace(result.PublicId))
            {
                throw new CloudinaryProviderException();
            }

            return result.PublicId;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CloudinaryProviderException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new CloudinaryProviderException(exception);
        }
    }

    public async Task<CloudinaryDownload> DownloadAsync(
        string publicId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicId);

        try
        {
            var signedUrl = _cloudinary.Api.UrlImgUp
                .Type("authenticated")
                .Signed(true)
                .BuildUrl(publicId);

            using var response = await _httpClient
                .GetAsync(signedUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new CloudinaryDownload(404, null, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new CloudinaryProviderException();
            }

            var content = await response.Content
                .ReadAsByteArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            if (content.Length > MaxDownloadBytes)
            {
                throw new CloudinaryProviderException();
            }

            return new CloudinaryDownload(
                200,
                content,
                response.Content.Headers.ContentType?.MediaType);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CloudinaryProviderException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new CloudinaryProviderException(exception);
        }
    }

    public async Task DeleteAsync(
        string publicId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicId);

        try
        {
            var result = await _cloudinary
                .DeleteResourcesAsync(
                    new DelResParams
                    {
                        PublicIds = [publicId],
                        Type = "authenticated",
                        Invalidate = true
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (result.Error is not null &&
                !result.Error.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                throw new CloudinaryProviderException();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CloudinaryProviderException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new CloudinaryProviderException(exception);
        }
    }

    public void Dispose() => _httpClient.Dispose();
}
