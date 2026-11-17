using UniHome.Application.Verification.Abstractions;
using UniHome.Domain.Verification;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class FakeDocumentAnalyzer : IDocumentAnalyzer
{
    public List<(TipoDocumentoVerificacion Type, string ContentType)> Calls { get; } = [];

    public Func<TipoDocumentoVerificacion, DocumentAnalysisResult>? ResultFactory { get; set; }

    public Task<DocumentAnalysisResult> AnalyzeAsync(
        ReadOnlyMemory<byte> content,
        string contentType,
        TipoDocumentoVerificacion documentType,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((documentType, contentType));
        var result = ResultFactory?.Invoke(documentType) ?? new DocumentAnalysisResult(
            documentType == TipoDocumentoVerificacion.Ine
                ? "Ana Pérez López"
                : "Ana Pérez López",
            EstadoAnalisisDocumento.Completado,
            EstadoAnalisisDocumento.Completado,
            EditionDetected: false,
            EditionSignals: [],
            ElaAvailable: true,
            MetadataAvailable: true,
            UnavailableDetail: null);

        return Task.FromResult(result);
    }
}
