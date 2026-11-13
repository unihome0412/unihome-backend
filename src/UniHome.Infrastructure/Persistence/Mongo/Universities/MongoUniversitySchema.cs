namespace UniHome.Infrastructure.Persistence.Mongo.Universities;

/// <summary>
/// Nombres físicos de la colección M03. No cambiar un nombre sin una migración
/// nueva: el checksum versionado protege la definición publicada.
/// </summary>
public static class MongoUniversitySchema
{
    public const string Collection = "universidades";
    public const string NameAndCampusField = "nombreYCampus";
    public const string NormalizedNameField = "nombreNormalizado";
    public const string ActiveField = "activa";
    public const string CreatedAtField = "createdAt";

    public const string UniqueNormalizedNameIndex = "ux_universidades_nombre_normalizado";
}
