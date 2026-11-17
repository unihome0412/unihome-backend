using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniHome.Application.Verification.Abstractions;
using UniHome.Domain.Verification;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Analysis;

/// <summary>
/// Orquesta OCR local, ELA y metadatos. Si el motor OCR o el rasterizador PDF no
/// están instalados, deja el nombre nulo y el estado NoDisponible; si el análisis
/// de edición falla, conserva la parte que sí pudo ejecutarse. Nunca convierte
/// una señal en una decisión de estado.
/// </summary>
internal sealed class LocalDocumentAnalyzer : IDocumentAnalyzer
{
    private readonly DocumentAnalysisOptions _options;
    private readonly TesseractCliOcrEngine _ocr;
    private readonly PdfRasterizer _pdfRasterizer;
    private readonly ILogger<LocalDocumentAnalyzer> _logger;

    public LocalDocumentAnalyzer(
        IOptions<DocumentAnalysisOptions> options,
        TesseractCliOcrEngine ocr,
        PdfRasterizer pdfRasterizer,
        ILogger<LocalDocumentAnalyzer> logger)
    {
        _options = options.Value;
        _ocr = ocr;
        _pdfRasterizer = pdfRasterizer;
        _logger = logger;
    }

    public async Task<DocumentAnalysisResult> AnalyzeAsync(
        ReadOnlyMemory<byte> content,
        string contentType,
        TipoDocumentoVerificacion documentType,
        CancellationToken cancellationToken = default)
    {
        if (content.IsEmpty)
        {
            return DocumentAnalysisResult.Unavailable("El documento está vacío.");
        }

        var bytes = content.ToArray();

        try
        {
            return contentType switch
            {
                "image/jpeg" or "image/png" => await AnalyzeImageAsync(
                        bytes,
                        contentType,
                        documentType,
                        cancellationToken)
                    .ConfigureAwait(false),
                "application/pdf" => await AnalyzePdfAsync(
                        bytes,
                        documentType,
                        cancellationToken)
                    .ConfigureAwait(false),
                _ => DocumentAnalysisResult.Unavailable(
                    "El tipo de documento no tiene un analizador local disponible.")
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "El análisis documental local falló. Tipo={ExceptionType} TraceId={TraceId}",
                exception.GetType().FullName,
                System.Diagnostics.Activity.Current?.Id);

            return DocumentAnalysisResult.Unavailable(
                "El análisis automático no está disponible; el documento requiere revisión manual.");
        }
    }

    private async Task<DocumentAnalysisResult> AnalyzeImageAsync(
        byte[] content,
        string contentType,
        TipoDocumentoVerificacion documentType,
        CancellationToken cancellationToken)
    {
        var ocr = await _ocr
            .ReadAsync(content, contentType, cancellationToken)
            .ConfigureAwait(false);
        var name = ocr.Available && !ocr.Failed
            ? DocumentNameExtractor.Extract(ocr.Text, documentType)
            : null;
        var ocrStatus = ocr.Available
            ? ocr.Failed
                ? EstadoAnalisisDocumento.Fallido
                : EstadoAnalisisDocumento.Completado
            : EstadoAnalisisDocumento.NoDisponible;
        var edition = ImageForensics.AnalyzeImage(content, _options);
        var details = new List<string>();

        if (!string.IsNullOrWhiteSpace(ocr.Detail))
        {
            details.Add(ocr.Detail);
        }

        if (!string.IsNullOrWhiteSpace(edition.Detail))
        {
            details.Add(edition.Detail);
        }

        return new DocumentAnalysisResult(
            name,
            ocrStatus,
            edition.Status,
            edition.Detected,
            edition.Signals,
            edition.ElaAvailable,
            edition.MetadataAvailable,
            details.Count == 0 ? null : string.Join(' ', details));
    }

    private async Task<DocumentAnalysisResult> AnalyzePdfAsync(
        byte[] content,
        TipoDocumentoVerificacion documentType,
        CancellationToken cancellationToken)
    {
        PdfInspection inspection;

        try
        {
            inspection = PdfInspector.Inspect(content);
        }
        catch (Exception)
        {
            return DocumentAnalysisResult.Unavailable(
                "El PDF no pudo inspeccionarse; requiere revisión manual.");
        }

        var edition = ImageForensics.AnalyzePdfMetadata(
            inspection.Producer,
            inspection.Creator);
        var details = new List<string>();

        if (inspection.TextLayerAvailable)
        {
            return new DocumentAnalysisResult(
                DocumentNameExtractor.Extract(inspection.ExtractedText, documentType),
                EstadoAnalisisDocumento.Completado,
                edition.Status,
                edition.Detected,
                edition.Signals,
                edition.ElaAvailable,
                edition.MetadataAvailable,
                $"Nombre extraído de la capa de texto del PDF. {edition.Detail}".Trim());
        }

        var raster = await _pdfRasterizer
            .RasterizeAsync(content, cancellationToken)
            .ConfigureAwait(false);

        if (!raster.Available)
        {
            details.Add(raster.Detail);
            details.Add(edition.Detail ?? string.Empty);
            return new DocumentAnalysisResult(
                null,
                EstadoAnalisisDocumento.NoDisponible,
                edition.Status,
                edition.Detected,
                edition.Signals,
                edition.ElaAvailable,
                edition.MetadataAvailable,
                string.Join(' ', details.Where(value => !string.IsNullOrWhiteSpace(value))));
        }

        if (raster.Failed)
        {
            details.Add(raster.Detail);
            details.Add(edition.Detail ?? string.Empty);
            return new DocumentAnalysisResult(
                null,
                EstadoAnalisisDocumento.Fallido,
                edition.Status,
                edition.Detected,
                edition.Signals,
                edition.ElaAvailable,
                edition.MetadataAvailable,
                string.Join(' ', details.Where(value => !string.IsNullOrWhiteSpace(value))));
        }

        var ocrText = new List<string>();
        var ocrStatus = EstadoAnalisisDocumento.NoDisponible;

        foreach (var page in raster.Pages)
        {
            var ocr = await _ocr
                .ReadAsync(page, "image/png", cancellationToken)
                .ConfigureAwait(false);

            if (!ocr.Available)
            {
                ocrStatus = EstadoAnalisisDocumento.NoDisponible;
                details.Add(ocr.Detail);
                break;
            }

            if (ocr.Failed)
            {
                ocrStatus = EstadoAnalisisDocumento.Fallido;
                details.Add(ocr.Detail);
                continue;
            }

            ocrStatus = EstadoAnalisisDocumento.Completado;

            if (!string.IsNullOrWhiteSpace(ocr.Text))
            {
                ocrText.Add(ocr.Text);
            }
        }

        details.Add(
            ocrStatus == EstadoAnalisisDocumento.Completado
                ? "PDF sin capa de texto: nombre extraído con OCR sobre páginas rasterizadas."
                : "PDF sin capa de texto: no se pudo completar el OCR rasterizado.");
        details.Add(edition.Detail ?? string.Empty);
        var detectedName = ocrStatus == EstadoAnalisisDocumento.Completado
            ? DocumentNameExtractor.Extract(string.Join('\n', ocrText), documentType)
            : null;

        return new DocumentAnalysisResult(
            detectedName,
            ocrStatus,
            edition.Status,
            edition.Detected,
            edition.Signals,
            edition.ElaAvailable,
            edition.MetadataAvailable,
            string.Join(' ', details.Where(value => !string.IsNullOrWhiteSpace(value))));
    }
}
