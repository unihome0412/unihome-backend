using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.OccupancyLinks;

internal static class MongoOccupancyIndexes
{
    public static IReadOnlyList<MongoCollectionDefinition> Definitions { get; } =
    [
        new(
            MongoOccupancySchema.LinksCollection,
            [
                new(
                    MongoOccupancySchema.TenantStatusIndex,
                    [
                        new(MongoOccupancySchema.LinkTenantField, 1),
                        new(MongoOccupancySchema.LinkStatusField, 1)
                    ]),
                new(
                    MongoOccupancySchema.UnitStatusIndex,
                    [
                        new(MongoOccupancySchema.LinkUnitField, 1),
                        new(MongoOccupancySchema.LinkStatusField, 1)
                    ])
            ]),
        new(
            MongoOccupancySchema.InvitationsCollection,
            [
                new(
                    MongoOccupancySchema.InvitationUnitIndex,
                    [
                        new(MongoOccupancySchema.InvitationUnitField, 1),
                        new(MongoOccupancySchema.InvitationCreatedAtField, -1)
                    ]),
                new(
                    MongoOccupancySchema.InvitationCodeUniqueIndex,
                    [new(MongoOccupancySchema.InvitationCodeField, 1)],
                    unique: true)
            ])
    ];
}
