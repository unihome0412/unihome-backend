using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Identity.Sessions;

namespace UniHome.Infrastructure.Persistence.Mongo.Sessions;

/// <summary>
/// Adaptador MongoDB de sesiones. La rotación usa findOneAndUpdate condicionado
/// por el hash vigente para que solo una solicitud consuma el refresh token.
/// Los índices los crea la migración versionada; la API no ejecuta DDL.
/// </summary>
internal sealed class MongoRefreshSessionRepository : IRefreshSessionRepository
{
    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoRefreshSessionRepository> _logger;

    public MongoRefreshSessionRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoRefreshSessionRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task CreateAsync(
        RefreshSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .InsertOneAsync(ToDocument(session), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<RefreshSession?> FindByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<RefreshSessionDocument>.Filter.Eq(
                document => document.Id,
                sessionId.ToString("N", CultureInfo.InvariantCulture));

            var document = await collection
                .Find(filter)
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

    public async Task<bool> TryRotateAsync(
        Guid sessionId,
        string currentTokenHash,
        string newTokenHash,
        DateTimeOffset now,
        DateTimeOffset newExpiresAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentTokenHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(newTokenHash);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<RefreshSessionDocument>.Filter.And(
                Builders<RefreshSessionDocument>.Filter.Eq(
                    document => document.Id,
                    sessionId.ToString("N", CultureInfo.InvariantCulture)),
                Builders<RefreshSessionDocument>.Filter.Eq(
                    document => document.CurrentTokenHash,
                    currentTokenHash),
                Builders<RefreshSessionDocument>.Filter.Eq(
                    document => document.RevokedAtUtc,
                    null),
                Builders<RefreshSessionDocument>.Filter.Gt(
                    document => document.ExpiresAtUtc,
                    now.UtcDateTime));

            var update = Builders<RefreshSessionDocument>.Update
                .Set(document => document.CurrentTokenHash, newTokenHash)
                .Set(document => document.CurrentTokenIssuedAtUtc, now.UtcDateTime)
                .Set(document => document.ExpiresAtUtc, newExpiresAt.UtcDateTime)
                .Set(document => document.LastRotatedAtUtc, now.UtcDateTime)
                .Push(
                    document => document.UsedTokens,
                    new UsedRefreshTokenDocument
                    {
                        TokenHash = currentTokenHash,
                        UsedAtUtc = now.UtcDateTime
                    });

            var result = await collection
                .FindOneAndUpdateAsync(
                    filter,
                    update,
                    new FindOneAndUpdateOptions<RefreshSessionDocument>
                    {
                        ReturnDocument = ReturnDocument.After
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return result is not null;
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<bool> RevokeAsync(
        Guid sessionId,
        Guid accountId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<RefreshSessionDocument>.Filter.And(
                Builders<RefreshSessionDocument>.Filter.Eq(
                    document => document.Id,
                    sessionId.ToString("N", CultureInfo.InvariantCulture)),
                Builders<RefreshSessionDocument>.Filter.Eq(
                    document => document.AccountId,
                    accountId.ToString("N", CultureInfo.InvariantCulture)),
                Builders<RefreshSessionDocument>.Filter.Eq(
                    document => document.RevokedAtUtc,
                    null));

            var update = Builders<RefreshSessionDocument>.Update
                .Set(document => document.RevokedAtUtc, now.UtcDateTime)
                .Set(document => document.RevokedReason, reason);

            var result = await collection
                .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return result.ModifiedCount > 0;
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task RevokeFamilyAsync(
        Guid sessionId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<RefreshSessionDocument>.Filter.And(
                Builders<RefreshSessionDocument>.Filter.Eq(
                    document => document.Id,
                    sessionId.ToString("N", CultureInfo.InvariantCulture)),
                Builders<RefreshSessionDocument>.Filter.Eq(
                    document => document.RevokedAtUtc,
                    null));

            var update = Builders<RefreshSessionDocument>.Update
                .Set(document => document.RevokedAtUtc, now.UtcDateTime)
                .Set(document => document.RevokedReason, reason);

            await collection
                .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task RevokeAllForAccountAsync(
        Guid accountId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<RefreshSessionDocument>.Filter.And(
                Builders<RefreshSessionDocument>.Filter.Eq(
                    document => document.AccountId,
                    accountId.ToString("N", CultureInfo.InvariantCulture)),
                Builders<RefreshSessionDocument>.Filter.Eq(
                    document => document.RevokedAtUtc,
                    null));

            var update = Builders<RefreshSessionDocument>.Update
                .Set(document => document.RevokedAtUtc, now.UtcDateTime)
                .Set(document => document.RevokedReason, reason);

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

    private async Task<IMongoCollection<RefreshSessionDocument>> GetCollectionAsync(CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<RefreshSessionDocument>(RefreshSessionIndexes.CollectionName);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private static RefreshSessionDocument ToDocument(RefreshSession session) =>
        new()
        {
            Id = session.Id.ToString("N", CultureInfo.InvariantCulture),
            AccountId = session.AccountId.ToString("N", CultureInfo.InvariantCulture),
            CurrentTokenHash = session.CurrentTokenHash,
            CurrentTokenIssuedAtUtc = session.CurrentTokenIssuedAt.UtcDateTime,
            ExpiresAtUtc = session.ExpiresAt.UtcDateTime,
            UsedTokens = session.UsedTokens
                .Select(token => new UsedRefreshTokenDocument
                {
                    TokenHash = token.TokenHash,
                    UsedAtUtc = token.UsedAt.UtcDateTime
                })
                .ToList(),
            RevokedAtUtc = session.RevokedAt?.UtcDateTime,
            RevokedReason = session.RevokedReason,
            CreatedAtUtc = session.CreatedAt.UtcDateTime,
            LastRotatedAtUtc = session.LastRotatedAt.UtcDateTime
        };

    private static RefreshSession ToDomain(RefreshSessionDocument document) =>
        new(
            Guid.ParseExact(document.Id, "N"),
            Guid.ParseExact(document.AccountId, "N"),
            document.CurrentTokenHash,
            ToDateTimeOffset(document.CurrentTokenIssuedAtUtc),
            ToDateTimeOffset(document.ExpiresAtUtc),
            document.UsedTokens
                .Select(token => new UsedRefreshToken(
                    token.TokenHash,
                    ToDateTimeOffset(token.UsedAtUtc)))
                .ToList(),
            document.RevokedAtUtc is null
                ? null
                : ToDateTimeOffset(document.RevokedAtUtc.Value),
            document.RevokedReason,
            ToDateTimeOffset(document.CreatedAtUtc),
            ToDateTimeOffset(document.LastRotatedAtUtc));

    private static DateTimeOffset ToDateTimeOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private void LogStorageFailure(MongoException exception)
    {
        // Nunca se registran hashes, tokens ni datos de conexión.
        _logger.LogWarning(
            "La operación de sesiones en MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }

}
