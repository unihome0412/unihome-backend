using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.Chat;

/// <summary>
/// Migración M08-001: conversaciones y mensajes del Sprint 6 (M08 Chat).
/// </summary>
public static class MongoChatMigration
{
    public const string Version = "M08-001";
    public const string Name = "Chat M08";
    public const string Description =
        "Crea conversaciones y mensajes con índice de participantes y de " +
        "conversación/fecha para la paginación del historial.";

    public static MongoMigrationDefinition Definition { get; } = new(
        Version,
        Name,
        Description,
        MongoChatIndexes.Definitions);
}
