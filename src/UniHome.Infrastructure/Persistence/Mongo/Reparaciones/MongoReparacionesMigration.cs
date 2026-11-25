using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.Reparaciones;

/// <summary>
/// Migración M11-001: reportes de reparación del Sprint 8 (HU-062/063/064/065).
/// </summary>
public static class MongoReparacionesMigration
{
    public const string Version = "M11-001";
    public const string Name = "Reparaciones M11";
    public const string Description =
        "Crea reparaciones con índices de unidad y de estado.";

    public static MongoMigrationDefinition Definition { get; } = new(
        Version,
        Name,
        Description,
        MongoReparacionesIndexes.Definitions);
}
