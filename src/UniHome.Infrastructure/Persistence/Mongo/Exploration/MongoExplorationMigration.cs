using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.Exploration;

/// <summary>
/// M07-001 crea solo la colección nueva de favoritos. No toca M05-001 ni sus
/// índices, cuyo checksum histórico permanece intacto.
/// </summary>
public static class MongoExplorationMigration
{
    public const string Version = "M07-001";
    public const string Name = "Favoritos M07";
    public const string Description =
        "Crea favoritos con índice único cuenta/unidad y consulta paginada por cuenta.";

    public static MongoMigrationDefinition Definition { get; } = new(
        Version,
        Name,
        Description,
        [MongoExplorationIndexes.Definition]);
}
