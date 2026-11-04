using MongoDB.Driver;

namespace UniHome.Infrastructure.Persistence.Mongo.Sessions;

internal static class RefreshSessionIndexes
{
    public const string CollectionName = "sesiones_refresh";
    public const string UniqueCurrentTokenIndexName = "ux_sesiones_refresh_token_actual";
    public const string AccountIndexName = "ix_sesiones_refresh_cuenta";
    public const string ExpirationTtlIndexName = "ttl_sesiones_refresh_expiracion";

    /// <summary>
    /// El hash vigente es único para detectar colisiones y apoyar la rotación.
    /// El TTL elimina sesiones vencidas; las revocadas permanecen hasta su expiración.
    /// </summary>
    public static IReadOnlyList<CreateIndexModel<RefreshSessionDocument>> All { get; } =
    [
        new(
            Builders<RefreshSessionDocument>.IndexKeys
                .Ascending(document => document.CurrentTokenHash),
            new CreateIndexOptions
            {
                Unique = true,
                Name = UniqueCurrentTokenIndexName
            }),
        new(
            Builders<RefreshSessionDocument>.IndexKeys
                .Ascending(document => document.AccountId),
            new CreateIndexOptions { Name = AccountIndexName }),
        new(
            Builders<RefreshSessionDocument>.IndexKeys
                .Ascending(document => document.ExpiresAtUtc),
            new CreateIndexOptions
            {
                ExpireAfter = TimeSpan.Zero,
                Name = ExpirationTtlIndexName
            })
    ];
}
