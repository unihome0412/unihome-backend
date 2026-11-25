namespace UniHome.Infrastructure.Persistence.Mongo.Reparaciones;

/// <summary>Nombres físicos M11. Un cambio exige una migración nueva.</summary>
public static class MongoReparacionesSchema
{
    public const string ReparacionesCollection = "reparaciones";

    public const string UnitField = "unitId";
    public const string ReportedByField = "reportadoPorAccountId";
    public const string TitleField = "titulo";
    public const string DescriptionField = "descripcion";
    public const string StatusField = "estado";
    public const string PhotoField = "foto";
    public const string CreatedAtField = "createdAt";
    public const string UpdatedAtField = "updatedAt";

    public const string PhotoStorageKeyField = "storageKey";
    public const string PhotoContentTypeField = "contentType";
    public const string PhotoSizeBytesField = "sizeBytes";
    public const string PhotoSha256Field = "sha256";

    public const string UnitIndex = "ix_reparaciones_unidad";
    public const string StatusIndex = "ix_reparaciones_estado";
}
