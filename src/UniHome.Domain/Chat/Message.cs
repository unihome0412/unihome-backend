namespace UniHome.Domain.Chat;

/// <summary>
/// Mensaje inmutable dentro de una conversación (M08, HU-068). Una vez enviado
/// no se edita ni se borra; el límite de longitud y el límite de frecuencia por
/// cuenta se validan en <c>SendMessageService</c> antes de llegar aquí, pero el
/// dominio también los exige como última línea de defensa.
/// </summary>
public sealed class Message
{
    public const int MaxBodyLength = 1000;

    private Message(
        Guid id,
        Guid conversationId,
        Guid senderAccountId,
        string body,
        DateTimeOffset sentAt)
    {
        Id = id;
        ConversationId = conversationId;
        SenderAccountId = senderAccountId;
        Body = body;
        SentAt = sentAt;
    }

    public Guid Id { get; }
    public Guid ConversationId { get; }
    public Guid SenderAccountId { get; }
    public string Body { get; }
    public DateTimeOffset SentAt { get; }

    public static Message Create(
        Guid conversationId,
        Guid senderAccountId,
        string body,
        DateTimeOffset now)
    {
        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException("La conversación es obligatoria.", nameof(conversationId));
        }

        if (senderAccountId == Guid.Empty)
        {
            throw new ArgumentException("El remitente es obligatorio.", nameof(senderAccountId));
        }

        var trimmed = body?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new ArgumentException("El mensaje no puede estar vacío.", nameof(body));
        }

        if (trimmed.Length > MaxBodyLength)
        {
            throw new ArgumentException(
                $"El mensaje no puede exceder {MaxBodyLength} caracteres.",
                nameof(body));
        }

        return new Message(Guid.NewGuid(), conversationId, senderAccountId, trimmed, now);
    }

    public static Message Restore(
        Guid id,
        Guid conversationId,
        Guid senderAccountId,
        string body,
        DateTimeOffset sentAt) =>
        new(id, conversationId, senderAccountId, body, sentAt);
}
