using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UniHome.Application.Chat.Abstractions;
using UniHome.Application.Identity.Exceptions;
using UniHome.Domain.Chat;

namespace UniHome.Infrastructure.Persistence.Mongo.Chat;

[ExcludeFromCodeCoverage]
internal sealed class MongoConversationRepository : IConversationRepository
{
    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoConversationRepository> _logger;

    public MongoConversationRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoConversationRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task<Conversation?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var document = await collection
                .Find(Builders<ConversationDocument>.Filter.Eq(
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

    public async Task SaveAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .ReplaceOneAsync(
                    Builders<ConversationDocument>.Filter.Eq(
                        item => item.Id,
                        conversation.Id.ToString("N", CultureInfo.InvariantCulture)),
                    ToDocument(conversation),
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

    public async Task<IReadOnlyList<Conversation>> ListByParticipantAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var documents = await collection
                .Find(Builders<ConversationDocument>.Filter.AnyEq(
                    item => item.Participants,
                    accountId.ToString("N", CultureInfo.InvariantCulture)))
                .Sort(Builders<ConversationDocument>.Sort.Descending(item => item.CreatedAt))
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

    private async Task<IMongoCollection<ConversationDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<ConversationDocument>(MongoChatSchema.ConversationsCollection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private static ConversationDocument ToDocument(Conversation conversation) =>
        new()
        {
            Id = conversation.Id.ToString("N", CultureInfo.InvariantCulture),
            UnitId = conversation.UnitId.ToString("N", CultureInfo.InvariantCulture),
            OwnerAccountId = conversation.OwnerAccountId.ToString("N", CultureInfo.InvariantCulture),
            RequesterAccountId = conversation.RequesterAccountId.ToString("N", CultureInfo.InvariantCulture),
            Participants =
            [
                conversation.OwnerAccountId.ToString("N", CultureInfo.InvariantCulture),
                conversation.RequesterAccountId.ToString("N", CultureInfo.InvariantCulture)
            ],
            CreatedAt = conversation.CreatedAt.UtcDateTime,
            LastMessageAt = conversation.LastMessageAt?.UtcDateTime,
            LastMessagePreview = conversation.LastMessagePreview
        };

    private static Conversation ToDomain(ConversationDocument document) =>
        Conversation.Restore(
            Guid.ParseExact(document.Id, "N"),
            Guid.ParseExact(document.UnitId, "N"),
            Guid.ParseExact(document.OwnerAccountId, "N"),
            Guid.ParseExact(document.RequesterAccountId, "N"),
            new DateTimeOffset(DateTime.SpecifyKind(document.CreatedAt, DateTimeKind.Utc)),
            document.LastMessageAt is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(document.LastMessageAt.Value, DateTimeKind.Utc)),
            document.LastMessagePreview);

    private void LogStorageFailure(MongoException exception)
    {
        _logger.LogWarning(
            "La operación de conversaciones en MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
