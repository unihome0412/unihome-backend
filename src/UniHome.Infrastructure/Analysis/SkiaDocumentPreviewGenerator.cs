using SkiaSharp;
using UniHome.Application.Verification.Abstractions;

namespace UniHome.Infrastructure.Analysis;

internal sealed class SkiaDocumentPreviewGenerator : IDocumentPreviewGenerator
{
    private const int ThumbnailMaxSide = 640;

    public Task<DocumentPreviewContent> CreateAsync(
        VerificationDocumentContent document,
        bool thumbnail,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!thumbnail ||
            document.ContentType is not ("image/jpeg" or "image/png"))
        {
            return Task.FromResult(new DocumentPreviewContent(
                document.Content,
                document.ContentType,
                IsThumbnail: false));
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var bitmap = SKBitmap.Decode(document.Content);

            if (bitmap is null)
            {
                return Task.FromResult(Original(document));
            }

            var maxSide = Math.Max(bitmap.Width, bitmap.Height);
            var scale = maxSide > ThumbnailMaxSide
                ? (double)ThumbnailMaxSide / maxSide
                : 1d;
            var width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
            var height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
            using var resized = bitmap.Resize(
                new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));

            if (resized is null)
            {
                return Task.FromResult(Original(document));
            }

            using var image = SKImage.FromBitmap(resized);
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 82);

            if (encoded is null)
            {
                return Task.FromResult(Original(document));
            }

            return Task.FromResult(new DocumentPreviewContent(
                encoded.ToArray(),
                "image/jpeg",
                IsThumbnail: true));
        }
        catch (Exception)
        {
            return Task.FromResult(Original(document));
        }
    }

    private static DocumentPreviewContent Original(VerificationDocumentContent document) =>
        new(document.Content, document.ContentType, IsThumbnail: false);
}
