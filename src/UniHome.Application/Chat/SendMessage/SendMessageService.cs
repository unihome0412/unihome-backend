using UniHome.Application.Abstractions.RateLimiting;
using UniHome.Application.Chat.Abstractions;
using UniHome.Application.Common;
using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Chat;

namespace UniHome.Application.Chat.SendMessage;

/// <summary>
/// EP-M08-02 (HU-068): envía un mensaje dentro de una conversación existente.
/// Solo los participantes pueden enviar; el límite de longitud lo exige
/// <see cref="Message.Create"/> y el de frecuencia este servicio, con un
/// contador distribuido compartido entre réplicas (igual que <c>Auth:RateLimit</c>).
/// </summary>
public sealed class SendMessageService
{
    /// <summary>
    /// Límite provisional por cuenta mientras no se apruebe un valor definitivo,
    /// igual que los umbrales de <c>Auth:RateLimit</c>.
    /// </summary>
    public const int MessagesPerWindow = 30;
    public const int WindowSeconds = 60;
    private const int PreviewMaxLength = 140;
    private const string RateLimitKeyPrefix = "unihome:rl:chat-send-message:";

    private readonly IConversationRepository _conversations;
    private readonly IMessageRepository _messages;
    private readonly IDistributedRateLimiter _rateLimiter;
    private readonly ISystemClock _clock;

    public SendMessageService(
        IConversationRepository conversations,
        IMessageRepository messages,
        IDistributedRateLimiter rateLimiter,
        ISystemClock clock)
    {
        _conversations = conversations;
        _messages = messages;
        _rateLimiter = rateLimiter;
        _clock = clock;
    }

    public async Task<OperationResult<MessageView>> SendAsync(
        Guid senderAccountId,
        Guid conversationId,
        string? texto,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _conversations
            .FindByIdAsync(conversationId, cancellationToken)
            .ConfigureAwait(false);

        if (conversation is null)
        {
            return OperationResult<MessageView>.NotFound();
        }

        if (!conversation.HasParticipant(senderAccountId))
        {
            return OperationResult<MessageView>.Forbidden();
        }

        if (string.IsNullOrWhiteSpace(texto))
        {
            return OperationResult<MessageView>.ValidationFailed("texto", "El mensaje es obligatorio.");
        }

        var trimmed = texto.Trim();

        if (trimmed.Length > Message.MaxBodyLength)
        {
            return OperationResult<MessageView>.ValidationFailed(
                "texto",
                $"El mensaje no puede exceder {Message.MaxBodyLength} caracteres.");
        }

        // Fail-open: si el almacén distribuido no está disponible, se permite el
        // envío en vez de bloquear todo el chat por un límite de frecuencia que
        // no es una garantía de negocio crítica (a diferencia del cupo de HU-049).
        var rateLimit = await _rateLimiter
            .CheckAndIncrementAsync(
                RateLimitKeyPrefix + senderAccountId.ToString("N"),
                MessagesPerWindow,
                TimeSpan.FromSeconds(WindowSeconds),
                cancellationToken)
            .ConfigureAwait(false);

        if (!rateLimit.IsAllowed && !rateLimit.IsServiceUnavailable)
        {
            return OperationResult<MessageView>.RateLimited();
        }

        var now = _clock.UtcNow;
        var message = Message.Create(conversationId, senderAccountId, trimmed, now);
        await _messages.SaveAsync(message, cancellationToken).ConfigureAwait(false);

        var preview = trimmed.Length > PreviewMaxLength
            ? trimmed[..PreviewMaxLength]
            : trimmed;
        conversation.RegisterMessage(preview, now);
        await _conversations.SaveAsync(conversation, cancellationToken).ConfigureAwait(false);

        return OperationResult<MessageView>.Success(new MessageView(
            message.Id,
            message.ConversationId,
            message.SenderAccountId,
            message.Body,
            message.SentAt));
    }
}

public sealed record MessageView(
    Guid MensajeId,
    Guid ConversacionId,
    Guid RemitenteAccountId,
    string Texto,
    DateTimeOffset EnviadoEn);
