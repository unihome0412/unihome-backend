using System.Globalization;
using SkiaSharp;
using UniHome.Domain.Verification;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Analysis;

internal sealed record EditionAnalysis(
    EstadoAnalisisDocumento Status,
    bool Detected,
    bool ElaAvailable,
    bool MetadataAvailable,
    IReadOnlyList<string> Signals,
    string? Detail);

/// <summary>
/// Análisis de edición de imágenes: ELA real (recompresión JPEG con SkiaSharp y
/// diferencia de píxeles) y metadatos EXIF/XMP. Produce señales orientativas;
/// nunca un veredicto de fraude (HU-020). Los PDF no se rasterizan localmente.
/// </summary>
internal static class ImageForensics
{
    public static EditionAnalysis AnalyzeImage(
        byte[] content,
        DocumentAnalysisOptions options)
    {
        int width;
        int height;

        try
        {
            using var codec = SKCodec.Create(new MemoryStream(content, writable: false));

            if (codec is null)
            {
                return Unavailable("No se pudo identificar la imagen para analizarla.");
            }

            width = codec.Info.Width;
            height = codec.Info.Height;
        }
        catch (Exception)
        {
            return Unavailable("No se pudo identificar la imagen para analizarla.");
        }

        var pixels = (long)width * height;

        if (pixels <= 0 || pixels > options.MaxImagePixels)
        {
            return Unavailable(
                "La imagen excede el límite de píxeles para el análisis automático.");
        }

        using var bitmap = SKBitmap.Decode(content);

        if (bitmap is null)
        {
            return Unavailable("No se pudo decodificar la imagen para analizarla.");
        }

        var metadata = ImageMetadataInspector.Inspect(content);
        var signals = new List<string>(metadata.Signals);
        var elaAvailable = false;
        string? detail = metadata.Detail;

        try
        {
            var (mean, ratio) = ComputeEla(bitmap, options);
            elaAvailable = true;

            if (mean >= options.ElaMeanDifferenceThreshold &&
                ratio >= options.ElaHighDifferenceRatioThreshold)
            {
                signals.Add(
                    $"ELA: diferencia media {mean.ToString("F1", CultureInfo.InvariantCulture)} " +
                    $"y proporción de píxeles alterados " +
                    $"{ratio.ToString("P1", CultureInfo.InvariantCulture)}.");
            }
        }
        catch (Exception)
        {
            detail = "ELA no disponible para esta imagen; se conservan los metadatos.";
        }

        var status = metadata.Available || elaAvailable
            ? EstadoAnalisisDocumento.Completado
            : EstadoAnalisisDocumento.NoDisponible;

        return new EditionAnalysis(
            status,
            signals.Count > 0,
            elaAvailable,
            metadata.Available,
            signals,
            detail);
    }

    public static EditionAnalysis AnalyzePdfMetadata(string? producer, string? creator)
    {
        var signals = new List<string>();
        var metadataText = string.Join(' ', new[] { producer, creator }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        if (metadataText.Length > 0)
        {
            var tool = ImageMetadataInspector.DetectEditingTool(metadataText);

            if (tool is not null)
            {
                signals.Add($"Metadatos del PDF indican edición con {tool}.");
            }
        }

        return new EditionAnalysis(
            EstadoAnalisisDocumento.Completado,
            signals.Count > 0,
            ElaAvailable: false,
            MetadataAvailable: true,
            signals,
            "ELA no aplicable a PDF; se analizaron los metadatos del documento.");
    }

    private static (double Mean, double Ratio) ComputeEla(
        SKBitmap bitmap,
        DocumentAnalysisOptions options)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(
            SKEncodedImageFormat.Jpeg,
            (int)Math.Round(options.ElaJpegQuality));

        if (encoded is null)
        {
            throw new InvalidOperationException("La recompresión JPEG falló.");
        }

        using var recompressed = SKBitmap.Decode(encoded.ToArray());

        if (recompressed is null ||
            recompressed.Width != bitmap.Width ||
            recompressed.Height != bitmap.Height)
        {
            throw new InvalidOperationException("La recompresión cambió las dimensiones.");
        }

        long totalDifference = 0;
        long highDifferencePixels = 0;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var original = bitmap.GetPixel(x, y);
                var compressed = recompressed.GetPixel(x, y);
                var difference =
                    (Math.Abs(original.Red - compressed.Red) +
                     Math.Abs(original.Green - compressed.Green) +
                     Math.Abs(original.Blue - compressed.Blue)) / 3;

                totalDifference += difference;

                if (difference >= options.ElaHighDifferenceLevel)
                {
                    highDifferencePixels++;
                }
            }
        }

        var pixelCount = (double)bitmap.Width * bitmap.Height;

        if (pixelCount <= 0)
        {
            throw new InvalidOperationException("La imagen no tiene píxeles.");
        }

        return (
            totalDifference / pixelCount,
            highDifferencePixels / pixelCount);
    }

    private static EditionAnalysis Unavailable(string detail) =>
        new(
            EstadoAnalisisDocumento.NoDisponible,
            Detected: false,
            ElaAvailable: false,
            MetadataAvailable: false,
            Signals: [],
            detail);
}
