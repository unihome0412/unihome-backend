using SkiaSharp;
using UniHome.Application.Verification.Abstractions;

namespace UniHome.Infrastructure.Analysis;

internal sealed class SkiaDocumentPreviewGenerator : IDocumentPreviewGenerator
{
    private const int ThumbnailMaxSide = 640;

    private readonly PdfRasterizer _pdfRasterizer;

    public SkiaDocumentPreviewGenerator(PdfRasterizer pdfRasterizer)
    {
        _pdfRasterizer = pdfRasterizer;
    }

    public async Task<DocumentPreviewContent> CreateAsync(
        VerificationDocumentContent document,
        bool thumbnail,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!thumbnail)
        {
            return Original(document);
        }

        if (document.ContentType == "application/pdf")
        {
            var raster = await _pdfRasterizer
                .RasterizeAsync(document.Content, cancellationToken)
                .ConfigureAwait(false);

            if (!raster.Available || raster.Failed || raster.Pages.Count == 0)
            {
                return Original(document);
            }

            return CreateThumbnail(raster.Pages[0]) ?? Original(document);
        }

        if (document.ContentType is "image/jpeg" or "image/png")
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CreateThumbnail(document.Content) ?? Original(document);
        }

        return Original(document);
    }

    private static DocumentPreviewContent? CreateThumbnail(byte[] content)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(content);

            if (bitmap is null)
            {
                return null;
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
                return null;
            }

            using var image = SKImage.FromBitmap(resized);
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 82);

            return encoded is null
                ? null
                : new DocumentPreviewContent(
                    encoded.ToArray(),
                    "image/jpeg",
                    IsThumbnail: true);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static DocumentPreviewContent Original(VerificationDocumentContent document) =>
        new(document.Content, document.ContentType, IsThumbnail: false);
}
