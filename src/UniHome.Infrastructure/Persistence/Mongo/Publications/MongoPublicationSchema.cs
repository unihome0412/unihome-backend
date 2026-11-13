namespace UniHome.Infrastructure.Persistence.Mongo.Publications;

/// <summary>Nombres físicos M05. Un cambio exige una migración nueva.</summary>
public static class MongoPublicationSchema
{
    public const string PropertiesCollection = "propiedades";
    public const string UnitsCollection = "unidades";
    public const string TermsCollection = "aceptaciones_terminos";

    public const string PropertyOwnerField = "ownerAccountId";
    public const string PropertyNameField = "nombre";
    public const string PropertyNameNormalizedField = "nombreNormalizado";
    public const string PropertyAddressField = "direccion";
    public const string PropertyAddressNormalizedField = "direccionNormalizada";
    public const string PropertyColoniaField = "colonia";
    public const string PropertyColoniaNormalizedField = "coloniaNormalizada";
    public const string PropertyTypeField = "tipoPropiedad";
    public const string PropertyUniversityField = "universidadId";
    public const string PropertyLocationField = "location";
    public const string PropertyFloorField = "piso";
    public const string PropertyUnitNumberField = "numeroUnidad";
    public const string PropertyStateField = "estadoPublicacion";
    public const string PropertyFirstApprovalField = "firstApprovalAt";
    public const string PropertyCreatedAtField = "createdAt";

    public const string UnitPropertyField = "propertyId";
    public const string UnitOwnerField = "ownerAccountId";
    public const string UnitUniversityField = "universidadId";
    public const string UnitTypeField = "tipoUnidad";
    public const string UnitOccupancyField = "ocupacion";
    public const string UnitPlacesField = "lugares";
    public const string UnitPriceField = "precioMensualPorLugarMxn";
    public const string UnitSizeField = "tamanoM2";
    public const string UnitForWhomField = "paraQuien";
    public const string UnitBathroomField = "bano";
    public const string UnitFurnitureField = "mobiliario";
    public const string UnitFurnitureItemsField = "muebles";
    public const string UnitTemplateField = "plantillaBase";
    public const string UnitZoneField = "informacionZona";
    public const string UnitRulesField = "reglasConvivencia";
    public const string UnitServicesField = "servicios";
    public const string UnitPhotosField = "fotos";
    public const string UnitStateField = "estadoPublicacion";
    public const string UnitCreatedAtField = "createdAt";

    public const string TermsAccountField = "accountId";
    public const string TermsVersionField = "versionTerminos";
    public const string TermsAcceptedAtField = "acceptedAt";

    public const string PropertyOwnerCreatedIndex = "ix_propiedades_propietario";
    public const string PropertyUniqueAddressIndex = "ux_propiedades_propietario_direccion";
    public const string UnitPropertyIndex = "ix_unidades_propiedad";
    public const string UnitSearchIndex = "ix_unidades_busqueda";
    public const string TermsUniqueIndex = "ux_aceptaciones_cuenta_version";
    public const string TermsAccountIndex = "ix_aceptaciones_cuenta";
}
