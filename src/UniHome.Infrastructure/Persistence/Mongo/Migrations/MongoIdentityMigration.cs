namespace UniHome.Infrastructure.Persistence.Mongo.Migrations;

/// <summary>
/// Migración M01-001. No crea validadores JSON para estas dos colecciones porque
/// el contrato de M01 no los define; solo materializa las colecciones y los
/// índices que el código ya utiliza.
/// </summary>
public static class MongoIdentityMigration
{
    public const string Version = "M01-001";
    public const string Name = "Identidad M01: cuentas y sesiones_refresh";
    public const string Description =
        "Crea cuentas y sesiones_refresh con el índice único de correo, los índices " +
        "de sesión y el TTL de limpieza. La vigencia de autorización se comprueba en el backend.";

    public static MongoMigrationDefinition Definition { get; } = CreateDefinition();

    public static IReadOnlySet<string> ManagedCollections { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            MongoIdentitySchema.AccountsCollection,
            MongoIdentitySchema.SessionsCollection
        };

    private static MongoMigrationDefinition CreateDefinition() =>
        new(
            Version,
            Name,
            Description,
            [
                new(
                    MongoIdentitySchema.AccountsCollection,
                    [
                        new(
                            MongoIdentitySchema.AccountsUniqueEmailIndex,
                            [new(MongoIdentitySchema.AccountsNormalizedEmailField, 1)],
                            unique: true)
                    ]),
                new(
                    MongoIdentitySchema.SessionsCollection,
                    [
                        new(
                            MongoIdentitySchema.SessionsUniqueCurrentTokenIndex,
                            [new(MongoIdentitySchema.SessionsCurrentTokenHashField, 1)],
                            unique: true),
                        new(
                            MongoIdentitySchema.SessionsAccountIndex,
                            [new(MongoIdentitySchema.SessionsAccountIdField, 1)]),
                        new(
                            MongoIdentitySchema.SessionsExpirationTtlIndex,
                            [new(MongoIdentitySchema.SessionsExpiresAtUtcField, 1)],
                            expireAfterSeconds: 0)
                    ])
            ]);
}
