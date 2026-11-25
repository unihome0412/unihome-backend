using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using UniHome.Application.Identity.Exceptions;
using UniHome.Application.Notifications.Abstractions;
using UniHome.Domain.Notifications;

namespace UniHome.Infrastructure.Persistence.Mongo.Notifications;

[ExcludeFromCodeCoverage]
internal sealed class MongoNotificationRepository : INotificationRepository
{
    private readonly MongoDbClientProvider _clientProvider;
    private readonly ILogger<MongoNotificationRepository> _logger;

    public MongoNotificationRepository(
        MongoDbClientProvider clientProvider,
        ILogger<MongoNotificationRepository> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    public async Task SaveAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            await collection
                .ReplaceOneAsync(
                    Builders<NotificationDocument>.Filter.Eq(
                        item => item.Id,
                        notification.Id.ToString("N", CultureInfo.InvariantCulture)),
                    ToDocument(notification),
                    new ReplaceOptions { IsUpsert = true },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    public async Task<IReadOnlyList<Notification>> ListByRecipientAsync(
        Guid recipientAccountId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = await GetCollectionAsync(cancellationToken).ConfigureAwait(false);
            var documents = await collection
                .Find(Builders<NotificationDocument>.Filter.Eq(
                    item => item.RecipientAccountId,
                    recipientAccountId.ToString("N", CultureInfo.InvariantCulture)))
                .Sort(Builders<NotificationDocument>.Sort.Descending(item => item.CreatedAt))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return documents.Select(ToDomain).ToArray();
        }
        catch (MongoException exception)
        {
            LogStorageFailure(exception);
            throw new StorageUnavailableException();
        }
    }

    private async Task<IMongoCollection<NotificationDocument>> GetCollectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_clientProvider.IsConfigured)
        {
            throw new StorageUnavailableException();
        }

        var collection = _clientProvider
            .GetDatabase()
            .GetCollection<NotificationDocument>(MongoNotificationsSchema.NotificationsCollection);
        await MongoIdentitySchemaGuard.EnsureAsync(collection, cancellationToken).ConfigureAwait(false);
        return collection;
    }

    private static NotificationDocument ToDocument(Notification notification) =>
        new()
        {
            Id = notification.Id.ToString("N", CultureInfo.InvariantCulture),
            RecipientAccountId = notification.RecipientAccountId.ToString("N", CultureInfo.InvariantCulture),
            Type = notification.Type.ToString(),
            Message = notification.Message,
            UnitId = notification.UnitId?.ToString("N", CultureInfo.InvariantCulture),
            Periodo = notification.Periodo,
            CreatedAt = notification.CreatedAt.UtcDateTime
        };

    private static Notification ToDomain(NotificationDocument document) =>
        Notification.Restore(
            Guid.ParseExact(document.Id, "N"),
            Guid.ParseExact(document.RecipientAccountId, "N"),
            Enum.Parse<NotificationType>(document.Type),
            document.Message,
            document.UnitId is null ? null : Guid.ParseExact(document.UnitId, "N"),
            document.Periodo,
            new DateTimeOffset(DateTime.SpecifyKind(document.CreatedAt, DateTimeKind.Utc)));

    private void LogStorageFailure(MongoException exception)
    {
        _logger.LogWarning(
            "La operación de notificaciones en MongoDB falló. Tipo={ExceptionType} TraceId={TraceId}",
            exception.GetType().FullName,
            Activity.Current?.Id);
    }
}
