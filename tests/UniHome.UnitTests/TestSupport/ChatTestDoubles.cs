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
        DateTimeOffset? before,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = Items.Where(message => message.ConversationId == conversationId);

        if (before is not null)
        {
            query = query.Where(message => message.SentAt < before.Value);
        }

        var page = query
            .OrderByDescending(message => message.SentAt)
            .Take(limit)
            .ToArray();

        return Task.FromResult<IReadOnlyList<Message>>(page);
    }
}
