using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Units.Abstractions;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.Infrastructure.Persistence.Mongo.Publications;

namespace UniHome.Infrastructure.Persistence.Mongo.Units;

[ExcludeFromCodeCoverage]
internal sealed class MongoUnitRepository : IUnitRepository
{
    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoUnitRepository> _logger;

    public MongoUnitRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoUnitRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task AddAsync(Unit unit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unit);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .InsertOneAsync(ToDocument(unit), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<Unit?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var document = await collection
                .Find(ByIdFilter(id))
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

    public async Task<Unit?> FindByPhotoStorageKeyAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var document = await collection
                .Find(Builders<UnitDocument>.Filter.ElemMatch(
                    unit => unit.Photos,
                    photo => photo.StorageKey == storageKey))
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

    public async Task<bool> UpdateServicesAsync(
        Guid id,
        Guid propertyId,
        IReadOnlyList<ServiceCharge> services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<UnitDocument>.Filter.And(
                ByIdFilter(id),
                Builders<UnitDocument>.Filter.Eq(
                    document => document.PropertyId,
                    propertyId.ToString("N", CultureInfo.InvariantCulture)));
            var update = Builders<UnitDocument>.Update.Set(
                document => document.Services,
                services.Select(ToDocument).ToList());

            var result = await collection
                .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return result.MatchedCount == 1;
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<bool> UpdateZoneRulesAsync(
        Guid id,
        Guid propertyId,
        string? zoneInfo,
        string? rules,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<UnitDocument>.Filter.And(
                ByIdFilter(id),
                Builders<UnitDocument>.Filter.Eq(
                    document => document.PropertyId,
                    propertyId.ToString("N", CultureInfo.InvariantCulture)));
            var update = Builders<UnitDocument>.Update
                .Set(document => document.ZoneInfo, zoneInfo)
                .Set(document => document.Rules, rules);

            var result = await collection
                .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return result.MatchedCount == 1;
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<bool> TryAppendPhotosAsync(
        Guid id,
        Guid propertyId,
        int expectedPhotoCount,
        IReadOnlyList<UnitPhoto> photos,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photos);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedPhotoCount);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<UnitDocument>.Filter.And(
                ByIdFilter(id),
                Builders<UnitDocument>.Filter.Eq(
                    document => document.PropertyId,
                    propertyId.ToString("N", CultureInfo.InvariantCulture)),
                Builders<UnitDocument>.Filter.Size(
                    document => document.Photos,
                    expectedPhotoCount));
            var update = Builders<UnitDocument>.Update.PushEach(
                document => document.Photos,
                photos.Select(ToDocument));

            var result = await collection
                .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return result.MatchedCount == 1;
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    private async Task<IMongoCollection<UnitDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<UnitDocument>(MongoPublicationSchema.UnitsCollection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private static FilterDefinition<UnitDocument> ByIdFilter(Guid id) =>
        Builders<UnitDocument>.Filter.Eq(
            document => document.Id,
            id.ToString("N", CultureInfo.InvariantCulture));

    private static UnitDocument ToDocument(Unit unit) =>
        new()
        {
            Id = unit.Id.ToString("N", CultureInfo.InvariantCulture),
            PropertyId = unit.PropertyId.ToString("N", CultureInfo.InvariantCulture),
            OwnerAccountId = unit.OwnerAccountId.ToString("N", CultureInfo.InvariantCulture),
            UniversityId = unit.UniversityId.ToString("N", CultureInfo.InvariantCulture),
            UnitType = unit.Type.ToString(),
            Occupancy = unit.Occupancy.ToString(),
            Places = unit.Places,
            PricePerPlace = unit.PricePerPlace,
            SizeM2 = unit.SizeM2,
            ForWhom = unit.ForWhom.ToString(),
            Bathroom = unit.Bathroom.ToString(),
            Furniture = unit.Furniture.ToString(),
            FurnitureItems = [.. unit.FurnitureItems],
            Template = unit.Template?.ToString(),
            ZoneInfo = unit.ZoneInfo,
            Rules = unit.Rules,
            Services = unit.Services.Select(ToDocument).ToList(),
            Photos = unit.Photos.Select(ToDocument).ToList(),
            PublicationState = unit.PublicationState.ToString(),
            CreatedAt = unit.CreatedAt.UtcDateTime
        };

    private static ServiceChargeDocument ToDocument(ServiceCharge service) =>
        new()
        {
            Name = service.Name.ToString(),
            Mode = service.Mode.ToString(),
            Amount = service.Amount
        };

    private static UnitPhotoDocument ToDocument(UnitPhoto photo) =>
        new()
        {
            StorageKey = photo.StorageKey,
            ContentType = photo.ContentType,
            SizeBytes = photo.SizeBytes,
            Sha256 = photo.Sha256,
            Order = photo.Order,
            IsPrimary = photo.IsPrimary,
            CreatedAt = photo.CreatedAt.UtcDateTime
        };

    private static Unit ToDomain(UnitDocument document) =>
        Unit.Restore(
            Guid.ParseExact(document.Id, "N"),
            Guid.ParseExact(document.PropertyId, "N"),
            Guid.ParseExact(document.OwnerAccountId, "N"),
            Guid.ParseExact(document.UniversityId, "N"),
            Enum.Parse<UnitType>(document.UnitType),
            Enum.Parse<Occupancy>(document.Occupancy),
            document.Places,
            document.PricePerPlace,
            document.SizeM2,
            Enum.Parse<ForWhom>(document.ForWhom),
            Enum.Parse<BathroomType>(document.Bathroom),
            Enum.Parse<FurnitureType>(document.Furniture),
            document.FurnitureItems,
            string.IsNullOrWhiteSpace(document.Template)
                ? null
                : Enum.Parse<UnitTemplate>(document.Template),
            document.ZoneInfo,
            document.Rules,
            document.Services.Select(ToDomain),
            document.Photos.Select(ToDomain),
            Enum.Parse<PublicationState>(document.PublicationState),
            new DateTimeOffset(
                DateTime.SpecifyKind(document.CreatedAt, DateTimeKind.Utc)));

    private static ServiceCharge ToDomain(ServiceChargeDocument document) =>
        ServiceCharge.Create(
            Enum.Parse<ServiceName>(document.Name),
            Enum.Parse<ServiceMode>(document.Mode),
            document.Amount);

    private static UnitPhoto ToDomain(UnitPhotoDocument document) =>
        new(
            document.StorageKey,
            document.ContentType,
            document.SizeBytes,
            document.Sha256,
            document.Order,
            document.IsPrimary,
            new DateTimeOffset(
                DateTime.SpecifyKind(document.CreatedAt, DateTimeKind.Utc)));

    private void LogStorageFailure(MongoException exception)
    {
        _logger.LogWarning(
            "La operación de unidades en MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
