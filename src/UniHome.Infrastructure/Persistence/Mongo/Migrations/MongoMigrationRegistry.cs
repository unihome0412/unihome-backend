namespace UniHome.Infrastructure.Persistence.Mongo.Migrations;

/// <summary>
/// Registro ordenado de migraciones. Las versiones y la propiedad de cada
/// colección son únicas; el checksum de una versión no puede cambiar después de
/// publicarse. La comprobación contra el ledger la realiza la promoción.
/// </summary>
public sealed class MongoMigrationRegistry
{
    public static MongoMigrationRegistry Default { get; } =
        new([MongoIdentityMigration.Definition]);

    public MongoMigrationRegistry(IEnumerable<MongoMigrationDefinition> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);

        var ordered = migrations
            .OrderBy(migration => migration.Version, StringComparer.Ordinal)
            .ToArray();

        if (ordered.Length == 0)
        {
            throw new ArgumentException("El registro debe contener al menos una migración.", nameof(migrations));
        }

        var versions = new HashSet<string>(StringComparer.Ordinal);
        var collections = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var migration in ordered)
        {
            if (!versions.Add(migration.Version))
            {
                throw new ArgumentException(
                    $"La versión de migración está duplicada: {migration.Version}.",
                    nameof(migrations));
            }

            if (migration.Collections.Any(collection =>
                    string.Equals(
                        collection.Name,
                        MongoIdentitySchema.MigrationLedgerCollection,
                        StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    $"La colección {MongoIdentitySchema.MigrationLedgerCollection} está reservada para el ledger.",
                    nameof(migrations));
            }

            foreach (var collection in migration.Collections)
            {
                if (collections.TryGetValue(collection.Name, out var owner))
                {
                    throw new ArgumentException(
                        $"La colección {collection.Name} pertenece a más de una migración " +
                        $"({owner} y {migration.Version}).",
                        nameof(migrations));
                }

                collections.Add(collection.Name, migration.Version);
            }
        }

        Migrations = ordered;
    }

    public IReadOnlyList<MongoMigrationDefinition> Migrations { get; }

    public MongoMigrationDefinition GetRequired(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        return Migrations.FirstOrDefault(migration =>
                   string.Equals(migration.Version, version, StringComparison.Ordinal))
               ?? throw new MongoMigrationRegistryException(
                   $"La migración '{version}' no existe en el registro.");
    }

    public IReadOnlyList<MongoMigrationDefinition> GetUpTo(string version)
    {
        var target = GetRequired(version);
        return Migrations
            .TakeWhile(migration => !string.Equals(
                migration.Version,
                target.Version,
                StringComparison.Ordinal))
            .Append(target)
            .ToArray();
    }

    public void EnsureLedgerChecksum(string version, string? checksum)
    {
        var migration = GetRequired(version);

        if (!string.Equals(migration.Checksum, checksum, StringComparison.OrdinalIgnoreCase))
        {
            throw new MongoMigrationDivergenceException(
                $"La migración {version} tiene un checksum distinto en el ledger.");
        }
    }
}

public sealed class MongoMigrationRegistryException : InvalidOperationException
{
    public MongoMigrationRegistryException(string message)
        : base(message)
    {
    }
}
