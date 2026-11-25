using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Reparaciones.Abstractions;
using UniHome.Domain.Reparaciones;

namespace UniHome.Infrastructure.Persistence.Mongo.Reparaciones;

[ExcludeFromCodeCoverage]
internal sealed class MongoReparacionRepository : IReparacionRepository
{
    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoReparacionRepository> _logger;

    public MongoReparacionRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoReparacionRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task AddAsync(Reparacion reparacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reparacion);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .InsertOneAsync(ToDocument(reparacion), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<Reparacion?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var document = await collection
                .Find(Builders<ReparacionDocument>.Filter.Eq(
                    item => item.Id,
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

    public async Task<IReadOnlyList<Reparacion>> ListByUnitAsync(
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var documents = await collection
                .Find(Builders<ReparacionDocument>.Filter.Eq(
                    item => item.UnitId,
                    unitId.ToString("N", CultureInfo.InvariantCulture)))
                .Sort(Builders<ReparacionDocument>.Sort.Descending(item => item.CreatedAt))
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

    public async Task SaveAsync(Reparacion reparacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reparacion);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .ReplaceOneAsync(
                    Builders<ReparacionDocument>.Filter.Eq(
                        item => item.Id,
                        reparacion.Id.ToString("N", CultureInfo.InvariantCulture)),
                    ToDocument(reparacion),
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

    private async Task<IMongoCollection<ReparacionDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<ReparacionDocument>(MongoReparacionesSchema.ReparacionesCollection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private static ReparacionDocument ToDocument(Reparacion reparacion) =>
        new()
        {
            Id = reparacion.Id.ToString("N", CultureInfo.InvariantCulture),
            UnitId = reparacion.UnitId.ToString("N", CultureInfo.InvariantCulture),
            ReportedByAccountId = reparacion.ReportedByAccountId.ToString("N", CultureInfo.InvariantCulture),
            Title = reparacion.Title,
            Description = reparacion.Description,
            Status = reparacion.Status.ToString(),
            Photo = reparacion.Photo is null
                ? null
                : new ReparacionFotoDocument
                {
                    StorageKey = reparacion.Photo.StorageKey,
                    ContentType = reparacion.Photo.ContentType,
                    SizeBytes = reparacion.Photo.SizeBytes,
                    Sha256 = reparacion.Photo.Sha256
                },
            CreatedAt = reparacion.CreatedAt.UtcDateTime,
            UpdatedAt = reparacion.UpdatedAt.UtcDateTime
        };

    private static Reparacion ToDomain(ReparacionDocument document) =>
        Reparacion.Restore(
            Guid.ParseExact(document.Id, "N"),
            Guid.ParseExact(document.UnitId, "N"),
            Guid.ParseExact(document.ReportedByAccountId, "N"),
            document.Title,
            document.Description,
            Enum.Parse<ReparacionEstado>(document.Status),
            document.Photo is null
                ? null
                : new ReparacionFoto(
                    document.Photo.StorageKey,
                    document.Photo.ContentType,
                    document.Photo.SizeBytes,
                    document.Photo.Sha256),
            new DateTimeOffset(DateTime.SpecifyKind(document.CreatedAt, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(document.UpdatedAt, DateTimeKind.Utc)));

    private void LogStorageFailure(MongoException exception)
    {
        _logger.LogWarning(
            "La operación de reparaciones en MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
