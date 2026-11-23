using UniHome.Domain.Chat;

namespace UniHome.Application.Chat.Abstractions;

public interface IMessageRepository
{
    /// <summary>Los mensajes son inmutables: solo se insertan, nunca se reemplazan.</summary>
    Task SaveAsync(Message message, CancellationToken cancellationToken = default);

    /// <summary>
    /// EP-M08-02/EP-M08-04 (HU-068): página de mensajes de una conversación,
    /// más recientes primero. Si <paramref name="before"/> no es nulo, solo
    /// devuelve mensajes estrictamente anteriores a esa fecha (paginación hacia
    /// atrás en el historial).
    /// </summary>
    Task<IReadOnlyList<Message>> ListPageAsync(
        Guid conversationId,
        DateTimeOffset? before,
        int limit,
        CancellationToken cancellationToken = default);
}
