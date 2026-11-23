using UniHome.Domain.Chat;

namespace UniHome.Application.Chat.Abstractions;

public interface IConversationRepository
{
    Task<Conversation?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserta o reemplaza el documento por <c>_id</c>. Como el identificador es
    /// determinístico (<see cref="Conversation.ComputeId"/>), dos solicitudes
    /// simultáneas para iniciar la misma conversación nunca producen duplicados:
    /// Mongo serializa el upsert sobre el mismo <c>_id</c>.
    /// </summary>
    Task SaveAsync(Conversation conversation, CancellationToken cancellationToken = default);

    /// <summary>
    /// EP-M08-03 (HU-069): conversaciones donde la cuenta es propietaria o
    /// solicitante, para el listado de "mis conversaciones".
    /// </summary>
    Task<IReadOnlyList<Conversation>> ListByParticipantAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);
}
