using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.OccupancyLinks;

/// <summary>
/// Migración M09-001: vínculos de ocupación e invitaciones de unidad del
/// Sprint 6. No modifica M05-001; `unidades.estadoOcupacion` ya existía como
/// campo flexible desde M07.
/// </summary>
public static class MongoOccupancyMigration
{
    public const string Version = "M09-001";
    public const string Name = "Ocupación M09";
    public const string Description =
        "Crea vinculos_ocupacion e invitaciones_unidad con índices de inquilino/unidad " +
        "por estado y de código único de invitación.";

    public static MongoMigrationDefinition Definition { get; } = new(
        Version,
        Name,
        Description,
        MongoOccupancyIndexes.Definitions);
}
