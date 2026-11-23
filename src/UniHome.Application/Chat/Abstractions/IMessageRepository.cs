using UniHome.Domain.Chat;

namespace UniHome.Application.Chat.Abstractions;

/// <summary>
/// Posición exacta de un mensaje para paginar hacia atrás. Solo la fecha no
/// alcanza: si dos mensajes comparten <c>fecha</c> (misma marca de tiempo),
/// un corte de página que compare solo por fecha puede saltarse alguno de
/// ellos. <c>MessageId</c> desempata con un orden estable.
/// </summary>
public sealed record MessageCursor(DateTimeOffset SentAt, Guid MessageId);

public interface IMessageRepository
{
    /// <summary>Los mensajes son inmutables: solo se insertan, nunca se reemplazan.</summary>
    Task SaveAsync(Message message, CancellationToken cancellationToken = default);

    /// <summary>
    /// EP-M08-02/EP-M08-04 (HU-068): página de mensajes de una conversación,
    /// más recientes primero. Si <paramref name="before"/> no es nulo, solo
    /// devuelve mensajes estrictamente anteriores a esa posición (fecha, y si
    /// la fecha coincide, de <c>MessageId</c> menor), para no saltarse ni
    /// repetir mensajes con la misma fecha en el corte de página.
    /// </summary>
    Task<IReadOnlyList<Message>> ListPageAsync(
        Guid conversationId,
        MessageCursor? before,
        int limit,
        CancellationToken cancellationToken = default);
}
