using UniHome.Application.Notifications.Abstractions;
using UniHome.Domain.Notifications;

namespace UniHome.IntegrationTests.TestSupport;

public sealed class InMemoryNotificationRepository : INotificationRepository
{
    private readonly object _gate = new();
    private readonly List<Notification> _notifications = [];

    public Task SaveAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _notifications.RemoveAll(item => item.Id == notification.Id);
            _notifications.Add(notification);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Notification>> ListByRecipientAsync(
        Guid recipientAccountId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<Notification> items = _notifications
                .Where(notification => notification.RecipientAccountId == recipientAccountId)
                .OrderByDescending(notification => notification.CreatedAt)
                .ToArray();
            return Task.FromResult(items);
        }
    }
}
