using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.Publications;

/// <summary>
/// Migración M05-001: propiedades, unidades y aceptaciones de términos del
/// Sprint 2. No crea validadores JSON para no inventar contratos no ratificados.
/// </summary>
public static class MongoPublicationMigration
{
    public const string Version = "M05-001";
    public const string Name = "Publicaciones M05";
    public const string Description =
        "Crea propiedades, unidades y aceptaciones de términos con sus índices de " +
        "propiedad, búsqueda y unicidad de términos.";

    public static MongoMigrationDefinition Definition { get; } = new(
        Version,
        Name,
        Description,
        MongoPublicationIndexes.Definitions);
}
