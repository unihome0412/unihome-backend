using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.Verification;

internal static class MongoVerificationIndexes
{
    public static IReadOnlyList<MongoCollectionDefinition> Definitions { get; } =
    [
        new(
            MongoVerificationSchema.ExpedientesCollection,
            [
                new(
                    MongoVerificationSchema.UniqueOwnerIndex,
                    [new(MongoVerificationSchema.ExpedienteOwnerField, 1)],
                    unique: true),
                new(
                    MongoVerificationSchema.UniqueFolioIndex,
                    [new(MongoVerificationSchema.ExpedienteFolioField, 1)],
                    unique: true),
                new(
                    MongoVerificationSchema.PropertyIndex,
                    [
                        new(MongoVerificationSchema.ExpedientePropertyField, 1),
                        new(MongoVerificationSchema.ExpedienteCreatedAtField, -1)
                    ]),
                new(
                    MongoVerificationSchema.ReviewIndex,
                    [
                        new(MongoVerificationSchema.ExpedienteSuspiciousField, -1),
                        new(MongoVerificationSchema.ExpedienteStateField, 1),
                        new(MongoVerificationSchema.ExpedienteCreatedAtField, 1)
                    ])
            ]),
        new(
            MongoVerificationSchema.DocumentsCollection,
            [
                new(
                    MongoVerificationSchema.UniqueDocumentVersionIndex,
                    [
                        new(MongoVerificationSchema.DocumentExpedienteField, 1),
                        new(MongoVerificationSchema.DocumentTypeField, 1),
                        new(MongoVerificationSchema.DocumentVersionField, 1)
                    ],
                    unique: true),
                new(
                    MongoVerificationSchema.DocumentHistoryIndex,
                    [
                        new(MongoVerificationSchema.DocumentExpedienteField, 1),
                        new(MongoVerificationSchema.DocumentTypeField, 1),
                        new(MongoVerificationSchema.DocumentCreatedAtField, -1)
                    ])
            ]),
        new(
            MongoVerificationSchema.AuditCollection,
            [
                new(
                    MongoVerificationSchema.AuditExpedienteIndex,
                    [
                        new(MongoVerificationSchema.AuditExpedienteField, 1),
                        new(MongoVerificationSchema.AuditTimestampField, -1)
                    ]),
                new(
                    MongoVerificationSchema.AuditActorIndex,
                    [
                        new(MongoVerificationSchema.AuditActorField, 1),
                        new(MongoVerificationSchema.AuditTimestampField, -1)
                    ])
            ])
    ];
}
