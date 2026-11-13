using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.Publications;

internal static class MongoPublicationIndexes
{
    public static IReadOnlyList<MongoCollectionDefinition> Definitions { get; } =
    [
        new(
            MongoPublicationSchema.PropertiesCollection,
            [
                new(
                    MongoPublicationSchema.PropertyUniqueAddressIndex,
                    [
                        new(MongoPublicationSchema.PropertyOwnerField, 1),
                        new(MongoPublicationSchema.PropertyAddressNormalizedField, 1),
                        new(MongoPublicationSchema.PropertyColoniaNormalizedField, 1),
                        new(MongoPublicationSchema.PropertyNameNormalizedField, 1)
                    ],
                    unique: true),
                new(
                    MongoPublicationSchema.PropertyOwnerCreatedIndex,
                    [
                        new(MongoPublicationSchema.PropertyOwnerField, 1),
                        new(MongoPublicationSchema.PropertyCreatedAtField, -1)
                    ])
            ]),
        new(
            MongoPublicationSchema.UnitsCollection,
            [
                new(
                    MongoPublicationSchema.UnitPropertyIndex,
                    [
                        new(MongoPublicationSchema.UnitPropertyField, 1),
                        new(MongoPublicationSchema.UnitCreatedAtField, -1)
                    ]),
                new(
                    MongoPublicationSchema.UnitSearchIndex,
                    [
                        new(MongoPublicationSchema.UnitStateField, 1),
                        new(MongoPublicationSchema.UnitPriceField, 1)
                    ])
            ]),
        new(
            MongoPublicationSchema.TermsCollection,
            [
                new(
                    MongoPublicationSchema.TermsUniqueIndex,
                    [
                        new(MongoPublicationSchema.TermsAccountField, 1),
                        new(MongoPublicationSchema.TermsVersionField, 1)
                    ],
                    unique: true),
                new(
                    MongoPublicationSchema.TermsAccountIndex,
                    [new(MongoPublicationSchema.TermsAccountField, 1)])
            ])
    ];
}
