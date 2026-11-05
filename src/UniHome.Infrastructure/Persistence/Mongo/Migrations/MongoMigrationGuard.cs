namespace UniHome.Infrastructure.Persistence.Mongo.Migrations;

/// <summary>
/// Protección de la herramienta de migración: la base de desarrollo no puede
/// recibir DDL por accidente y las bases admitidas deben declararse explícitamente.
/// </summary>
public static class MongoMigrationGuard
{
    public const string TestDatabaseName = "unihome_modelo_pruebas";
    public const string AllowlistEnvironmentVariable = "UNIHOME_MIGRATION_ALLOWED_DATABASES";

    private static readonly HashSet<string> AlwaysForbidden = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "local",
        "config",
        "unihome_desarrollo"
    };

    public static bool IsAllowed(string? databaseName, string? allowedDatabases = null)
    {
        var candidate = databaseName?.Trim();

        if (string.IsNullOrWhiteSpace(candidate) || AlwaysForbidden.Contains(candidate))
        {
            return false;
        }

        if (string.Equals(candidate, TestDatabaseName, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(allowedDatabases))
        {
            return false;
        }

        return allowedDatabases
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(item => string.Equals(item, candidate, StringComparison.Ordinal));
    }

    public static void EnsureAllowed(string? databaseName, string? allowedDatabases = null)
    {
        if (IsAllowed(databaseName, allowedDatabases))
        {
            return;
        }

        throw new InvalidOperationException(
            $"La base '{databaseName}' no está autorizada para migraciones. " +
            $"Solo se admite '{TestDatabaseName}' o una base incluida en " +
            $"{AllowlistEnvironmentVariable}.");
    }
}
