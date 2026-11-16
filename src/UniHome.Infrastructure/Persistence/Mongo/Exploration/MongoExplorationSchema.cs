namespace UniHome.Infrastructure.Persistence.Mongo.Exploration;

/// <summary>
/// Nombres físicos de M04/M07. M04 reutiliza los índices de M05; M07 solo añade
/// la colección de favoritos para no cambiar el checksum histórico de M05-001.
/// </summary>
public static class MongoExplorationSchema
{
    public const string FavoritesCollection = "favoritos";
    public const string FavoriteAccountField = "accountId";
    public const string FavoriteUnitField = "unitId";
    public const string FavoriteCreatedAtField = "createdAt";
    public const string FavoriteUniqueIndex = "ux_favoritos_cuenta_unidad";
    public const string FavoriteAccountIndex = "ix_favoritos_cuenta_fecha";

    public const string UnitAvailabilityField = "estadoOcupacion";
}
