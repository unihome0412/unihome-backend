using UniHome.Infrastructure.Configuration;

namespace UniHome.UnitTests.Infrastructure;

public sealed class VerificationOptionsTests
{
    private static readonly string ValidKey = Convert.ToBase64String(Enumerable.Range(0, 32).Select(x => (byte)x).ToArray());
    private static readonly string ValidSigningKey = "unihome-unit-test-url-signing-key-0123456789";

    [Fact]
    public void DocumentSecurity_ValidaClavesUrlsYDuracion()
    {
        Assert.True(new DocumentSecurityOptions().IsValid());

        var valid = new DocumentSecurityOptions
        {
            EncryptionKey = ValidKey,
            UrlSigningKey = ValidSigningKey,
            PublicBaseUrl = "https://api.unihome.test",
            UrlLifetimeSeconds = 900
        };
        Assert.True(valid.IsValid());
        Assert.True(valid.HasValidEncryptionKey());
        Assert.Equal(32, valid.GetEncryptionKeyBytes().Length);
        Assert.NotEmpty(valid.GetUrlSigningKeyBytes(valid.GetEncryptionKeyBytes()));

        Assert.False(new DocumentSecurityOptions { UrlLifetimeSeconds = 0 }.IsValid());
        Assert.False(new DocumentSecurityOptions { UrlLifetimeSeconds = 901 }.IsValid());
        Assert.False(new DocumentSecurityOptions { UrlSigningKey = "corta" }.IsValid());
        Assert.False(new DocumentSecurityOptions { PublicBaseUrl = "http://api.unihome.test" }.IsValid());
        Assert.False(new DocumentSecurityOptions { PublicBaseUrl = "no-es-url" }.IsValid());
    }

    [Fact]
    public void DocumentSecurity_DetectaClavesInvalidasYDerivaFirma()
    {
        var invalidBase64 = new DocumentSecurityOptions { EncryptionKey = "no-base64" };
        var wrongLength = new DocumentSecurityOptions
        {
            EncryptionKey = Convert.ToBase64String(new byte[16])
        };
        var valid = new DocumentSecurityOptions { EncryptionKey = ValidKey };

        Assert.Throws<InvalidOperationException>(() => invalidBase64.GetEncryptionKeyBytes());
        Assert.Throws<InvalidOperationException>(() => wrongLength.GetEncryptionKeyBytes());
        Assert.False(invalidBase64.HasValidEncryptionKey());
        Assert.False(wrongLength.HasValidEncryptionKey());
        Assert.Equal(32, Convert.FromBase64String(DocumentSecurityOptions.CreateEphemeralKey()).Length);
        Assert.NotEqual(
            valid.GetUrlSigningKeyBytes(valid.GetEncryptionKeyBytes()),
            new DocumentSecurityOptions
            {
                UrlSigningKey = ValidSigningKey
            }.GetUrlSigningKeyBytes(valid.GetEncryptionKeyBytes()));
    }

    [Fact]
    public void DocumentAnalysis_ValidaRangosYBinarios()
    {
        Assert.True(new DocumentAnalysisOptions().IsValid());

        Assert.False(new DocumentAnalysisOptions { MaxImagePixels = 0 }.IsValid());
        Assert.False(new DocumentAnalysisOptions { MaxImagePixels = DocumentAnalysisOptions.MaxAllowedPixels + 1 }.IsValid());
        Assert.False(new DocumentAnalysisOptions { ElaJpegQuality = 49 }.IsValid());
        Assert.False(new DocumentAnalysisOptions { ElaJpegQuality = 101 }.IsValid());
        Assert.False(new DocumentAnalysisOptions { ElaMeanDifferenceThreshold = -1 }.IsValid());
        Assert.False(new DocumentAnalysisOptions { ElaMeanDifferenceThreshold = 256 }.IsValid());
        Assert.False(new DocumentAnalysisOptions { ElaHighDifferenceRatioThreshold = -0.1 }.IsValid());
        Assert.False(new DocumentAnalysisOptions { ElaHighDifferenceRatioThreshold = 1.1 }.IsValid());
        Assert.False(new DocumentAnalysisOptions { ElaHighDifferenceLevel = 0 }.IsValid());
        Assert.False(new DocumentAnalysisOptions { ElaHighDifferenceLevel = 256 }.IsValid());
        Assert.False(new DocumentAnalysisOptions { TesseractTimeoutSeconds = 4 }.IsValid());
        Assert.False(new DocumentAnalysisOptions { TesseractTimeoutSeconds = 121 }.IsValid());
        Assert.False(new DocumentAnalysisOptions { TesseractPageSegmentationMode = -1 }.IsValid());
        Assert.False(new DocumentAnalysisOptions { TesseractPageSegmentationMode = 14 }.IsValid());
        Assert.False(new DocumentAnalysisOptions { TesseractExecutablePath = " " }.IsValid());
        Assert.False(new DocumentAnalysisOptions { TesseractLanguage = "" }.IsValid());
    }
}
