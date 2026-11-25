using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.Reparaciones;

internal static class MongoReparacionesIndexes
{
    public static IReadOnlyList<MongoCollectionDefinition> Definitions { get; } =
    [
        new(
            MongoReparacionesSchema.ReparacionesCollection,
            [
                new(
                    MongoReparacionesSchema.UnitIndex,
                    [new(MongoReparacionesSchema.UnitField, 1)]),
                new(
                    MongoReparacionesSchema.StatusIndex,
                    [new(MongoReparacionesSchema.StatusField, 1)])
            ])
    ];
}
