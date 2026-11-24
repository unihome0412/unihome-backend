using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.Notifications;

internal static class MongoNotificationsIndexes
{
    public static IReadOnlyList<MongoCollectionDefinition> Definitions { get; } =
    [
        new(
            MongoNotificationsSchema.NotificationsCollection,
            [
                new(
                    MongoNotificationsSchema.RecipientIndex,
                    [new(MongoNotificationsSchema.RecipientField, 1)])
            ])
    ];
}
