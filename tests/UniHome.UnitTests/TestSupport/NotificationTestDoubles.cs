using UniHome.Application.Notifications.Abstractions;
using UniHome.Domain.Notifications;

namespace UniHome.UnitTests.TestSupport;

internal sealed class FakeNotificationRepository : INotificationRepository
{
    public List<Notification> Items { get; } = [];

    public Task SaveAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        Items.RemoveAll(item => item.Id == notification.Id);
        Items.Add(notification);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Notification>> ListByRecipientAsync(
        Guid recipientAccountId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Notification>>(Items
            .Where(notification => notification.RecipientAccountId == recipientAccountId)
            .OrderByDescending(notification => notification.CreatedAt)
            .ToArray());
}
