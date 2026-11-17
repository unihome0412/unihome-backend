using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using UniHome.Domain.Verification;
using UniHome.Infrastructure.Analysis;
using UniHome.Infrastructure.Configuration;
using UniHome.UnitTests.TestSupport;

namespace UniHome.UnitTests.Infrastructure;

public sealed class RealScannedPdfTests
{
    [RealScannedPdfFact]
    public async Task PdfEscaneado_DebeRasterizarseYExtraerNombreConOcrLocal()
    {
        var tesseract = RealOcrFactAttribute.ResolveExecutable();
        var pdftoppm = RealScannedPdfFactAttribute.ResolvePdftoppm();
        Assert.NotNull(tesseract);
        Assert.NotNull(pdftoppm);

        var options = new DocumentAnalysisOptions
        {
            TesseractExecutablePath = tesseract!,
            PdftoppmExecutablePath = pdftoppm!,
            TesseractLanguage = "spa+eng",
            TesseractTimeoutSeconds = 60,
            PdfRasterTimeoutSeconds = 60
        };
        var engine = new TesseractCliOcrEngine(
            Options.Create(options),
            NullLogger<TesseractCliOcrEngine>.Instance);
        var analyzer = new LocalDocumentAnalyzer(
            Options.Create(options),
            engine,
            new PdfRasterizer(Options.Create(options), NullLogger<PdfRasterizer>.Instance),
            NullLogger<LocalDocumentAnalyzer>.Instance);

        var result = await analyzer.AnalyzeAsync(
            CreateScannedPdf(),
            "application/pdf",
            TipoDocumentoVerificacion.Ine);

        Assert.Equal(EstadoAnalisisDocumento.Completado, result.OcrStatus);
        Assert.NotNull(result.DetectedName);
        Assert.Contains("ANA", result.DetectedName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PEREZ", result.DetectedName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rasterizadas", result.UnavailableDetail, StringComparison.OrdinalIgnoreCase);
    }

    [RealScannedPdfFact]
    public async Task PdfEscaneado_ConPoppler_DebeGenerarMiniaturaJpeg()
    {
        var pdftoppm = RealScannedPdfFactAttribute.ResolvePdftoppm();
        Assert.NotNull(pdftoppm);
        var options = new DocumentAnalysisOptions
        {
            PdftoppmExecutablePath = pdftoppm!
        };
        var generator = new SkiaDocumentPreviewGenerator(
            new PdfRasterizer(Options.Create(options), NullLogger<PdfRasterizer>.Instance));
        var pdf = CreateScannedPdf();

        var preview = await generator.CreateAsync(
            new UniHome.Application.Verification.Abstractions.VerificationDocumentContent(
                pdf,
                "application/pdf",
                pdf.LongLength,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdf))),
            thumbnail: true);

        Assert.True(preview.IsThumbnail);
        Assert.Equal("image/jpeg", preview.ContentType);
        using var codec = SKCodec.Create(new MemoryStream(preview.Content));
        Assert.NotNull(codec);
        Assert.True(codec!.Info.Width <= 640);
    }

    private static byte[] CreateScannedPdf()
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
        using var stream = new MemoryStream();
        using var document = SKDocument.CreatePdf(stream);
        var page = document.BeginPage(612, 792);
        page.DrawImage(
            image,
            50,
            100,
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),
            paint);
        document.EndPage();
        document.Close();
        return stream.ToArray();
    }
}
