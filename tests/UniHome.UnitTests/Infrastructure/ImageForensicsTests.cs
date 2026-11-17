using SkiaSharp;
using UniHome.Application.Verification.Abstractions;
using UniHome.Domain.Verification;
using UniHome.Infrastructure.Analysis;
using UniHome.Infrastructure.Configuration;

namespace UniHome.UnitTests.Infrastructure;

public sealed class ImageForensicsTests
{
    [Fact]
    public void Ela_SeEjecutaSobreUnaImagenRealYNoInventaDisponibilidad()
    {
        var jpeg = CreateJpeg(160, 120, edited: true);
        var options = new DocumentAnalysisOptions();

        var result = ImageForensics.AnalyzeImage(jpeg, options);

        Assert.True(result.ElaAvailable);
        Assert.Equal(EstadoAnalisisDocumento.Completado, result.Status);
        Assert.NotNull(result.Signals);
    }

    [Fact]
    public void ImagenConDimensionesExcesivas_QuedaNoDisponible()
    {
        var jpeg = CreateJpeg(20, 20, edited: false);
        var options = new DocumentAnalysisOptions { MaxImagePixels = 100 };

        var result = ImageForensics.AnalyzeImage(jpeg, options);

        Assert.False(result.ElaAvailable);
        Assert.Equal(EstadoAnalisisDocumento.NoDisponible, result.Status);
    }

    [Fact]
    public void DetectorDeHerramientas_EsCaseInsensitive()
    {
        Assert.Equal("photoshop", ImageMetadataInspector.DetectEditingTool("Adobe Photoshop 25.0"));
        Assert.Null(ImageMetadataInspector.DetectEditingTool("Microsoft Word"));
    }

    [Fact]
    public void PdfMetadata_DetectaEditorSinDeclararEla()
    {
        var result = ImageForensics.AnalyzePdfMetadata("Adobe Photoshop", "Photoshop");

        Assert.True(result.Detected);
        Assert.True(result.MetadataAvailable);
        Assert.False(result.ElaAvailable);
        Assert.Contains(result.Signals, signal =>
            signal.Contains("photoshop", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Miniatura_DeImagen_DebeSerJpegYNoSerPdf()
    {
        var jpeg = CreateJpeg(800, 600, edited: false);
        var generator = new SkiaDocumentPreviewGenerator();
        var document = new VerificationDocumentContent(
            jpeg,
            "image/jpeg",
            jpeg.LongLength,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(jpeg)));

        var preview = await generator.CreateAsync(document, thumbnail: true);

        Assert.True(preview.IsThumbnail);
        Assert.Equal("image/jpeg", preview.ContentType);
        using var codec = SKCodec.Create(new MemoryStream(preview.Content));
        Assert.NotNull(codec);
        Assert.True(codec!.Info.Width <= 640);
        Assert.True(codec.Info.Height <= 640);
    }

    [Fact]
    public async Task Miniatura_DePdf_DebeDevolverOriginalSinDeclararlaMiniatura()
    {
        var pdf = "%PDF-1.4\n%%EOF"u8.ToArray();
        var generator = new SkiaDocumentPreviewGenerator();
        var document = new VerificationDocumentContent(
            pdf,
            "application/pdf",
            pdf.LongLength,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdf)));

        var preview = await generator.CreateAsync(document, thumbnail: true);

        Assert.False(preview.IsThumbnail);
        Assert.Equal("application/pdf", preview.ContentType);
        Assert.Equal(pdf, preview.Content);
    }

    [Fact]
    public void ImagenInvalidaQuedaNoDisponibleSinLanzar()
    {
        var result = ImageForensics.AnalyzeImage([1, 2, 3, 4], new DocumentAnalysisOptions());

        Assert.Equal(EstadoAnalisisDocumento.NoDisponible, result.Status);
        Assert.False(result.ElaAvailable);
        Assert.False(result.MetadataAvailable);
        Assert.Empty(result.Signals);
    }

    [Fact]
    public void ElaConUmbralesCero_DebeReportarDiferenciaSinInventarMetadatos()
    {
        var jpeg = CreateUniformJpeg(40, 30);
        var options = new DocumentAnalysisOptions
        {
            ElaMeanDifferenceThreshold = 0,
            ElaHighDifferenceRatioThreshold = 0,
            ElaHighDifferenceLevel = 1
        };

        var result = ImageForensics.AnalyzeImage(jpeg, options);

        Assert.True(result.ElaAvailable);
        Assert.Equal(EstadoAnalisisDocumento.Completado, result.Status);
        Assert.Contains(result.Signals, signal =>
            signal.Contains("ELA", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PdfSinMetadatos_NoDebeInventarSenales()
    {
        var result = ImageForensics.AnalyzePdfMetadata(null, " ");

        Assert.Equal(EstadoAnalisisDocumento.Completado, result.Status);
        Assert.False(result.Detected);
        Assert.True(result.MetadataAvailable);
        Assert.Empty(result.Signals);
    }

    private static byte[] CreateUniformJpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 94);
        return data.ToArray();
    }

    private static byte[] CreateJpeg(int width, int height, bool edited)
    {
        using var bitmap = new SKBitmap(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var baseValue = (byte)((x * 3 + y * 5) % 220 + 20);
                var selected = edited && x > width / 2 && y > height / 3;
                var value = selected
                    ? (byte)Math.Min(255, baseValue + 25)
                    : baseValue;

                bitmap.SetPixel(x, y, new SKColor(value, (byte)(value / 2), (byte)(255 - value)));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 94);
        return data.ToArray();
    }
}
