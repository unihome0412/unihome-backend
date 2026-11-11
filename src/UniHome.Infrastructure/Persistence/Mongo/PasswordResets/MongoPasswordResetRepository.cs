using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Exceptions;
using UniHome.Domain.Accounts;

namespace UniHome.Infrastructure.Persistence.Mongo.PasswordResets;

/// <summary>
/// Adaptador MongoDB de recuperación. El consumo y el incremento de intentos usan
/// filtros condicionados para que una sola operación gane bajo concurrencia. Los
/// índices se comprueban en modo lectura; la API no crea ni modifica el esquema.
/// La cobertura real de este adaptador se ejerce en
/// <c>MongoPasswordResetPersistenceTests</c> (opt-in); se excluye del reporte
/// unitario para no declarar cobertura ficticia de MongoDB.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class MongoPasswordResetRepository : IPasswordResetRepository
{
    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoPasswordResetRepository> _logger;

    public MongoPasswordResetRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoPasswordResetRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task AddAsync(
        PasswordResetToken token,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .InsertOneAsync(ToDocument(token), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<PasswordResetToken?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var document = await collection
                .Find(Builders<PasswordResetDocument>.Filter.Eq(
                    document => document.Id,
                    id.ToString("N", CultureInfo.InvariantCulture)))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return document is null ? null : ToDomain(document);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<bool> TryConsumeAsync(
        Guid id,
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<PasswordResetDocument>.Filter.And(
                Builders<PasswordResetDocument>.Filter.Eq(
                    document => document.Id,
                    id.ToString("N", CultureInfo.InvariantCulture)),
                Builders<PasswordResetDocument>.Filter.Eq(
                    document => document.TokenHash,
                    tokenHash),
                Builders<PasswordResetDocument>.Filter.Eq(
                    document => document.ConsumedAt,
                    null),
                Builders<PasswordResetDocument>.Filter.Gt(
                    document => document.ExpiresAt,
                    now.UtcDateTime),
                Builders<PasswordResetDocument>.Filter.Or(
                    Builders<PasswordResetDocument>.Filter.Exists(
                        document => document.FailedAttempts,
                        false),
                    Builders<PasswordResetDocument>.Filter.Lt(
                        document => document.FailedAttempts,
                        PasswordResetToken.MaxAttempts)));

            var update = Builders<PasswordResetDocument>.Update
                .Set(document => document.ConsumedAt, now.UtcDateTime);

            var result = await collection
                .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return result.ModifiedCount == 1;
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<PasswordResetToken?> RegisterFailedAttemptAsync(
        Guid id,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<PasswordResetDocument>.Filter.And(
                Builders<PasswordResetDocument>.Filter.Eq(
                    document => document.Id,
                    id.ToString("N", CultureInfo.InvariantCulture)),
                Builders<PasswordResetDocument>.Filter.Eq(
                    document => document.ConsumedAt,
                    null),
                Builders<PasswordResetDocument>.Filter.Gt(
                    document => document.ExpiresAt,
                    now.UtcDateTime),
                Builders<PasswordResetDocument>.Filter.Or(
                    Builders<PasswordResetDocument>.Filter.Exists(
                        document => document.FailedAttempts,
                        false),
                    Builders<PasswordResetDocument>.Filter.Lt(
                        document => document.FailedAttempts,
                        PasswordResetToken.MaxAttempts)));

            var update = Builders<PasswordResetDocument>.Update
                .Inc(document => document.FailedAttempts, 1);

            var document = await collection
                .FindOneAndUpdateAsync(
                    filter,
                    update,
                    new FindOneAndUpdateOptions<PasswordResetDocument>
                    {
                        ReturnDocument = ReturnDocument.After
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return document is null ? null : ToDomain(document);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task InvalidateAllForAccountAsync(
        Guid accountId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<PasswordResetDocument>.Filter.And(
                Builders<PasswordResetDocument>.Filter.Eq(
                    document => document.AccountId,
                    accountId.ToString("N", CultureInfo.InvariantCulture)),
                Builders<PasswordResetDocument>.Filter.Eq(
                    document => document.ConsumedAt,
                    null));

            var update = Builders<PasswordResetDocument>.Update
                .Set(document => document.ConsumedAt, now.UtcDateTime);

            await collection
                .UpdateManyAsync(filter, update, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    private async Task<IMongoCollection<PasswordResetDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<PasswordResetDocument>(MongoIdentitySchema.AccountRecoveryCollection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private static PasswordResetDocument ToDocument(PasswordResetToken token) =>
        new()
        {
            Id = token.Id.ToString("N", CultureInfo.InvariantCulture),
            AccountId = token.AccountId.ToString("N", CultureInfo.InvariantCulture),
            TokenHash = token.TokenHash,
            ExpiresAt = token.ExpiresAt.UtcDateTime,
            ConsumedAt = token.ConsumedAt?.UtcDateTime,
            CreatedAt = token.IssuedAt.UtcDateTime,
            FailedAttempts = token.FailedAttempts
        };

    private static PasswordResetToken ToDomain(PasswordResetDocument document) =>
        PasswordResetToken.Restore(
            Guid.ParseExact(document.Id, "N"),
            Guid.ParseExact(document.AccountId, "N"),
            document.TokenHash,
            ToDateTimeOffset(document.CreatedAt),
            ToDateTimeOffset(document.ExpiresAt),
            document.ConsumedAt is null
                ? null
                : ToDateTimeOffset(document.ConsumedAt.Value),
            document.FailedAttempts);

    private static DateTimeOffset ToDateTimeOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private void LogStorageFailure(MongoException exception)
    {
        // Nunca se registra el token, el hash, el correo ni la conexión.
        _logger.LogWarning(
            "La operación de recuperación en MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
