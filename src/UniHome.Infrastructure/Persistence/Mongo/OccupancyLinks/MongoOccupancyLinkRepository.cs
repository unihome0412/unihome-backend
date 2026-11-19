using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Domain.OccupancyLinks;

namespace UniHome.Infrastructure.Persistence.Mongo.OccupancyLinks;

[ExcludeFromCodeCoverage]
internal sealed class MongoOccupancyLinkRepository : IOccupancyLinkRepository
{
    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoOccupancyLinkRepository> _logger;

    public MongoOccupancyLinkRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoOccupancyLinkRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task<OccupancyLink?> FindAsync(
        Guid tenantAccountId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var document = await collection
                .Find(Builders<OccupancyLinkDocument>.Filter.Eq(
                    item => item.Id,
                    BuildId(tenantAccountId, unitId)))
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

    public async Task SaveAsync(OccupancyLink link, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .ReplaceOneAsync(
                    Builders<OccupancyLinkDocument>.Filter.Eq(
                        item => item.Id,
                        BuildId(link.TenantAccountId, link.UnitId)),
                    ToDocument(link),
                    new ReplaceOptions { IsUpsert = true },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<int> CountActiveAsync(Guid unitId, CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var count = await collection
                .CountDocumentsAsync(
                    ActiveFilter(Builders<OccupancyLinkDocument>.Filter.Eq(
                        item => item.UnitId,
                        unitId.ToString("N", CultureInfo.InvariantCulture))),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return (int)count;
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<bool> HasActiveLinkAsync(
        Guid tenantAccountId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var count = await collection
                .CountDocumentsAsync(
                    ActiveFilter(Builders<OccupancyLinkDocument>.Filter.Eq(
                        item => item.TenantAccountId,
                        tenantAccountId.ToString("N", CultureInfo.InvariantCulture))),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return count > 0;
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    private static FilterDefinition<OccupancyLinkDocument> ActiveFilter(
        FilterDefinition<OccupancyLinkDocument> scope) =>
        Builders<OccupancyLinkDocument>.Filter.And(
            scope,
            Builders<OccupancyLinkDocument>.Filter.Eq(
                item => item.Status,
                OccupancyStatus.Activo.ToString()));

    private static string BuildId(Guid tenantAccountId, Guid unitId) =>
        $"{tenantAccountId:N}:{unitId:N}";

    private async Task<IMongoCollection<OccupancyLinkDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<OccupancyLinkDocument>(MongoOccupancySchema.LinksCollection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private static OccupancyLinkDocument ToDocument(OccupancyLink link) =>
        new()
        {
            Id = BuildId(link.TenantAccountId, link.UnitId),
            TenantAccountId = link.TenantAccountId.ToString("N", CultureInfo.InvariantCulture),
            UnitId = link.UnitId.ToString("N", CultureInfo.InvariantCulture),
            Status = link.Status.ToString(),
            JoinedAt = link.JoinedAt.UtcDateTime,
            EndedAt = link.EndedAt?.UtcDateTime,
            EndedReason = link.EndedReason?.ToString()
        };

    private static OccupancyLink ToDomain(OccupancyLinkDocument document) =>
        OccupancyLink.Restore(
            Guid.ParseExact(document.TenantAccountId, "N"),
            Guid.ParseExact(document.UnitId, "N"),
            Enum.Parse<OccupancyStatus>(document.Status),
            new DateTimeOffset(DateTime.SpecifyKind(document.JoinedAt, DateTimeKind.Utc)),
            document.EndedAt is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(document.EndedAt.Value, DateTimeKind.Utc)),
            document.EndedReason is null
                ? null
                : Enum.Parse<OccupancyEndReason>(document.EndedReason));

    private void LogStorageFailure(MongoException exception)
    {
        _logger.LogWarning(
            "La operación de ocupación en MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
