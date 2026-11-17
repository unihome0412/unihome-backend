using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UniHome.Domain.Verification;
using UniHome.Infrastructure.Analysis;
using UniHome.Infrastructure.Configuration;

namespace UniHome.UnitTests.Infrastructure;

public sealed class PdfInspectionTests
{
    [Fact]
    public async Task PdfConCapaDeTexto_DebeExtraerNombreSinInventarEla()
    {
        var pdf = CreatePdf("NOMBRE: ANA PEREZ LOPEZ");
        var options = new DocumentAnalysisOptions
        {
            TesseractExecutablePath = Path.Combine(
                Path.GetTempPath(),
                $"sin-tesseract-{Guid.NewGuid():N}",
                "tesseract.exe"),
            PdftoppmExecutablePath = Path.Combine(
                Path.GetTempPath(),
                $"sin-pdftoppm-{Guid.NewGuid():N}",
                "pdftoppm.exe")
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
            pdf,
            "application/pdf",
            TipoDocumentoVerificacion.Comprobante);

        Assert.Equal(EstadoAnalisisDocumento.Completado, result.OcrStatus);
        Assert.Equal("ANA PEREZ LOPEZ", result.DetectedName);
        Assert.False(result.ElaAvailable);
        Assert.True(result.MetadataAvailable);
        Assert.Contains("capa de texto", result.UnavailableDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PdfSinCapaDeTexto_DebeMarcarNoDisponibleSinNombre()
    {
        var pdf = CreatePdf(string.Empty);
        var options = new DocumentAnalysisOptions
        {
            TesseractExecutablePath = Path.Combine(
                Path.GetTempPath(),
                $"sin-tesseract-{Guid.NewGuid():N}",
                "tesseract.exe"),
            PdftoppmExecutablePath = Path.Combine(
                Path.GetTempPath(),
                $"sin-pdftoppm-{Guid.NewGuid():N}",
                "pdftoppm.exe")
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
            pdf,
            "application/pdf",
            TipoDocumentoVerificacion.Ine);

        Assert.Equal(EstadoAnalisisDocumento.NoDisponible, result.OcrStatus);
        Assert.Null(result.DetectedName);
    }

    private static byte[] CreatePdf(string text)
    {
        var content = string.IsNullOrEmpty(text)
            ? string.Empty
            : $"BT /F1 24 Tf 72 700 Td ({Escape(text)}) Tj ET";

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] " +
            "/Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream"
        };

        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };

        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(builder.Length);
            builder.Append(CultureInfo.InvariantCulture, $"{index + 1} 0 obj\n");
            builder.Append(objects[index]);
            builder.Append("\nendobj\n");
        }

        var xrefOffset = builder.Length;
        builder.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n");
        builder.Append("0000000000 65535 f \n");

        foreach (var offset in offsets.Skip(1))
        {
            builder.Append(CultureInfo.InvariantCulture, $"{offset:0000000000} 00000 n \n");
        }

        builder.Append(CultureInfo.InvariantCulture,
            $"trailer << /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
}
