using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.Universities;

/// <summary>
/// Migración M03-001: materializa el catálogo de universidades y su índice único
/// de nombre normalizado. No ejecuta DDL desde la API; se aplica con el migrador
/// versionado en un entorno autorizado.
/// </summary>
public static class MongoUniversityMigration
{
    public const string Version = "M03-001";
    public const string Name = "Catálogo de universidades";
    public const string Description =
        "Crea universidades con el índice único de nombre normalizado usado para " +
        "evitar duplicados en HU-075 y ordenar la búsqueda pública.";

    public static MongoMigrationDefinition Definition { get; } = new(
        Version,
        Name,
        Description,
        [MongoUniversityIndexes.Definition]);
}
