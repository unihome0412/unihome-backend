using MongoDB.Driver;

namespace UniHome.Infrastructure.Persistence.Mongo.Accounts;

internal static class AccountIndexes
{
    public const string CollectionName = MongoIdentitySchema.AccountsCollection;
    public const string UniqueEmailIndexName = MongoIdentitySchema.AccountsUniqueEmailIndex;

    /// <summary>
    /// Índice único sobre el correo normalizado. La migración versionada es la
    /// única responsable de crearlo; el repositorio no ejecuta DDL.
    /// </summary>
    public static CreateIndexModel<AccountDocument> UniqueNormalizedEmail { get; } = new(
        Builders<AccountDocument>.IndexKeys.Ascending(MongoIdentitySchema.AccountsNormalizedEmailField),
        new CreateIndexOptions
        {
            Unique = true,
            Name = UniqueEmailIndexName
        });
}
