using UglyToad.PdfPig;
using UniHome.Domain.Verification;

namespace UniHome.Infrastructure.Analysis;

internal sealed record PdfInspection(
    string? ExtractedText,
    string? Producer,
    string? Creator,
    bool TextLayerAvailable);

/// <summary>
/// Inspección de PDF con PdfPig: capa de texto (sin ejecutar contenido activo) y
/// metadatos de creación. Un PDF escaneado sin capa de texto se marca como no
/// disponible para OCR; no se inventa un nombre.
/// </summary>
internal static class PdfInspector
{
    public static PdfInspection Inspect(byte[] content)
    {
        using var document = PdfDocument.Open(content);
        var information = document.Information;
        var text = string.Join(
            '\n',
            document.GetPages()
                .Take(3)
                .Select(page => page.Text)
                .Where(pageText => !string.IsNullOrWhiteSpace(pageText)));

        return new PdfInspection(
            string.IsNullOrWhiteSpace(text) ? null : text,
            information.Producer,
            information.Creator,
            !string.IsNullOrWhiteSpace(text));
    }
}
