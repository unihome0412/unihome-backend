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
    public void PdfConJavaScriptFueraDelPrefijoInspeccionado_DebeRechazarse()
    {
        var prefix = Encoding.ASCII.GetBytes("%PDF-1.4\n");
        var filler = Enumerable.Repeat((byte)' ', 2 * 1024 * 1024).ToArray();
        var active = Encoding.ASCII.GetBytes("/JavaScript (app.alert(1))\n%%EOF\n");
        var pdf = prefix.Concat(filler).Concat(active).ToArray();

        Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Comprobante,
                new VerificationDocumentUpload("predial.pdf", "application/pdf", pdf)));
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

    [Fact]
    public void EntradasVaciasYArgumentosNulos_DebenRechazarse()
    {
        Assert.Throws<ArgumentNullException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Ine,
                null!));
        Assert.Throws<ArgumentNullException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Ine,
                new VerificationDocumentUpload("ine.png", "image/png", null!)));
        Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Ine,
                new VerificationDocumentUpload("ine.png", "image/png", [])));
    }

    [Fact]
    public void JpegConExtensionYMimeAlternativos_DebeAceptarse()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var result = VerificationDocumentValidator.Validate(
            TipoDocumentoVerificacion.Ine,
            new VerificationDocumentUpload("ine.jpeg", "image/jpg", jpeg));

        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal("ine.jpeg", result.OriginalFileName);
    }

    [Fact]
    public void PdfSinEofOConContenidoActivo_DebeRechazarse()
    {
        var incomplete = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj << >> endobj\n");
        var launch = Encoding.ASCII.GetBytes("%PDF-1.4\n/Launch /Action\n%%EOF\n");
        var embedded = Encoding.ASCII.GetBytes("%PDF-1.4\n/EmbeddedFile\n%%EOF\n");
        var js = Encoding.ASCII.GetBytes("%PDF-1.4\n/JS (app.alert(1))\n%%EOF\n");

        foreach (var pdf in new[] { incomplete, launch, embedded, js })
        {
            Assert.Throws<VerificationDocumentValidationException>(() =>
                VerificationDocumentValidator.Validate(
                    TipoDocumentoVerificacion.Comprobante,
                    new VerificationDocumentUpload("predial.pdf", "application/pdf", pdf)));
        }
    }

    [Fact]
    public void NombreInvalidoODemasiadoLargo_DebeRechazarse()
    {
        foreach (var name in new[]
                 {
                     "ine\u0001.png",
                     "ine?.png",
                     new string('x', DocumentoVerificacion.MaxOriginalFileNameLength + 1) + ".png"
                 })
        {
            Assert.Throws<VerificationDocumentValidationException>(() =>
                VerificationDocumentValidator.Validate(
                    TipoDocumentoVerificacion.Ine,
                    new VerificationDocumentUpload(name, "image/png", Png)));
        }
    }

    [Fact]
    public void NombreVacioYMimeOctetStream_DebenNormalizarseSinConfiarEnElCliente()
    {
        var result = VerificationDocumentValidator.Validate(
            TipoDocumentoVerificacion.Ine,
            new VerificationDocumentUpload(null, "application/octet-stream; charset=binary", Png));

        Assert.Equal("documento", result.OriginalFileName);
        Assert.Equal("image/png", result.ContentType);
    }
}
