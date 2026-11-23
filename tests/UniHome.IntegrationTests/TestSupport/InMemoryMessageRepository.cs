using UniHome.Application.Chat.Abstractions;
using UniHome.Domain.Chat;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class InMemoryMessageRepository : IMessageRepository
{
    private readonly object _gate = new();
    private readonly List<Message> _messages = [];

    public Task SaveAsync(Message message, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _messages.Add(message);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Message>> ListPageAsync(
        Guid conversationId,
        MessageCursor? before,
        int limit,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var query = _messages.Where(message => message.ConversationId == conversationId);

            if (before is not null)
            {
                var cursor = before;

                // Mismo desempate (fecha, id como cadena "N") que MongoMessageRepository.
                query = query.Where(message =>
                    message.SentAt < cursor.SentAt ||
                    (message.SentAt == cursor.SentAt &&
                     string.CompareOrdinal(
                         message.Id.ToString("N"), cursor.MessageId.ToString("N")) < 0));
            }

            IReadOnlyList<Message> page = query
                .OrderByDescending(message => message.SentAt)
                .ThenByDescending(message => message.Id.ToString("N"), StringComparer.Ordinal)
                .Take(limit)
                .ToArray();
            return Task.FromResult(page);
        }
    }
}
