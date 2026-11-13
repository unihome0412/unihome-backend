using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Universities.Abstractions;
using UniHome.Application.Universities.Exceptions;
using UniHome.Domain.Universities;

namespace UniHome.Infrastructure.Persistence.Mongo.Universities;

/// <summary>
/// Adaptador MongoDB del catálogo M03. El índice único sobre el nombre
/// normalizado decide los duplicados de forma atómica; la búsqueda solo devuelve
/// universidades activas y el límite se aplica en la consulta. La cobertura real
/// se ejerce en <c>MongoUniversityPersistenceTests</c> (opt-in); se excluye del
/// reporte unitario para no declarar cobertura ficticia de MongoDB.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class MongoUniversityRepository : IUniversityRepository
{
    private const int DuplicateKeyCode = 11000;

    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoUniversityRepository> _logger;

    public MongoUniversityRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoUniversityRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task AddAsync(
        University university,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(university);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .InsertOneAsync(ToDocument(university), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoWriteException exception) when (IsDuplicateKey(exception))
        {
            throw new DuplicateUniversityException();
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<University?> FindByIdAsync(
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

    public async Task<GeoCoordinate?> GetLocationAsync(
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

            if (document?.Location is null ||
                document.Location.Coordinates.Count != 2 ||
                !string.Equals(document.Location.Type, "Point", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var coordinate = new GeoCoordinate(
                document.Location.Coordinates[1],
                document.Location.Coordinates[0]);

            return coordinate.IsValid ? coordinate : null;
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<UniversityPage> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var total = await collection
                .CountDocumentsAsync(
                    Builders<UniversityDocument>.Filter.Empty,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
            var documents = await collection
                .Find(Builders<UniversityDocument>.Filter.Empty)
                .Sort(Builders<UniversityDocument>.Sort.Ascending(
                    document => document.NormalizedName))
                .Skip(skip)
                .Limit(pageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return new UniversityPage(
                documents.Select(ToDomain).ToArray(),
                total);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<IReadOnlyList<University>> SearchActiveAsync(
        string normalizedTerm,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedTerm);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<UniversityDocument>.Filter.And(
                Builders<UniversityDocument>.Filter.Eq(
                    document => document.IsActive,
                    true),
                Builders<UniversityDocument>.Filter.Regex(
                    document => document.NormalizedName,
                    new BsonRegularExpression(Regex.Escape(normalizedTerm))));

            var documents = await collection
                .Find(filter)
                .Sort(Builders<UniversityDocument>.Sort.Ascending(
                    document => document.NormalizedName))
                .Limit(limit)
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

    public async Task<bool> UpdateNameAndCampusAsync(
        University university,
        string expectedNormalizedName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(university);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedNormalizedName);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filter = Builders<UniversityDocument>.Filter.And(
                ByIdFilter(university.Id),
                Builders<UniversityDocument>.Filter.Eq(
                    document => document.NormalizedName,
                    expectedNormalizedName));
            var update = Builders<UniversityDocument>.Update
                .Set(document => document.NameAndCampus, university.NameAndCampus)
                .Set(document => document.NormalizedName, university.NormalizedName);

            var result = await collection
                .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return result.MatchedCount == 1;
        }
        catch (MongoWriteException exception) when (IsDuplicateKey(exception))
        {
            throw new DuplicateUniversityException();
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<bool> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var update = Builders<UniversityDocument>.Update
                .Set(document => document.IsActive, isActive);

            var result = await collection
                .UpdateOneAsync(ByIdFilter(id), update, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return result.MatchedCount == 1;
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    private async Task<IMongoCollection<UniversityDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<UniversityDocument>(MongoUniversitySchema.Collection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private static bool IsDuplicateKey(MongoWriteException exception) =>
        exception.WriteError?.Code == DuplicateKeyCode;

    private static FilterDefinition<UniversityDocument> ByIdFilter(Guid id) =>
        Builders<UniversityDocument>.Filter.Eq(
            document => document.Id,
            id.ToString("N", CultureInfo.InvariantCulture));

    private static UniversityDocument ToDocument(University university) =>
        new()
        {
            Id = university.Id.ToString("N", CultureInfo.InvariantCulture),
            NameAndCampus = university.NameAndCampus,
            NormalizedName = university.NormalizedName,
            IsActive = university.IsActive,
            CreatedAt = university.CreatedAt.UtcDateTime
        };

    private static University ToDomain(UniversityDocument document) =>
        University.Restore(
            Guid.ParseExact(document.Id, "N"),
            document.NameAndCampus,
            document.IsActive,
            new DateTimeOffset(
                DateTime.SpecifyKind(document.CreatedAt, DateTimeKind.Utc)));

    private void LogStorageFailure(MongoException exception)
    {
        // Nunca se registra el nombre, el correo ni la conexión.
        _logger.LogWarning(
            "La operación de universidades en MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
