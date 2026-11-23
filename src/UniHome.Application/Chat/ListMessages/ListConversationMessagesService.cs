using UniHome.Application.Chat.Abstractions;
using UniHome.Application.Chat.SendMessage;
using UniHome.Application.Common;

namespace UniHome.Application.Chat.ListMessages;

/// <summary>
/// EP-M08-04 (HU-068): historial paginado de una conversación, solo para sus
/// participantes. La página trae los mensajes más recientes primero; para
/// seguir hacia atrás en el historial se vuelve a llamar pasando
/// <c>SiguienteCursor</c> como <c>before</c>.
/// </summary>
public sealed class ListConversationMessagesService
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    private readonly IConversationRepository _conversations;
    private readonly IMessageRepository _messages;

    public ListConversationMessagesService(
        IConversationRepository conversations,
        IMessageRepository messages)
    {
        _conversations = conversations;
        _messages = messages;
    }

    public async Task<OperationResult<MessagePageView>> ListAsync(
        Guid accountId,
        Guid conversationId,
        DateTimeOffset? before,
        int? pageSize,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _conversations
            .FindByIdAsync(conversationId, cancellationToken)
            .ConfigureAwait(false);

        if (conversation is null)
        {
            return OperationResult<MessagePageView>.NotFound();
        }

        if (!conversation.HasParticipant(accountId))
        {
            return OperationResult<MessagePageView>.Forbidden();
        }

        var size = pageSize is null or <= 0
            ? DefaultPageSize
            : Math.Min(pageSize.Value, MaxPageSize);

        var page = await _messages
            .ListPageAsync(conversationId, before, size, cancellationToken)
            .ConfigureAwait(false);

        // Página llena: puede haber más historial anterior al mensaje más
        // antiguo devuelto. Página incompleta: ya se llegó al principio.
        DateTimeOffset? nextCursor = page.Count == size ? page[^1].SentAt : null;

        var views = page
            .Select(message => new MessageView(
                message.Id,
                message.ConversationId,
                message.SenderAccountId,
                message.Body,
                message.SentAt))
            .ToArray();

        return OperationResult<MessagePageView>.Success(new MessagePageView(views, nextCursor));
    }
}

public sealed record MessagePageView(
    IReadOnlyList<MessageView> Mensajes,
    DateTimeOffset? SiguienteCursor);
