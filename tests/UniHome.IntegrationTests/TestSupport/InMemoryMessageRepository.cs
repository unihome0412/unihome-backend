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
        DateTimeOffset? before,
        int limit,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var query = _messages.Where(message => message.ConversationId == conversationId);

            if (before is not null)
            {
                query = query.Where(message => message.SentAt < before.Value);
            }

            IReadOnlyList<Message> page = query
                .OrderByDescending(message => message.SentAt)
                .Take(limit)
                .ToArray();
            return Task.FromResult(page);
        }
    }
}
