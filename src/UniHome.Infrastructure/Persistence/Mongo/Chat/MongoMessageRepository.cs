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
internal sealed class MongoMessageRepository : IMessageRepository
{
    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoMessageRepository> _logger;

    public MongoMessageRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoMessageRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task SaveAsync(Message message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .InsertOneAsync(ToDocument(message), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<IReadOnlyList<Message>> ListPageAsync(
        Guid conversationId,
        MessageCursor? before,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(limit, 0);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var filters = new List<FilterDefinition<MessageDocument>>
            {
                Builders<MessageDocument>.Filter.Eq(
                    item => item.ConversationId,
                    conversationId.ToString("N", CultureInfo.InvariantCulture))
            };

            if (before is not null)
            {
                // Cursor compuesto (fecha, id): dos mensajes con la misma fecha no se
                // saltan en el corte de página, porque el desempate por id completa
                // un orden total, a diferencia de comparar solo por fecha.
                var beforeDate = before.SentAt.UtcDateTime;
                var beforeId = before.MessageId.ToString("N", CultureInfo.InvariantCulture);
                filters.Add(Builders<MessageDocument>.Filter.Or(
                    Builders<MessageDocument>.Filter.Lt(item => item.SentAt, beforeDate),
                    Builders<MessageDocument>.Filter.And(
                        Builders<MessageDocument>.Filter.Eq(item => item.SentAt, beforeDate),
                        Builders<MessageDocument>.Filter.Lt(item => item.Id, beforeId))));
            }

            var documents = await collection
                .Find(Builders<MessageDocument>.Filter.And(filters))
                .Sort(Builders<MessageDocument>.Sort
                    .Descending(item => item.SentAt)
                    .Descending(item => item.Id))
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

    private async Task<IMongoCollection<MessageDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<MessageDocument>(MongoChatSchema.MessagesCollection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private static MessageDocument ToDocument(Message message) =>
        new()
        {
            Id = message.Id.ToString("N", CultureInfo.InvariantCulture),
            ConversationId = message.ConversationId.ToString("N", CultureInfo.InvariantCulture),
            SenderAccountId = message.SenderAccountId.ToString("N", CultureInfo.InvariantCulture),
            Body = message.Body,
            SentAt = message.SentAt.UtcDateTime
        };

    private static Message ToDomain(MessageDocument document) =>
        Message.Restore(
            Guid.ParseExact(document.Id, "N"),
            Guid.ParseExact(document.ConversationId, "N"),
            Guid.ParseExact(document.SenderAccountId, "N"),
            document.Body,
            new DateTimeOffset(DateTime.SpecifyKind(document.SentAt, DateTimeKind.Utc)));

    private void LogStorageFailure(MongoException exception)
    {
        _logger.LogWarning(
            "La operación de mensajes en MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
