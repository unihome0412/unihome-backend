using System.Security.Cryptography;
using System.Text;
using UniHome.Domain.Verification;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Verification;

namespace UniHome.UnitTests.Infrastructure;

public sealed class DocumentCryptographyTests
{
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
    private static readonly Guid ExpedienteId = Guid.NewGuid();

    [Fact]
    public void AesGcm_CifraYDescifraConIntegridad()
    {
        var cipher = new AesGcmDocumentCipher(Key);
        var plaintext = Encoding.UTF8.GetBytes("documento sensible de prueba");
        var hash = Convert.ToHexString(SHA256.HashData(plaintext));

        var encrypted = cipher.Encrypt(
            plaintext,
            ExpedienteId,
            TipoDocumentoVerificacion.Ine,
            1,
            hash);
        var decrypted = cipher.Decrypt(
            encrypted.Ciphertext,
            encrypted.Nonce,
            encrypted.Tag,
            ExpedienteId,
            TipoDocumentoVerificacion.Ine,
            1,
            hash);

        Assert.Equal(plaintext, decrypted);
        Assert.NotEqual(plaintext, encrypted.Ciphertext);
        Assert.Equal(12, encrypted.Nonce.Length);
        Assert.Equal(16, encrypted.Tag.Length);
    }

    [Fact]
    public void AesGcm_DetectaManipulacionDelCiphertext()
    {
        var cipher = new AesGcmDocumentCipher(Key);
        var plaintext = Encoding.UTF8.GetBytes("contenido");
        var hash = Convert.ToHexString(SHA256.HashData(plaintext));
        var encrypted = cipher.Encrypt(
            plaintext,
            ExpedienteId,
            TipoDocumentoVerificacion.Ine,
            1,
            hash);
        encrypted.Ciphertext[0] ^= 0xFF;

        Assert.Throws<CryptographicException>(() => cipher.Decrypt(
            encrypted.Ciphertext,
            encrypted.Nonce,
            encrypted.Tag,
            ExpedienteId,
            TipoDocumentoVerificacion.Ine,
            1,
            hash));
    }

    [Fact]
    public void AesGcm_RechazaMetadatosAsociadosDistintos()
    {
        var cipher = new AesGcmDocumentCipher(Key);
        var plaintext = Encoding.UTF8.GetBytes("contenido");
        var hash = Convert.ToHexString(SHA256.HashData(plaintext));
        var encrypted = cipher.Encrypt(
            plaintext,
            ExpedienteId,
            TipoDocumentoVerificacion.Ine,
            1,
            hash);

        Assert.Throws<CryptographicException>(() => cipher.Decrypt(
            encrypted.Ciphertext,
            encrypted.Nonce,
            encrypted.Tag,
            ExpedienteId,
            TipoDocumentoVerificacion.Comprobante,
            1,
            hash));
    }

    [Fact]
    public void UrlFirmada_RechazaTokensMalFormadosOFirmasInvalidas()
    {
        var options = new DocumentSecurityOptions
        {
            EncryptionKey = Convert.ToBase64String(Key),
            UrlSigningKey = "unihome-unit-test-url-signing-key-0123456789",
            UrlLifetimeSeconds = 300
        };
        var signer = new HmacDocumentUrlSigner(Microsoft.Extensions.Options.Options.Create(options));
        var now = DateTimeOffset.UtcNow;

        Assert.Null(signer.Validate(null!, now));
        Assert.Null(signer.Validate(" ", now));
        Assert.Null(signer.Validate(new string('a', 2049), now));
        Assert.Null(signer.Validate("sin-punto", now));
        Assert.Null(signer.Validate("cabecera.", now));
        Assert.Null(signer.Validate(".firma", now));
        Assert.Null(signer.Validate("a.b.c", now));
        Assert.Null(signer.Validate("%%%.%%%", now));
        Assert.Null(signer.Validate(
            CreateSignedToken("v1|invalido|0|1|0|9999999999|x", options.UrlSigningKey),
            now));
        Assert.Null(signer.Validate(
            CreateSignedToken("v2|00000000000000000000000000000000|0|1|0|9999999999|x", options.UrlSigningKey),
            now));
        Assert.Null(signer.Validate(
            CreateSignedToken("v1|00000000000000000000000000000000|0|1", options.UrlSigningKey),
            now));
        Assert.Null(signer.Validate(
            CreateSignedToken(
                $"v1|{ExpedienteId:N}|99|1|0|9999999999|x",
                options.UrlSigningKey),
            now));
        Assert.Null(signer.Validate(
            CreateSignedToken(
                $"v1|{ExpedienteId:N}|0|1|99|9999999999|x",
                options.UrlSigningKey),
            now));
        Assert.Null(signer.Validate(
            CreateSignedToken(
                $"v1|{ExpedienteId:N}|0|1|0|invalido|x",
                options.UrlSigningKey),
            now));
    }

    [Fact]
    public void FirmaDeUrl_ValidaParametrosDeCreacion()
    {
        var options = new DocumentSecurityOptions
        {
            EncryptionKey = Convert.ToBase64String(Key),
            UrlSigningKey = "unihome-unit-test-url-signing-key-0123456789"
        };
        var signer = new HmacDocumentUrlSigner(Microsoft.Extensions.Options.Options.Create(options));

        Assert.Throws<ArgumentException>(() => signer.Create(
            Guid.Empty,
            TipoDocumentoVerificacion.Ine,
            1,
            UniHome.Application.Verification.Abstractions.DocumentUrlVariant.Full,
            DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => signer.Create(
            ExpedienteId,
            TipoDocumentoVerificacion.Ine,
            0,
            UniHome.Application.Verification.Abstractions.DocumentUrlVariant.Full,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AesGcm_RechazaClaveLongitudONonceInvalidos()
    {
        Assert.Throws<ArgumentException>(() => new AesGcmDocumentCipher(new byte[16]));

        var cipher = new AesGcmDocumentCipher(Key);
        var plaintext = Encoding.UTF8.GetBytes("contenido");
        var hash = Convert.ToHexString(SHA256.HashData(plaintext));
        var encrypted = cipher.Encrypt(
            plaintext,
            ExpedienteId,
            TipoDocumentoVerificacion.Ine,
            1,
            hash);

        Assert.Throws<CryptographicException>(() => cipher.Decrypt(
            encrypted.Ciphertext,
            [],
            encrypted.Tag,
            ExpedienteId,
            TipoDocumentoVerificacion.Ine,
            1,
            hash));
        Assert.Throws<CryptographicException>(() => cipher.Decrypt(
            encrypted.Ciphertext,
            encrypted.Nonce,
            [],
            ExpedienteId,
            TipoDocumentoVerificacion.Ine,
            1,
            hash));
    }

    [Fact]
    public void AesGcm_RotaClavesConservandoLecturaDeLaVersionAnterior()
    {
        var oldKey = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        var newKey = Enumerable.Range(32, 32).Select(value => (byte)value).ToArray();
        var oldCipher = new AesGcmDocumentCipher(oldKey);
        var plaintext = Encoding.UTF8.GetBytes("contenido rotado");
        var hash = Convert.ToHexString(SHA256.HashData(plaintext));
        var encrypted = oldCipher.Encrypt(
            plaintext,
            ExpedienteId,
            TipoDocumentoVerificacion.Ine,
            1,
            hash);

        var rotated = new AesGcmDocumentCipher(
            newKey,
            currentVersion: 2,
            previousKeys: new Dictionary<int, byte[]> { [1] = oldKey });
        var decrypted = rotated.Decrypt(
            encrypted.Ciphertext,
            encrypted.Nonce,
            encrypted.Tag,
            ExpedienteId,
            TipoDocumentoVerificacion.Ine,
            1,
            hash,
            keyVersion: 1);

        Assert.Equal(plaintext, decrypted);
        Assert.Equal(1, encrypted.KeyVersion);
        Assert.Equal(2, rotated.CurrentVersion);
        Assert.Throws<CryptographicException>(() => rotated.Decrypt(
            encrypted.Ciphertext,
            encrypted.Nonce,
            encrypted.Tag,
            ExpedienteId,
            TipoDocumentoVerificacion.Ine,
            1,
            hash,
            keyVersion: 2));
    }

    [Fact]
    public void DocumentSecurityOptions_ValidaRotacionYClavesAnteriores()
    {
        var oldKey = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        var newKey = Enumerable.Range(32, 32).Select(value => (byte)value).ToArray();
        var valid = new DocumentSecurityOptions
        {
            EncryptionKey = Convert.ToBase64String(newKey),
            CurrentKeyVersion = 2,
            PreviousEncryptionKeys = [Convert.ToBase64String(oldKey)],
            UrlSigningKey = "unihome-unit-test-url-signing-key-0123456789"
        };
        var invalid = new DocumentSecurityOptions
        {
            EncryptionKey = Convert.ToBase64String(newKey),
            CurrentKeyVersion = 2,
            PreviousEncryptionKeys = []
        };

        Assert.True(valid.IsValid());
        Assert.Equal(oldKey, valid.GetEncryptionKeyBytes(1));
        Assert.Equal(newKey, valid.GetEncryptionKeyBytes(2));
        Assert.False(invalid.IsValid());
        Assert.Throws<InvalidOperationException>(() => valid.GetEncryptionKeyBytes(3));
    }

    [Fact]
    public void UrlFirmada_DebeValidarYRechazarManipulacion()
    {
        var options = new DocumentSecurityOptions
        {
            EncryptionKey = Convert.ToBase64String(Key),
            UrlSigningKey = "unihome-unit-test-url-signing-key-0123456789",
            UrlLifetimeSeconds = 300
        };
        var signer = new HmacDocumentUrlSigner(Microsoft.Extensions.Options.Options.Create(options));
        var now = DateTimeOffset.UtcNow;

        var grant = signer.Create(
            ExpedienteId,
            TipoDocumentoVerificacion.Ine,
            1,
            UniHome.Application.Verification.Abstractions.DocumentUrlVariant.Full,
            now);
        var parsed = signer.Validate(grant.Token, now.AddSeconds(10));

        Assert.NotNull(parsed);
        Assert.Equal(ExpedienteId, parsed.ExpedienteId);
        Assert.Equal(TipoDocumentoVerificacion.Ine, parsed.DocumentType);
        Assert.Equal(1, parsed.Version);

        var tampered = grant.Token[..^1] + (grant.Token[^1] == 'A' ? 'B' : 'A');
        Assert.Null(signer.Validate(tampered, now));
    }

    [Fact]
    public void UrlFirmada_Expira()
    {
        var options = new DocumentSecurityOptions
        {
            EncryptionKey = Convert.ToBase64String(Key),
            UrlSigningKey = "unihome-unit-test-url-signing-key-0123456789",
            UrlLifetimeSeconds = 60
        };
        var signer = new HmacDocumentUrlSigner(Microsoft.Extensions.Options.Options.Create(options));
        var now = DateTimeOffset.UtcNow;

        var grant = signer.Create(
            ExpedienteId,
            TipoDocumentoVerificacion.Comprobante,
            2,
            UniHome.Application.Verification.Abstractions.DocumentUrlVariant.Thumbnail,
            now);

        Assert.Null(signer.Validate(grant.Token, now.AddSeconds(61)));
    }

    private static string CreateSignedToken(string payload, string key)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), payloadBytes);
        return $"{Base64Url(payloadBytes)}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
