namespace UniHome.Infrastructure.Persistence.Mongo.OccupancyLinks;

/// <summary>Nombres físicos M09. Un cambio exige una migración nueva.</summary>
public static class MongoOccupancySchema
{
    public const string LinksCollection = "vinculos_ocupacion";
    public const string InvitationsCollection = "invitaciones_unidad";

    public const string LinkTenantField = "inquilinoAccountId";
    public const string LinkUnitField = "unitId";
    public const string LinkStatusField = "estado";
    public const string LinkJoinedAtField = "fechaVinculo";
    public const string LinkEndedAtField = "fechaFin";
    public const string LinkEndedReasonField = "motivoFin";

    public const string InvitationUnitField = "unitId";
    public const string InvitationCodeField = "codigo";
    public const string InvitationCreatedAtField = "createdAt";
    public const string InvitationExpiresAtField = "vigenciaHasta";

    public const string TenantStatusIndex = "ix_ocupacion_inquilino_estado";
    public const string UnitStatusIndex = "ix_ocupacion_unidad_estado";

    public const string InvitationUnitIndex = "ix_invitaciones_unidad";
    public const string InvitationCodeUniqueIndex = "ux_invitaciones_codigo";
}
