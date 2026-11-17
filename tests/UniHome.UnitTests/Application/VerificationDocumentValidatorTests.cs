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

    [Fact]
    public void FirmasParcialesOMayoresNoDebenConfundirseConFormatosValidos()
    {
        foreach (var content in new[]
                 {
                     new byte[] { 0x89, 0x50, 0x4E },
                     new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A },
                     new byte[] { 0xFF, 0xD8 },
                     new byte[] { 0xFF, 0xD8, 0x00 },
                     new byte[] { 0x00, 0xFF, 0xD8, 0xFF }
                 })
        {
            Assert.Throws<VerificationDocumentValidationException>(() =>
                VerificationDocumentValidator.Validate(
                    TipoDocumentoVerificacion.Ine,
                    new VerificationDocumentUpload("archivo.png", null, content)));
        }
    }

    [Fact]
    public void PdfConFirmaFueraDeLosPrimeros1024Bytes_DebeRechazarse()
    {
        var content = Enumerable.Repeat((byte)' ', 1024)
            .Concat(Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF\n"))
            .ToArray();

        Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Comprobante,
                new VerificationDocumentUpload("predial.pdf", "application/pdf", content)));
    }

    [Fact]
    public void PdfConFirmaEnElLimiteDe1024_DebeAceptarse()
    {
        var content = Enumerable.Repeat((byte)' ', 1019)
            .Concat(Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF\n"))
            .ToArray();

        var result = VerificationDocumentValidator.Validate(
            TipoDocumentoVerificacion.Comprobante,
            new VerificationDocumentUpload("predial.pdf", "application/pdf", content));

        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public void PdfConEofFueraDeLosUltimos4096Bytes_DebeRechazarse()
    {
        var content = Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF\n")
            .Concat(Enumerable.Repeat((byte)' ', 5000))
            .ToArray();

        Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Comprobante,
                new VerificationDocumentUpload("predial.pdf", "application/pdf", content)));
    }

    [Fact]
    public void PdfConEofEnElLimiteDeLosUltimos4096Bytes_DebeAceptarse()
    {
        var prefix = Encoding.ASCII.GetBytes("%PDF-1.4\n");
        var eof = Encoding.ASCII.GetBytes("%%EOF\n");
        var fillerLength = 4096 - eof.Length;
        var content = prefix.Concat(Enumerable.Repeat((byte)' ', fillerLength)).Concat(eof).ToArray();

        var result = VerificationDocumentValidator.Validate(
            TipoDocumentoVerificacion.Comprobante,
            new VerificationDocumentUpload("predial.pdf", "application/pdf", content));

        Assert.Equal("application/pdf", result.ContentType);
    }

    [Theory]
    [InlineData("/JAVASCRIPT")]
    [InlineData("/Js")]
    [InlineData("/LAUNCH")]
    [InlineData("/EmbeddedFile")]
    [InlineData("/embeddedfile")]
    public void PdfConContenidoActivoEnCualquierMayuscula_DebeRechazarse(string marker)
    {
        var pdf = Encoding.ASCII.GetBytes($"%PDF-1.4\n{marker} (x)\n%%EOF\n");

        Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Comprobante,
                new VerificationDocumentUpload("predial.pdf", "application/pdf", pdf)));
    }

    [Fact]
    public void PdfConPalabraParecidaSinLimiteDePalabra_DebeAceptarse()
    {
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n/JavaScriptX (texto)\n%%EOF\n");

        var result = VerificationDocumentValidator.Validate(
            TipoDocumentoVerificacion.Comprobante,
            new VerificationDocumentUpload("predial.pdf", "application/pdf", pdf));

        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public void ExtensionMimeMayusculasYParametros_DebenAceptarse()
    {
        var result = VerificationDocumentValidator.Validate(
            TipoDocumentoVerificacion.Ine,
            new VerificationDocumentUpload("INE.PNG", "IMAGE/PNG; charset=binary", Png));

        Assert.Equal("image/png", result.ContentType);
        Assert.Equal("INE.PNG", result.OriginalFileName);
    }

    [Fact]
    public void ExtensionJpegConContenidoPngOMimeIncoherente_DebeRechazarse()
    {
        Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Ine,
                new VerificationDocumentUpload("ine.jpeg", "image/png", Png)));

        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Ine,
                new VerificationDocumentUpload("ine.png", "image/png", jpeg)));
    }

    [Fact]
    public void NombreSoloEspacios_DebeNormalizarse()
    {
        var result = VerificationDocumentValidator.Validate(
            TipoDocumentoVerificacion.Ine,
            new VerificationDocumentUpload("   ", null, Png));

        Assert.Equal("documento", result.OriginalFileName);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("\\")]
    [InlineData("\"")]
    [InlineData("'")]
    [InlineData("<")]
    [InlineData(">")]
    [InlineData("|")]
    [InlineData(":")]
    [InlineData("*")]
    [InlineData("?")]
    public void NombreConCaracterProhibido_DebeRechazarse(string forbidden)
    {
        Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Ine,
                new VerificationDocumentUpload($"ine{forbidden}.png", null, Png)));
    }

    [Fact]
    public void LimitesDeTamanoYNombre_DebenTenerMensajesYLimitesExactos()
    {
        var empty = Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Ine,
                new VerificationDocumentUpload("ine.png", "image/png", [])));
        Assert.Contains("vacío", empty.Message, StringComparison.OrdinalIgnoreCase);

        var oversized = Png
            .Concat(new byte[VerificationDocumentValidator.MaxBytesPerDocument])
            .ToArray();
        var tooLarge = Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Ine,
                new VerificationDocumentUpload("ine.png", "image/png", oversized)));
        Assert.Contains("10 MB", tooLarge.Message, StringComparison.OrdinalIgnoreCase);

        // El nombre puede ocupar exactamente el máximo; el byte adicional no.
        var maxName = new string('x', DocumentoVerificacion.MaxOriginalFileNameLength - 4) + ".png";
        _ = VerificationDocumentValidator.Validate(
            TipoDocumentoVerificacion.Ine,
            new VerificationDocumentUpload(maxName, "image/png", Png));
        Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Ine,
                new VerificationDocumentUpload(maxName + "x", "image/png", Png)));
    }

    [Fact]
    public void FirmasExactasDeTresYDeOchoBytes_DebenAceptarse()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF };
        var jpegResult = VerificationDocumentValidator.Validate(
            TipoDocumentoVerificacion.Ine,
            new VerificationDocumentUpload("ine.jpg", "image/jpeg", jpeg));
        Assert.Equal("image/jpeg", jpegResult.ContentType);

        var pngSignature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var pngResult = VerificationDocumentValidator.Validate(
            TipoDocumentoVerificacion.Ine,
            new VerificationDocumentUpload("ine.png", "image/png", pngSignature));
        Assert.Equal("image/png", pngResult.ContentType);
    }

    [Fact]
    public void MimeImageJpgConContenidoPng_DebeRechazarseAunqueNoHayaExtension()
    {
        Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Ine,
                new VerificationDocumentUpload(null, "image/jpg", Png)));
    }

    [Fact]
    public void PdfConEofAlInicioYFirmaDentroDeLaVentana_DebeAceptarse()
    {
        var pdf = Encoding.ASCII.GetBytes("%%EOF\n%PDF-1.4\n%%EOF\n");

        var result = VerificationDocumentValidator.Validate(
            TipoDocumentoVerificacion.Comprobante,
            new VerificationDocumentUpload("predial.pdf", "application/pdf", pdf));

        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public void NombreConCaracterProhibidoEnLaPrimeraPosicion_DebeRechazarse()
    {
        Assert.Throws<VerificationDocumentValidationException>(() =>
            VerificationDocumentValidator.Validate(
                TipoDocumentoVerificacion.Ine,
                new VerificationDocumentUpload("/ine.png", "image/png", Png)));
    }

    [Fact]
    public void MimeNuloOVacioConExtensionValida_DebeAceptarse()
    {
        var withNull = VerificationDocumentValidator.Validate(
            TipoDocumentoVerificacion.Ine,
            new VerificationDocumentUpload("ine.png", null, Png));
        var withEmpty = VerificationDocumentValidator.Validate(
            TipoDocumentoVerificacion.Ine,
            new VerificationDocumentUpload("ine.png", "   ", Png));

        Assert.Equal("image/png", withNull.ContentType);
        Assert.Equal("image/png", withEmpty.ContentType);
    }
}
