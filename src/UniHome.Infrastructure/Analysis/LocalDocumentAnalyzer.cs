using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniHome.Application.Verification.Abstractions;
using UniHome.Domain.Verification;
using UniHome.Infrastructure.Configuration;

namespace UniHome.Infrastructure.Analysis;

/// <summary>
/// Orquesta OCR local, ELA y metadatos. Si el motor OCR no está instalado, deja
/// el nombre nulo y el estado NoDisponible; si el análisis de edición falla,
/// conserva la parte que sí pudo ejecutarse. Nunca convierte una señal en una
/// decisión de estado.
/// </summary>
internal sealed class LocalDocumentAnalyzer : IDocumentAnalyzer
{
    private readonly DocumentAnalysisOptions _options;
    private readonly TesseractCliOcrEngine _ocr;
    private readonly ILogger<LocalDocumentAnalyzer> _logger;

    public LocalDocumentAnalyzer(
        IOptions<DocumentAnalysisOptions> options,
        TesseractCliOcrEngine ocr,
        ILogger<LocalDocumentAnalyzer> logger)
    {
        _options = options.Value;
        _ocr = ocr;
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
                "application/pdf" => AnalyzePdf(bytes, documentType),
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

    private static DocumentAnalysisResult AnalyzePdf(
        byte[] content,
        TipoDocumentoVerificacion documentType)
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

        var name = inspection.ExtractedText is null
            ? null
            : DocumentNameExtractor.Extract(inspection.ExtractedText, documentType);
        var ocrStatus = inspection.TextLayerAvailable
            ? EstadoAnalisisDocumento.Completado
            : EstadoAnalisisDocumento.NoDisponible;
        var edition = ImageForensics.AnalyzePdfMetadata(
            inspection.Producer,
            inspection.Creator);
        var detail = inspection.TextLayerAvailable
            ? "Nombre extraído de la capa de texto del PDF; no se ejecutó OCR sobre rasterizado."
            : "El PDF no tiene capa de texto y no hay rasterizador OCR local disponible.";

        return new DocumentAnalysisResult(
            name,
            ocrStatus,
            edition.Status,
            edition.Detected,
            edition.Signals,
            edition.ElaAvailable,
            edition.MetadataAvailable,
            $"{detail} {edition.Detail}".Trim());
    }
}
