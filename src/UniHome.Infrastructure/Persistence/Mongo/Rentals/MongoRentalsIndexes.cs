using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.Infrastructure.Persistence.Mongo.Rentals;

internal static class MongoRentalsIndexes
{
    public static IReadOnlyList<MongoCollectionDefinition> Definitions { get; } =
    [
        new(
            MongoRentalsSchema.RentPeriodsCollection,
            [
                new(
                    MongoRentalsSchema.RentPeriodUniqueIndex,
                    [
                        new(MongoRentalsSchema.RentPeriodUnitField, 1),
                        new(MongoRentalsSchema.RentPeriodPeriodoField, 1)
                    ],
                    unique: true)
            ]),
        new(
            MongoRentalsSchema.PaymentsCollection,
            [
                new(
                    MongoRentalsSchema.PaymentPeriodTenantIndex,
                    [
                        new(MongoRentalsSchema.PaymentRentPeriodField, 1),
                        new(MongoRentalsSchema.PaymentTenantField, 1)
                    ])
            ])
    ];
}
