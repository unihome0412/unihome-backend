using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UniHome.Application.Favorites.Abstractions;
using UniHome.Application.Identity.Exceptions;
using UniHome.Domain.Favorites;

namespace UniHome.Infrastructure.Persistence.Mongo.Exploration;

[ExcludeFromCodeCoverage]
internal sealed class MongoFavoriteRepository : IUnitFavoriteRepository
{
    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoFavoriteRepository> _logger;

    public MongoFavoriteRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoFavoriteRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task AddAsync(
        Guid accountId,
        Guid unitId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<FavoriteDocument>.Filter.And(
                Builders<FavoriteDocument>.Filter.Eq(
                    document => document.AccountId,
                    accountId.ToString("N", CultureInfo.InvariantCulture)),
                Builders<FavoriteDocument>.Filter.Eq(
                    document => document.UnitId,
                    unitId.ToString("N", CultureInfo.InvariantCulture)));
            var update = Builders<FavoriteDocument>.Update
                .SetOnInsert(
                    document => document.Id,
                    $"{accountId:N}:{unitId:N}")
                .SetOnInsert(
                    document => document.AccountId,
                    accountId.ToString("N", CultureInfo.InvariantCulture))
                .SetOnInsert(
                    document => document.UnitId,
                    unitId.ToString("N", CultureInfo.InvariantCulture))
                .SetOnInsert(document => document.CreatedAt, now.UtcDateTime);

            await collection
                .UpdateOneAsync(
                    filter,
                    update,
                    new UpdateOptions { IsUpsert = true },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task RemoveAsync(
        Guid accountId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .DeleteOneAsync(
                    Builders<FavoriteDocument>.Filter.And(
                        Builders<FavoriteDocument>.Filter.Eq(
                            document => document.AccountId,
                            accountId.ToString("N", CultureInfo.InvariantCulture)),
                        Builders<FavoriteDocument>.Filter.Eq(
                            document => document.UnitId,
                            unitId.ToString("N", CultureInfo.InvariantCulture))),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<bool> ExistsAsync(
        Guid accountId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var count = await collection.CountDocumentsAsync(
                Builders<FavoriteDocument>.Filter.And(
                    Builders<FavoriteDocument>.Filter.Eq(
                        document => document.AccountId,
                        accountId.ToString("N", CultureInfo.InvariantCulture)),
                    Builders<FavoriteDocument>.Filter.Eq(
                        document => document.UnitId,
                        unitId.ToString("N", CultureInfo.InvariantCulture))),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return count > 0;
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<IReadOnlySet<Guid>> GetFavoriteUnitIdsAsync(
        Guid accountId,
        IReadOnlyCollection<Guid> unitIds,
        CancellationToken cancellationToken = default)
    {
        if (unitIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var stringIds = unitIds
                .Select(id => id.ToString("N", CultureInfo.InvariantCulture))
                .ToArray();
            var documents = await collection
                .Find(Builders<FavoriteDocument>.Filter.And(
                    Builders<FavoriteDocument>.Filter.Eq(
                        document => document.AccountId,
                        accountId.ToString("N", CultureInfo.InvariantCulture)),
                    Builders<FavoriteDocument>.Filter.In(
                        document => document.UnitId,
                        stringIds)))
                .Project(document => document.UnitId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return documents
                .Select(id => Guid.ParseExact(id, "N"))
                .ToHashSet();
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<FavoritePage> ListAsync(
        Guid accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<FavoriteDocument>.Filter.Eq(
                document => document.AccountId,
                accountId.ToString("N", CultureInfo.InvariantCulture));
            var total = await collection
                .CountDocumentsAsync(filter, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var documents = await collection
                .Find(filter)
                .Sort(Builders<FavoriteDocument>.Sort
                    .Descending(document => document.CreatedAt))
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var items = documents
                .Select(document => new Favorite(
                    Guid.ParseExact(document.AccountId, "N"),
                    Guid.ParseExact(document.UnitId, "N"),
                    new DateTimeOffset(
                        DateTime.SpecifyKind(document.CreatedAt, DateTimeKind.Utc))))
                .ToArray();

            return new FavoritePage(items, total);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    private async Task<IMongoCollection<FavoriteDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<FavoriteDocument>(MongoExplorationSchema.FavoritesCollection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private void LogStorageFailure(MongoException exception)
    {
        _logger.LogWarning(
            "La operación de favoritos en MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
