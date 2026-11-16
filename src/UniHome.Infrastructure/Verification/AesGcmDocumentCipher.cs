using System.Security.Cryptography;
using System.Text;
using UniHome.Domain.Verification;

namespace UniHome.Infrastructure.Verification;

/// <summary>
/// AES-256-GCM para documentos en reposo. El nonce es aleatorio por operación y
/// los metadatos del documento se autentican como associated data, de modo que
/// un ciphertext no puede reasociarse a otro expediente, tipo, versión o hash.
/// </summary>
internal sealed class AesGcmDocumentCipher
{
    public const int NonceBytes = 12;
    public const int TagBytes = 16;

    private readonly byte[] _key;

    public AesGcmDocumentCipher(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length != 32)
        {
            throw new ArgumentException("La clave AES-256 debe tener 32 bytes.", nameof(key));
        }

        _key = [.. key];
    }

    public EncryptedDocument Encrypt(
        byte[] plaintext,
        Guid expedienteId,
        TipoDocumentoVerificacion type,
        int version,
        string sha256)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagBytes];

        using var aes = new AesGcm(_key, TagBytes);
        aes.Encrypt(
            nonce,
            plaintext,
            ciphertext,
            tag,
            BuildAssociatedData(expedienteId, type, version, sha256));

        return new EncryptedDocument(ciphertext, nonce, tag);
    }

    public byte[] Decrypt(
        byte[] ciphertext,
        byte[] nonce,
        byte[] tag,
        Guid expedienteId,
        TipoDocumentoVerificacion type,
        int version,
        string sha256)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(tag);

        if (nonce.Length != NonceBytes || tag.Length != TagBytes)
        {
            throw new CryptographicException("El contenedor cifrado no es válido.");
        }

        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(_key, TagBytes);
            aes.Decrypt(
                nonce,
                ciphertext,
                tag,
                plaintext,
                BuildAssociatedData(expedienteId, type, version, sha256));
            return plaintext;
        }
        catch (AuthenticationTagMismatchException)
        {
            throw new CryptographicException("La integridad del documento no es válida.");
        }
    }

    private static byte[] BuildAssociatedData(
        Guid expedienteId,
        TipoDocumentoVerificacion type,
        int version,
        string sha256) =>
        Encoding.UTF8.GetBytes(
            $"{expedienteId:N}|{type}|{version}|{sha256.ToUpperInvariant()}");
}

internal sealed record EncryptedDocument(byte[] Ciphertext, byte[] Nonce, byte[] Tag);
