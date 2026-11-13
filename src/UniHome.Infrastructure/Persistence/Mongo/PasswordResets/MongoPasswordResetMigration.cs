using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.PasswordResets;

/// <summary>
/// Migración M01-002. Materializa la colección externa de recuperación de
/// contraseña con sus tres índices y el validador que exige <c>consumedAt</c>
/// como date/null. No modifica M01-001 ni el ledger existente.
/// </summary>
public static class MongoPasswordResetMigration
{
    public const string Version = "M01-002";
    public const string Name = "Recuperación de contraseña M01-002";
    public const string Description =
        "Crea restablecimientos_contrasena con tokenHash único, accountId, TTL de " +
        "expiración y consumedAt date/null; no persiste tokens en claro.";

    public static MongoMigrationDefinition Definition { get; } = new(
        Version,
        Name,
        Description,
        [MongoPasswordResetIndexes.MigrationDefinition]);
}
