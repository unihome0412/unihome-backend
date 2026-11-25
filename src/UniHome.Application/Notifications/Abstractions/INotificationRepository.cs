using UniHome.Domain.Notifications;

namespace UniHome.Application.Notifications.Abstractions;

public interface INotificationRepository
{
    /// <summary>Inserta o reemplaza por <c>Id</c>; el id determinista de HU-059 hace esto un upsert idempotente.</summary>
    Task SaveAsync(Notification notification, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notification>> ListByRecipientAsync(
        Guid recipientAccountId,
        CancellationToken cancellationToken = default);
}
