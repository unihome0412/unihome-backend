using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Rentals.Abstractions;
using UniHome.Domain.Rentals;

namespace UniHome.Infrastructure.Persistence.Mongo.Rentals;

[ExcludeFromCodeCoverage]
internal sealed class MongoRentPeriodRepository : IRentPeriodRepository
{
    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoRentPeriodRepository> _logger;

    public MongoRentPeriodRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoRentPeriodRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task<RentPeriod?> FindAsync(
        Guid unitId,
        string periodo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var document = await collection
                .Find(Builders<RentPeriodDocument>.Filter.And(
                    UnitFilter(unitId),
                    Builders<RentPeriodDocument>.Filter.Eq(item => item.Periodo, periodo)))
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

    public async Task<RentPeriod?> FindMostRecentBeforeAsync(
        Guid unitId,
        string periodo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var document = await collection
                .Find(Builders<RentPeriodDocument>.Filter.And(
                    UnitFilter(unitId),
                    Builders<RentPeriodDocument>.Filter.Lt(item => item.Periodo, periodo)))
                .Sort(Builders<RentPeriodDocument>.Sort.Descending(item => item.Periodo))
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

    public async Task<IReadOnlyList<RentPeriod>> ListByUnitAsync(
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var documents = await collection
                .Find(UnitFilter(unitId))
                .Sort(Builders<RentPeriodDocument>.Sort.Descending(item => item.Periodo))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return documents.Select(ToDomain).ToArray();
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task SaveAsync(RentPeriod period, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .ReplaceOneAsync(
                    Builders<RentPeriodDocument>.Filter.Eq(
                        item => item.Id,
                        period.Id.ToString("N", CultureInfo.InvariantCulture)),
                    ToDocument(period),
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

    private static FilterDefinition<RentPeriodDocument> UnitFilter(Guid unitId) =>
        Builders<RentPeriodDocument>.Filter.Eq(
            item => item.UnitId,
            unitId.ToString("N", CultureInfo.InvariantCulture));

    private async Task<IMongoCollection<RentPeriodDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<RentPeriodDocument>(MongoRentalsSchema.RentPeriodsCollection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private static RentPeriodDocument ToDocument(RentPeriod period) =>
        new()
        {
            Id = period.Id.ToString("N", CultureInfo.InvariantCulture),
            UnitId = period.UnitId.ToString("N", CultureInfo.InvariantCulture),
            Periodo = period.Periodo,
            TotalMxn = period.TotalMxn,
            CutoffDay = period.CutoffDay,
            Concepts = period.Concepts
                .Select(concept => new RentConceptDocument
                {
                    Nombre = concept.Nombre.ToString(),
                    EsFijo = concept.EsFijo
                })
                .ToList(),
            SplitMethod = period.SplitMethod?.ToString(),
            Shares = period.Shares
                .Select(share => new TenantShareDocument
                {
                    TenantAccountId = share.TenantAccountId.ToString("N", CultureInfo.InvariantCulture),
                    AmountMxn = share.AmountMxn
                })
                .ToList(),
            CreatedAt = period.CreatedAt.UtcDateTime,
            UpdatedAt = period.UpdatedAt.UtcDateTime
        };

    private static RentPeriod ToDomain(RentPeriodDocument document) =>
        RentPeriod.Restore(
            Guid.ParseExact(document.Id, "N"),
            Guid.ParseExact(document.UnitId, "N"),
            document.Periodo,
            document.TotalMxn,
            document.CutoffDay,
            document.Concepts.Select(concept => new RentConcept(
                Enum.Parse<RentConceptName>(concept.Nombre),
                concept.EsFijo)),
            document.SplitMethod is null ? null : Enum.Parse<RentSplitMethod>(document.SplitMethod),
            document.Shares.Select(share => new TenantShare(
                Guid.ParseExact(share.TenantAccountId, "N"),
                share.AmountMxn)),
            new DateTimeOffset(DateTime.SpecifyKind(document.CreatedAt, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(document.UpdatedAt, DateTimeKind.Utc)));

    private void LogStorageFailure(MongoException exception)
    {
        _logger.LogWarning(
            "La operación de renta en MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
