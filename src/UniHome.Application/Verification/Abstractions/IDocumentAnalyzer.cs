using UniHome.Domain.Verification;

namespace UniHome.Application.Verification.Abstractions;

/// <summary>
/// Resultado orientativo del análisis local. <see cref="OcrStatus"/> y
/// <see cref="EditionStatus"/> distinguen "sin señales" de "no se pudo analizar";
/// nunca se inventa un nombre ni un veredicto.
/// </summary>
public sealed record DocumentAnalysisResult(
    string? DetectedName,
    EstadoAnalisisDocumento OcrStatus,
    EstadoAnalisisDocumento EditionStatus,
    bool EditionDetected,
    IReadOnlyList<string> EditionSignals,
    bool ElaAvailable,
    bool MetadataAvailable,
    string? UnavailableDetail)
{
    public static DocumentAnalysisResult Unavailable(string detail) =>
        new(
            DetectedName: null,
            EstadoAnalisisDocumento.NoDisponible,
            EstadoAnalisisDocumento.NoDisponible,
            EditionDetected: false,
            EditionSignals: [],
            ElaAvailable: false,
            MetadataAvailable: false,
            UnavailableDetail: detail);
}

public interface IDocumentAnalyzer
{
    Task<DocumentAnalysisResult> AnalyzeAsync(
        ReadOnlyMemory<byte> content,
        string contentType,
        TipoDocumentoVerificacion documentType,
        CancellationToken cancellationToken = default);
}
