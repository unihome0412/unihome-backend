namespace UniHome.Infrastructure.Persistence.Mongo.Verification;

/// <summary>Nombres físicos M06. Un cambio exige una migración nueva.</summary>
public static class MongoVerificationSchema
{
    public const string ExpedientesCollection = "expedientes_verificacion";
    public const string DocumentsCollection = "documentos_verificacion";
    public const string AuditCollection = "eventos_expediente";

    public const string ExpedienteOwnerField = "ownerAccountId";
    public const string ExpedientePublicationField = "publicationId";
    public const string ExpedientePropertyField = "propertyId";
    public const string ExpedienteFolioField = "folio";
    public const string ExpedienteStateField = "estado";
    public const string ExpedienteSuspiciousField = "esSospechoso";
    public const string ExpedienteCreatedAtField = "createdAt";
    public const string ExpedienteUpdatedAtField = "updatedAt";

    public const string DocumentExpedienteField = "expedienteId";
    public const string DocumentTypeField = "tipo";
    public const string DocumentVersionField = "version";
    public const string DocumentCreatedAtField = "createdAt";

    public const string AuditExpedienteField = "expedienteId";
    public const string AuditActorField = "actorAccountId";
    public const string AuditTimestampField = "timestamp";

    public const string UniqueOwnerIndex = "ux_expedientes_propietario";
    public const string UniqueFolioIndex = "ux_expedientes_folio";
    public const string PropertyIndex = "ix_expedientes_propiedad";
    public const string ReviewIndex = "ix_expedientes_revision";

    public const string UniqueDocumentVersionIndex = "ux_documentos_expediente_tipo_version";
    public const string DocumentHistoryIndex = "ix_documentos_expediente_tipo_fecha";

    public const string AuditExpedienteIndex = "ix_eventos_expediente_fecha";
    public const string AuditActorIndex = "ix_eventos_actor_fecha";
}
