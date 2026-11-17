using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Verification.Abstractions;
using UniHome.Domain.Verification;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Verification;

namespace UniHome.Infrastructure.Verification;

[ExcludeFromCodeCoverage]
internal sealed class MongoVerificationDocumentStore : IVerificationDocumentStore
{
    private readonly MongoDbClientProvider _clientProvider;
    private readonly AesGcmDocumentCipher _cipher;
    private readonly ILogger<MongoVerificationDocumentStore> _logger;

    public MongoVerificationDocumentStore(
        MongoDbClientProvider clientProvider,
        IOptions<DocumentSecurityOptions> options,
        ILogger<MongoVerificationDocumentStore> logger)
    {
        _clientProvider = clientProvider;
        var security = options.Value;
        var currentKey = security.GetEncryptionKeyBytes(security.CurrentKeyVersion);
        var previousKeys = Enumerable
            .Range(1, Math.Max(0, security.CurrentKeyVersion - 1))
            .ToDictionary(
                version => version,
                version => security.GetEncryptionKeyBytes(version));
        _cipher = new AesGcmDocumentCipher(
            currentKey,
            security.CurrentKeyVersion,
            previousKeys);
        _logger = logger;
    }

    public async Task<StoredVerificationDocument> SaveAsync(
        Guid expedienteId,
        TipoDocumentoVerificacion type,
        int version,
        byte[] content,
        string contentType,
        string originalFileName,
        string sha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        var storageKey = $"doc-{Guid.NewGuid():N}";
        var encrypted = _cipher.Encrypt(content, expedienteId, type, version, sha256);
        var document = new VerificationDocumentBlobDocument
        {
            Id = storageKey,
            ExpedienteId = expedienteId.ToString("N", CultureInfo.InvariantCulture),
            Tipo = type.ToString(),
            Version = version,
            ContentType = contentType,
            OriginalFileName = originalFileName,
            SizeBytes = content.LongLength,
            Sha256 = sha256.ToUpperInvariant(),
            KeyVersion = encrypted.KeyVersion,
            Nonce = encrypted.Nonce,
            Tag = encrypted.Tag,
            Ciphertext = encrypted.Ciphertext,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .InsertOneAsync(document, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new StoredVerificationDocument(
                storageKey,
                contentType,
                content.LongLength,
                sha256.ToUpperInvariant());
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<VerificationDocumentContent?> ReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var document = await collection
                .Find(Builders<VerificationDocumentBlobDocument>.Filter.Eq(
                    item => item.Id,
                    storageKey))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (document is null)
            {
                return null;
            }

            var expedienteId = Guid.ParseExact(document.ExpedienteId, "N");
            var type = Enum.Parse<TipoDocumentoVerificacion>(document.Tipo);
            var plaintext = _cipher.Decrypt(
                document.Ciphertext,
                document.Nonce,
                document.Tag,
                expedienteId,
                type,
                document.Version,
                document.Sha256,
                document.KeyVersion <= 0 ? 1 : document.KeyVersion);

            var actualHash = Convert.ToHexString(SHA256.HashData(plaintext));

            if (!string.Equals(actualHash, document.Sha256, StringComparison.OrdinalIgnoreCase) ||
                plaintext.LongLength != document.SizeBytes)
            {
                throw new CryptographicException("La huella del documento descifrado no coincide.");
            }

            return new VerificationDocumentContent(
                plaintext,
                document.ContentType,
                document.SizeBytes,
                document.Sha256);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
        catch (CryptographicException exception)
        {
            _logger.LogWarning(
                "La integridad de un documento cifrado no es válida. Tipo={ExceptionType} TraceId={TraceId}",
                exception.GetType().FullName,
                Activity.Current?.Id);
            throw new StorageUnavailableException();
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            _logger.LogWarning(
                "El contenedor cifrado contiene metadatos inválidos. Tipo={ExceptionType} TraceId={TraceId}",
                exception.GetType().FullName,
                Activity.Current?.Id);
            throw new StorageUnavailableException();
        }
        catch (FormatException)
        {
            throw new StorageUnavailableException();
        }
    }

    public async Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .DeleteOneAsync(
                    Builders<VerificationDocumentBlobDocument>.Filter.Eq(item => item.Id, storageKey),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    private async Task<IMongoCollection<VerificationDocumentBlobDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<VerificationDocumentBlobDocument>(
                MongoVerificationSchema.DocumentsCollection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private void LogStorageFailure(MongoException exception)
    {
        _logger.LogWarning(
            "La operación de documentos de verificación falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
