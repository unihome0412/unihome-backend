using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.Universities;

/// <summary>
/// Índices de <c>universidades</c>. El índice único sobre el nombre normalizado
/// hace atómica la detección de duplicados; la API solo verifica metadatos y no
/// ejecuta DDL.
/// </summary>
internal static class MongoUniversityIndexes
{
    public static MongoCollectionDefinition Definition { get; } = new(
        MongoUniversitySchema.Collection,
        [
            new(
                MongoUniversitySchema.UniqueNormalizedNameIndex,
                [new(MongoUniversitySchema.NormalizedNameField, 1)],
                unique: true)
        ]);
}
