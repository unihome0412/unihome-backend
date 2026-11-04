using MongoDB.Driver;

namespace UniHome.Infrastructure.Persistence.Mongo.Accounts;

internal static class AccountIndexes
{
    public const string CollectionName = "cuentas";
    public const string UniqueEmailIndexName = "ux_cuentas_correo_normalizado";

    /// <summary>
    /// Índice único sobre el correo normalizado. Es la garantía definitiva ante
    /// registros concurrentes con el mismo correo; la comprobación previa no basta.
    /// </summary>
    public static CreateIndexModel<AccountDocument> UniqueNormalizedEmail { get; } = new(
        Builders<AccountDocument>.IndexKeys.Ascending(document => document.NormalizedEmail),
        new CreateIndexOptions
        {
            Unique = true,
            Name = UniqueEmailIndexName
        });
}
