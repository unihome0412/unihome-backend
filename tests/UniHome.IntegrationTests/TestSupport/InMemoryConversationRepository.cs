using UniHome.Application.Chat.Abstractions;
using UniHome.Domain.Chat;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class InMemoryConversationRepository : IConversationRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Conversation> _conversations = [];

    public Task<Conversation?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _conversations.TryGetValue(id, out var conversation);
            return Task.FromResult(conversation);
        }
    }

    public Task SaveAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _conversations[conversation.Id] = conversation;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Conversation>> ListByParticipantAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<Conversation> items = _conversations.Values
                .Where(conversation => conversation.HasParticipant(accountId))
                .OrderByDescending(conversation => conversation.LastMessageAt ?? conversation.CreatedAt)
                .ToArray();
            return Task.FromResult(items);
        }
    }
}
