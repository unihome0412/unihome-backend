using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.Exploration;

internal static class MongoExplorationIndexes
{
    public static MongoCollectionDefinition Definition { get; } = new(
        MongoExplorationSchema.FavoritesCollection,
        [
            new(
                MongoExplorationSchema.FavoriteUniqueIndex,
                [
                    new(MongoExplorationSchema.FavoriteAccountField, 1),
                    new(MongoExplorationSchema.FavoriteUnitField, 1)
                ],
                unique: true),
            new(
                MongoExplorationSchema.FavoriteAccountIndex,
                [
                    new(MongoExplorationSchema.FavoriteAccountField, 1),
                    new(MongoExplorationSchema.FavoriteCreatedAtField, -1)
                ])
        ]);
}
