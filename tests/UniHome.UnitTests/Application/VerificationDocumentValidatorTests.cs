using System.Text;
using UniHome.Application.Verification.Documents;
using UniHome.Domain.Verification;

namespace UniHome.UnitTests.Application;

public sealed class VerificationDocumentValidatorTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFElEQVR42mP4z8DAAMb//0MZUA4Aep8J9x7TI9wAAAAASUVORK5CYII=");

    [Fact]
    public void PngValido_DebeAceptarse()
    {
        var result = VerificationDocumentValidator.Validate(
            TipoDocumentoVerificacion.Ine,
            new VerificationDocumentUpload("ine.png", "image/png", Png));

        Assert.Equal("image/png", result.ContentType);
        Assert.Equal("ine.png", result.OriginalFileName);
        Assert.Equal(64, result.Sha256.Length);
    }

    [Fact]
    public void ExtensionFalsa_DebeRechazarseAunqueElMimeDeclaradoSeaPdf()
    {
        var exception = Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Ine,
                new VerificationDocumentUpload("ine.pdf", "application/pdf", Png)));

        Assert.Contains("extensión", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MimeDeclaradoDistinto_DebeRechazarse()
    {
        Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Comprobante,
                new VerificationDocumentUpload("predial.png", "application/pdf", Png)));
    }

    [Fact]
    public void PdfValido_DebeAceptarse()
    {
        var pdf = Encoding.ASCII.GetBytes(
            "%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n");

        var result = VerificationDocumentValidator.Validate(
            TipoDocumentoVerificacion.Comprobante,
            new VerificationDocumentUpload("predial.pdf", "application/pdf", pdf));

        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public void PdfConJavaScript_DebeRechazarse()
    {
        var pdf = Encoding.ASCII.GetBytes(
            "%PDF-1.4\n1 0 obj << /JavaScript (app.alert(1)) >> endobj\n%%EOF\n");

        var exception = Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Comprobante,
                new VerificationDocumentUpload("predial.pdf", "application/pdf", pdf)));

        Assert.Contains("activo", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NombreConRuta_DebeRechazarse()
    {
        Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Ine,
                new VerificationDocumentUpload("..\\ine.png", "image/png", Png)));
    }

    [Fact]
    public void ContenidoNoPermitido_DebeRechazarse()
    {
        Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Ine,
                new VerificationDocumentUpload(
                    "ine.exe",
                    "application/octet-stream",
                    Encoding.ASCII.GetBytes("MZ ejecutable"))));
    }

    [Fact]
    public void ArchivoMayorAlLimite_DebeRechazarse()
    {
        var oversized = new byte[VerificationDocumentValidator.MaxBytesPerDocument + 1];

        Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Ine,
                new VerificationDocumentUpload("ine.png", "image/png", oversized)));
    }
}
