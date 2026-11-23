using UniHome.Application.Chat.Abstractions;
using UniHome.Domain.Chat;

namespace UniHome.UnitTests.TestSupport;

internal sealed class FakeConversationRepository : IConversationRepository
{
    public List<Conversation> Items { get; } = [];

    public Task<Conversation?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.SingleOrDefault(conversation => conversation.Id == id));

    public Task SaveAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        Items.RemoveAll(item => item.Id == conversation.Id);
        Items.Add(conversation);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Conversation>> ListByParticipantAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Conversation>>(Items
            .Where(conversation => conversation.HasParticipant(accountId))
            .OrderByDescending(conversation => conversation.LastMessageAt ?? conversation.CreatedAt)
            .ToArray());
}

internal sealed class FakeMessageRepository : IMessageRepository
{
    public List<Message> Items { get; } = [];

    public Task SaveAsync(Message message, CancellationToken cancellationToken = default)
    {
        Items.Add(message);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Message>> ListPageAsync(
        Guid conversationId,
        MessageCursor? before,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = Items.Where(message => message.ConversationId == conversationId);

        if (before is not null)
        {
            var cursor = before;

            // Mismo desempate (fecha, id como cadena "N") que MongoMessageRepository,
            // para que la prueba en memoria detecte el mismo bug de corte de página.
            query = query.Where(message =>
                message.SentAt < cursor.SentAt ||
                (message.SentAt == cursor.SentAt &&
                 string.CompareOrdinal(
                     message.Id.ToString("N"), cursor.MessageId.ToString("N")) < 0));
        }

        var page = query
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id.ToString("N"), StringComparer.Ordinal)
            .Take(limit)
            .ToArray();

        return Task.FromResult<IReadOnlyList<Message>>(page);
    }
}
