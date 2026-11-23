using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.OccupancyLinks.Abstractions;
using UniHome.Application.OccupancyLinks.Exceptions;
using UniHome.Domain.OccupancyLinks;

namespace UniHome.Infrastructure.Persistence.Mongo.OccupancyLinks;

[ExcludeFromCodeCoverage]
internal sealed class MongoUnitInvitationRepository : IUnitInvitationRepository
{
    private const int DuplicateKeyCode = 11000;

    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoUnitInvitationRepository> _logger;

    public MongoUnitInvitationRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoUnitInvitationRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task<UnitInvitation?> FindLatestByUnitAsync(
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var document = await collection
                .Find(Builders<UnitInvitationDocument>.Filter.Eq(
                    item => item.UnitId,
                    unitId.ToString("N", CultureInfo.InvariantCulture)))
                .Sort(Builders<UnitInvitationDocument>.Sort.Descending(item => item.CreatedAt))
                .Limit(1)
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

    public async Task<UnitInvitation?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var document = await collection
                .Find(Builders<UnitInvitationDocument>.Filter.Eq(item => item.Code, code))
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

    public async Task AddAsync(UnitInvitation invitation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .InsertOneAsync(ToDocument(invitation), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Code == DuplicateKeyCode)
        {
            throw new DuplicateInvitationCodeException();
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    private async Task<IMongoCollection<UnitInvitationDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<UnitInvitationDocument>(MongoOccupancySchema.InvitationsCollection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private static UnitInvitationDocument ToDocument(UnitInvitation invitation) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            UnitId = invitation.UnitId.ToString("N", CultureInfo.InvariantCulture),
            Code = invitation.Code,
            CreatedAt = invitation.CreatedAt.UtcDateTime,
            ExpiresAt = invitation.ExpiresAt.UtcDateTime
        };

    private static UnitInvitation ToDomain(UnitInvitationDocument document) =>
        UnitInvitation.Restore(
            Guid.ParseExact(document.UnitId, "N"),
            document.Code,
            new DateTimeOffset(DateTime.SpecifyKind(document.CreatedAt, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(document.ExpiresAt, DateTimeKind.Utc)));

    private void LogStorageFailure(MongoException exception)
    {
        _logger.LogWarning(
            "La operación de invitaciones en MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
