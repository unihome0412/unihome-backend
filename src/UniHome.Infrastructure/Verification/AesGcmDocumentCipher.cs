using System.Security.Cryptography;
using System.Text;
using UniHome.Domain.Verification;

namespace UniHome.Infrastructure.Verification;

/// <summary>
/// AES-256-GCM para documentos en reposo. El nonce es aleatorio por operación y
/// los metadatos del documento se autentican como associated data, de modo que
/// un ciphertext no puede reasociarse a otro expediente, tipo, versión o hash.
/// Soporta rotación conservando claves anteriores para lectura.
/// </summary>
internal sealed class AesGcmDocumentCipher
{
    public const int NonceBytes = 12;
    public const int TagBytes = 16;

    private readonly byte[] _currentKey;
    private readonly int _currentVersion;
    private readonly IReadOnlyDictionary<int, byte[]> _previousKeys;

    public AesGcmDocumentCipher(byte[] key)
        : this(key, currentVersion: 1, previousKeys: null)
    {
    }

    public AesGcmDocumentCipher(
        byte[] currentKey,
        int currentVersion,
        IReadOnlyDictionary<int, byte[]>? previousKeys)
    {
        ArgumentNullException.ThrowIfNull(currentKey);

        if (currentKey.Length != 32)
        {
            throw new ArgumentException("La clave AES-256 debe tener 32 bytes.", nameof(currentKey));
        }

        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion));
        }

        _currentKey = [.. currentKey];
        _currentVersion = currentVersion;
        _previousKeys = previousKeys is null
            ? new Dictionary<int, byte[]>()
            : new Dictionary<int, byte[]>(previousKeys);
    }

    public int CurrentVersion => _currentVersion;

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

        using var aes = new AesGcm(_currentKey, TagBytes);
        aes.Encrypt(
            nonce,
            plaintext,
            ciphertext,
            tag,
            BuildAssociatedData(expedienteId, type, version, sha256));

        return new EncryptedDocument(ciphertext, nonce, tag, _currentVersion);
    }

    public byte[] Decrypt(
        byte[] ciphertext,
        byte[] nonce,
        byte[] tag,
        Guid expedienteId,
        TipoDocumentoVerificacion type,
        int version,
        string sha256,
        int keyVersion = 1)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(tag);

        if (nonce.Length != NonceBytes || tag.Length != TagBytes)
        {
            throw new CryptographicException("El contenedor cifrado no es válido.");
        }

        var key = GetKey(keyVersion);
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(key, TagBytes);
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

    private byte[] GetKey(int version)
    {
        if (version <= 0)
        {
            version = 1;
        }

        if (version == _currentVersion)
        {
            return _currentKey;
        }

        return _previousKeys.TryGetValue(version, out var key)
            ? key
            : throw new CryptographicException(
                "No existe una clave para descifrar esta versión del documento.");
    }

    private static byte[] BuildAssociatedData(
        Guid expedienteId,
        TipoDocumentoVerificacion type,
        int version,
        string sha256) =>
        Encoding.UTF8.GetBytes(
            $"{expedienteId:N}|{type}|{version}|{sha256.ToUpperInvariant()}");
}

internal sealed record EncryptedDocument(
    byte[] Ciphertext,
    byte[] Nonce,
    byte[] Tag,
    int KeyVersion);
