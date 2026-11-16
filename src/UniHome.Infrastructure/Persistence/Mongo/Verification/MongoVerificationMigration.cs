using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.Verification;

/// <summary>
/// M06-001 crea el expediente, los documentos cifrados y la bitácora inmutable.
/// No modifica M05-001: la publicación reutiliza `unidades.estadoPublicacion`.
/// </summary>
public static class MongoVerificationMigration
{
    public const string Version = "M06-001";
    public const string Name = "Verificación documental M06";
    public const string Description =
        "Crea expedientes_verificacion, documentos_verificacion y eventos_expediente " +
        "con índices de unicidad, cola de revisión y auditoría por expediente/actor.";

    public static MongoMigrationDefinition Definition { get; } = new(
        Version,
        Name,
        Description,
        MongoVerificationIndexes.Definitions);
}
