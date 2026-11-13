using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Properties.Exceptions;
using UniHome.Domain.Properties;
using UniHome.Infrastructure.Persistence.Mongo.Publications;

namespace UniHome.Infrastructure.Persistence.Mongo.Properties;

[ExcludeFromCodeCoverage]
internal sealed class MongoPropertyRepository : IPropertyRepository
{
    private const int DuplicateKeyCode = 11000;

    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoPropertyRepository> _logger;

    public MongoPropertyRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoPropertyRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task AddAsync(
        Property property,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(property);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .InsertOneAsync(ToDocument(property), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoWriteException exception) when (IsDuplicateKey(exception))
        {
            throw new DuplicatePropertyException();
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<Property?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var document = await collection
                .Find(Builders<PropertyDocument>.Filter.Eq(
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

    public async Task<IReadOnlyList<Property>> FindByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var stringIds = ids
                .Select(id => id.ToString("N", CultureInfo.InvariantCulture))
                .ToArray();
            var documents = await collection
                .Find(Builders<PropertyDocument>.Filter.In(document => document.Id, stringIds))
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

    private async Task<IMongoCollection<PropertyDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<PropertyDocument>(MongoPublicationSchema.PropertiesCollection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private static bool IsDuplicateKey(MongoWriteException exception) =>
        exception.WriteError?.Code == DuplicateKeyCode;

    private static PropertyDocument ToDocument(Property property) =>
        new()
        {
            Id = property.Id.ToString("N", CultureInfo.InvariantCulture),
            OwnerAccountId = property.OwnerAccountId.ToString("N", CultureInfo.InvariantCulture),
            Name = property.Name,
            NameNormalized = property.NameNormalized,
            Address = property.Address,
            AddressNormalized = property.AddressNormalized,
            Colonia = property.Colonia,
            ColoniaNormalized = property.ColoniaNormalized,
            PropertyType = property.PropertyType.ToString(),
            UniversityId = property.UniversityId.ToString("N", CultureInfo.InvariantCulture),
            Location = property.Latitude is null || property.Longitude is null
                ? null
                : new MongoGeoPointDocument
                {
                    Coordinates = [property.Longitude.Value, property.Latitude.Value]
                },
            Floor = property.Floor,
            UnitNumber = property.UnitNumber,
            PublicationState = property.PublicationState.ToString(),
            CreatedAt = property.CreatedAt.UtcDateTime
        };

    private static Property ToDomain(PropertyDocument document)
    {
        var latitude = document.Location?.Coordinates.Count == 2
            ? document.Location.Coordinates[1]
            : (double?)null;
        var longitude = document.Location?.Coordinates.Count == 2
            ? document.Location.Coordinates[0]
            : (double?)null;

        return Property.Restore(
            Guid.ParseExact(document.Id, "N"),
            Guid.ParseExact(document.OwnerAccountId, "N"),
            document.Name,
            document.Address,
            document.Colonia,
            Enum.Parse<PropertyType>(document.PropertyType),
            Guid.ParseExact(document.UniversityId, "N"),
            latitude,
            longitude,
            document.Floor,
            document.UnitNumber,
            Enum.Parse<PublicationState>(document.PublicationState),
            new DateTimeOffset(
                DateTime.SpecifyKind(document.CreatedAt, DateTimeKind.Utc)));
    }

    private void LogStorageFailure(MongoException exception)
    {
        _logger.LogWarning(
            "La operación de propiedades en MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
