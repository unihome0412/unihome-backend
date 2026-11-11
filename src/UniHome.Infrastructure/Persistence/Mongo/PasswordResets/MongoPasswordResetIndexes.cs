using MongoDB.Driver;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.PasswordResets;

/// <summary>
/// Índices de la colección externa <c>restablecimientos_contrasena</c>. La
/// colección ya existe en el modelo aprobado; la API solo verifica estos índices
/// en modo lectura y nunca ejecuta DDL. No se añade una migración nueva.
/// </summary>
internal static class MongoPasswordResetIndexes
{
    public static MongoCollectionDefinition Definition { get; } = new(
        MongoIdentitySchema.AccountRecoveryCollection,
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
        ]);
}
