using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using UniHome.Domain.Verification;
using UniHome.Infrastructure.Analysis;
using UniHome.Infrastructure.Configuration;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Infrastructure;

public sealed class RealOcrTests
{
    [RealOcrFact]
    public async Task TesseractLocal_DebeLeerUnNombreRealSinSimulacion()
    {
        var executable = RealOcrFactAttribute.ResolveExecutable();
        Assert.NotNull(executable);

        var options = new DocumentAnalysisOptions
        {
            TesseractExecutablePath = executable!,
            TesseractLanguage = "eng",
            TesseractTimeoutSeconds = 60,
            TesseractPageSegmentationMode = 6
        };
        var engine = new TesseractCliOcrEngine(
            Options.Create(options),
            NullLogger<TesseractCliOcrEngine>.Instance);
        var analyzer = new LocalDocumentAnalyzer(
            Options.Create(options),
            engine,
            NullLogger<LocalDocumentAnalyzer>.Instance);
        var image = CreateNameImage();

        var result = await analyzer.AnalyzeAsync(
            image,
            "image/png",
            TipoDocumentoVerificacion.Ine);

        Assert.Equal(EstadoAnalisisDocumento.Completado, result.OcrStatus);
        Assert.NotNull(result.DetectedName);
        Assert.Contains("ANA", result.DetectedName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PEREZ", result.DetectedName, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.ElaAvailable || result.MetadataAvailable);
    }

    [Fact]
    public async Task MotorOcrAusente_DebeMarcarNoDisponibleSinInventarNombre()
    {
        var options = new DocumentAnalysisOptions
        {
            TesseractExecutablePath = Path.Combine(
                Path.GetTempPath(),
                $"no-existe-{Guid.NewGuid():N}",
                "tesseract.exe")
        };
        var engine = new TesseractCliOcrEngine(
            Options.Create(options),
            NullLogger<TesseractCliOcrEngine>.Instance);
        var analyzer = new LocalDocumentAnalyzer(
            Options.Create(options),
            engine,
            NullLogger<LocalDocumentAnalyzer>.Instance);

        var result = await analyzer.AnalyzeAsync(
            CreateNameImage(),
            "image/png",
            TipoDocumentoVerificacion.Ine);

        Assert.Equal(EstadoAnalisisDocumento.NoDisponible, result.OcrStatus);
        Assert.Null(result.DetectedName);
        Assert.Contains("Tesseract", result.UnavailableDetail, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] CreateNameImage()
    {
        using var bitmap = new SKBitmap(1000, 500);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var font = new SKFont(SKTypeface.Default, 44);
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true
        };

        canvas.DrawText("NOMBRE: ANA PEREZ LOPEZ", 50, 120, SKTextAlign.Left, font, paint);
        canvas.DrawText("INSTITUTO NACIONAL ELECTORAL", 50, 220, SKTextAlign.Left, font, paint);
        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
