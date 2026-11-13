using MongoDB.Bson;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.PasswordResets;

/// <summary>
/// Índices y contrato de la colección de recuperación de contraseña. La API solo
/// verifica el esquema en modo lectura; la creación versionada corresponde a
/// <see cref="MongoPasswordResetMigration"/>.
/// </summary>
internal static class MongoPasswordResetIndexes
{
    private static IReadOnlyList<MongoIndexDefinition> Indexes { get; } =
    [
        new(
            MongoIdentitySchema.AccountRecoveryUniqueTokenIndex,
            [new(MongoIdentitySchema.AccountRecoveryTokenHashField, 1)],
            unique: true),
        new(
            MongoIdentitySchema.AccountRecoveryAccountIndex,
            [new(MongoIdentitySchema.AccountRecoveryAccountIdField, 1)]),
        new(
            MongoIdentitySchema.AccountRecoveryExpirationTtlIndex,
            [new(MongoIdentitySchema.AccountRecoveryExpiresAtUtcField, 1)],
            expireAfterSeconds: 0)
    ];

    /// <summary>
    /// Contrato físico de la colección. <c>consumedAt</c> es date/null; el token
    /// en claro nunca se persiste.
    /// </summary>
    public static MongoValidatorDefinition Validator { get; } = new(
        BsonDocument.Parse(
            """
            {
              "$jsonSchema": {
                "bsonType": "object",
                "required": ["accountId", "tokenHash", "expiresAt"],
                "properties": {
                  "_id": { "bsonType": ["objectId", "string", "binData"] },
                  "accountId": { "bsonType": ["objectId", "string", "binData"] },
                  "tokenHash": { "bsonType": "string", "maxLength": 4096 },
                  "expiresAt": { "bsonType": "date" },
                  "consumedAt": { "bsonType": ["date", "null"] },
                  "createdAt": { "bsonType": "date" }
                }
              }
            }
            """));

    public static MongoCollectionDefinition Definition { get; } = new(
        MongoIdentitySchema.AccountRecoveryCollection,
        Indexes);

    public static MongoCollectionDefinition MigrationDefinition { get; } = new(
        MongoIdentitySchema.AccountRecoveryCollection,
        Indexes,
        Validator);
}
