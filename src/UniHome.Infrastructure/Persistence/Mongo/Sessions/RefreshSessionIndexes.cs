using MongoDB.Driver;

namespace UniHome.Infrastructure.Persistence.Mongo.Sessions;

internal static class RefreshSessionIndexes
{
    public const string CollectionName = MongoIdentitySchema.SessionsCollection;
    public const string UniqueCurrentTokenIndexName = MongoIdentitySchema.SessionsUniqueCurrentTokenIndex;
    public const string AccountIndexName = MongoIdentitySchema.SessionsAccountIndex;
    public const string ExpirationTtlIndexName = MongoIdentitySchema.SessionsExpirationTtlIndex;

    /// <summary>
    /// El hash vigente es único para detectar colisiones y apoyar la rotación.
    /// El TTL elimina sesiones vencidas; las revocadas permanecen hasta su expiración.
    /// </summary>
    public static IReadOnlyList<CreateIndexModel<RefreshSessionDocument>> All { get; } =
    [
        new(
            Builders<RefreshSessionDocument>.IndexKeys
                .Ascending(MongoIdentitySchema.SessionsCurrentTokenHashField),
            new CreateIndexOptions
            {
                Unique = true,
                Name = UniqueCurrentTokenIndexName
            }),
        new(
            Builders<RefreshSessionDocument>.IndexKeys
                .Ascending(MongoIdentitySchema.SessionsAccountIdField),
            new CreateIndexOptions { Name = AccountIndexName }),
        new(
            Builders<RefreshSessionDocument>.IndexKeys
                .Ascending(MongoIdentitySchema.SessionsExpiresAtUtcField),
            new CreateIndexOptions
            {
                ExpireAfter = TimeSpan.Zero,
                Name = ExpirationTtlIndexName
            })
    ];
}
