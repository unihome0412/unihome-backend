using MongoDB.Bson;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Chat;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
using UniHome.Infrastructure.Persistence.Mongo.Exploration;
using UniHome.Infrastructure.Persistence.Mongo.Notifications;
using UniHome.Infrastructure.Persistence.Mongo.OccupancyLinks;
using UniHome.Infrastructure.Persistence.Mongo.PasswordResets;
using UniHome.Infrastructure.Persistence.Mongo.Publications;
using UniHome.Infrastructure.Persistence.Mongo.Rentals;
using UniHome.Infrastructure.Persistence.Mongo.Universities;
using UniHome.Infrastructure.Persistence.Mongo.Verification;

namespace UniHome.UnitTests.Infrastructure;

public sealed class MongoMigrationRegistryTests
{
    [Fact]
    public void RegistroPorDefecto_DebeContenerM01AM12EnOrden()
    {
        var registry = MongoMigrationRegistry.Default;

        Assert.Equal(
            [
                MongoIdentityMigration.Version,
                MongoPasswordResetMigration.Version,
                MongoUniversityMigration.Version,
                MongoPublicationMigration.Version,
                MongoVerificationMigration.Version,
                MongoExplorationMigration.Version,
                MongoChatMigration.Version,
                MongoOccupancyMigration.Version,
                MongoRentalsMigration.Version,
                MongoNotificationsMigration.Version
            ],
            registry.Migrations.Select(migration => migration.Version));
        Assert.Equal(MongoIdentityMigration.Definition.Checksum, registry.Migrations[0].Checksum);
        Assert.Equal(MongoPasswordResetMigration.Definition.Checksum, registry.Migrations[1].Checksum);
        Assert.Equal(MongoUniversityMigration.Definition.Checksum, registry.Migrations[2].Checksum);
        Assert.Equal(MongoPublicationMigration.Definition.Checksum, registry.Migrations[3].Checksum);
        Assert.Equal(MongoVerificationMigration.Definition.Checksum, registry.Migrations[4].Checksum);
        Assert.Equal(MongoExplorationMigration.Definition.Checksum, registry.Migrations[5].Checksum);
        Assert.Equal(MongoChatMigration.Definition.Checksum, registry.Migrations[6].Checksum);
        Assert.Equal(MongoOccupancyMigration.Definition.Checksum, registry.Migrations[7].Checksum);
        Assert.Equal(MongoRentalsMigration.Definition.Checksum, registry.Migrations[8].Checksum);
        Assert.Equal(MongoNotificationsMigration.Definition.Checksum, registry.Migrations[9].Checksum);
        Assert.Contains(MongoIdentitySchema.AccountsCollection, registry.ManagedCollections);
        Assert.Contains(MongoIdentitySchema.AccountRecoveryCollection, registry.ManagedCollections);
        Assert.Contains(MongoUniversitySchema.Collection, registry.ManagedCollections);
        Assert.Contains(MongoPublicationSchema.PropertiesCollection, registry.ManagedCollections);
        Assert.Contains(MongoPublicationSchema.UnitsCollection, registry.ManagedCollections);
        Assert.Contains(MongoPublicationSchema.TermsCollection, registry.ManagedCollections);
        Assert.Contains(MongoVerificationSchema.ExpedientesCollection, registry.ManagedCollections);
        Assert.Contains(MongoVerificationSchema.DocumentsCollection, registry.ManagedCollections);
        Assert.Contains(MongoVerificationSchema.AuditCollection, registry.ManagedCollections);
        Assert.Contains(MongoExplorationSchema.FavoritesCollection, registry.ManagedCollections);
        Assert.Contains(MongoChatSchema.ConversationsCollection, registry.ManagedCollections);
        Assert.Contains(MongoChatSchema.MessagesCollection, registry.ManagedCollections);
        Assert.Contains(MongoOccupancySchema.LinksCollection, registry.ManagedCollections);
        Assert.Contains(MongoOccupancySchema.InvitationsCollection, registry.ManagedCollections);
        Assert.Contains(MongoRentalsSchema.RentPeriodsCollection, registry.ManagedCollections);
        Assert.Contains(MongoRentalsSchema.PaymentsCollection, registry.ManagedCollections);
        Assert.Contains(MongoNotificationsSchema.NotificationsCollection, registry.ManagedCollections);
    }

    [Fact]
    public void MigracionesPublicadas_DebenConservarSuChecksum()
    {
        // El ledger de cualquier entorno (incluida la base de verificación de
        // Atlas) rechaza la migración si el checksum cambia. Estos valores son
        // la referencia contractual de M01-001...M07-001 y solo pueden cambiar
        // publicando una versión nueva, nunca reescribiendo una existente.
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["M01-001"] = "6FE8415A25A62BFA8C2A6D2F12C591B6645FF388D69E1B5260298B940E08CC4D",
            ["M01-002"] = "C36167BBDB93A2A00BC1FB5140B01E2276B0F90078F96EE7187A28F0C2B45031",
            ["M03-001"] = "559FC5D2CB1F4307C5A12CE7A753DEEAE147C456C02C186A16DFF547D979CD44",
            ["M05-001"] = "190A17E494DE79980C9A7819E0B0A1145CD44177B8B99A9093368125889356D5",
            ["M06-001"] = "A4C76265FC753CAEF0EA0EE971063F1B8483696339A58FC53E8111665C35A33A",
            ["M07-001"] = "003D4CC39CCB6ACC6E44C8762AEEBD60C417A08C03ABA35B2AE585392F38887E",
            ["M08-001"] = "6AA8900080A8F0F4F12D56A7CD49CD31B6A145A9E067F9D306A7D14A7AFEFA66",
            ["M09-001"] = "097B741234F5DF9DF0F53E611BDF3C1D0D41F23197CF6466FEE6825707DCA237",
            ["M10-001"] = "41D877A8CB04A72A5D5C5C2F24EFEB22332D09E647D523919B90672B98AEE9C0",
            ["M12-001"] = "7D5024EA255550A1FEB797AF3398B125BB7C336BABA5E20D71513EABB39CC875"
        };

        Assert.Equal(
            expected,
            MongoMigrationRegistry.Default.Migrations.ToDictionary(
                migration => migration.Version,
                migration => migration.Checksum,
                StringComparer.Ordinal));
    }

    [Fact]
    public void M07_DebeCrearSoloFavoritosConSusDosIndices()
    {
        var migration = MongoExplorationMigration.Definition;

        var collection = Assert.Single(migration.Collections);
        Assert.Equal(MongoExplorationSchema.FavoritesCollection, collection.Name);
        Assert.Equal(2, collection.Indexes.Count);

        var unique = Assert.Single(
            collection.Indexes,
            index => index.Name == MongoExplorationSchema.FavoriteUniqueIndex);
        Assert.True(unique.Unique);
        Assert.Equal(
            [
                MongoExplorationSchema.FavoriteAccountField,
                MongoExplorationSchema.FavoriteUnitField
            ],
            unique.Fields.Select(field => field.Name));

        var byAccount = Assert.Single(
            collection.Indexes,
            index => index.Name == MongoExplorationSchema.FavoriteAccountIndex);
        Assert.False(byAccount.Unique);
        Assert.Equal(
            [
                MongoExplorationSchema.FavoriteAccountField,
                MongoExplorationSchema.FavoriteCreatedAtField
            ],
            byAccount.Fields.Select(field => field.Name));
        Assert.Equal(-1, byAccount.Fields[1].Direction);
    }

    [Fact]
    public void M10_DebeCrearRentasYPagosConSusIndices()
    {
        var migration = MongoRentalsMigration.Definition;

        Assert.Equal(2, migration.Collections.Count);

        var rentPeriods = Assert.Single(
            migration.Collections,
            collection => collection.Name == MongoRentalsSchema.RentPeriodsCollection);
        var uniqueIndex = Assert.Single(rentPeriods.Indexes);
        Assert.Equal(MongoRentalsSchema.RentPeriodUniqueIndex, uniqueIndex.Name);
        Assert.True(uniqueIndex.Unique);
        Assert.Equal(
            [MongoRentalsSchema.RentPeriodUnitField, MongoRentalsSchema.RentPeriodPeriodoField],
            uniqueIndex.Fields.Select(field => field.Name));

        var payments = Assert.Single(
            migration.Collections,
            collection => collection.Name == MongoRentalsSchema.PaymentsCollection);
        var paymentIndex = Assert.Single(payments.Indexes);
        Assert.Equal(MongoRentalsSchema.PaymentPeriodTenantIndex, paymentIndex.Name);
        Assert.False(paymentIndex.Unique);
        Assert.Equal(
            [MongoRentalsSchema.PaymentRentPeriodField, MongoRentalsSchema.PaymentTenantField],
            paymentIndex.Fields.Select(field => field.Name));
    }

    [Fact]
    public void M12_DebeCrearNotificacionesConSuIndice()
    {
        var migration = MongoNotificationsMigration.Definition;

        var collection = Assert.Single(migration.Collections);
        Assert.Equal(MongoNotificationsSchema.NotificationsCollection, collection.Name);
        var index = Assert.Single(collection.Indexes);
        Assert.Equal(MongoNotificationsSchema.RecipientIndex, index.Name);
        Assert.False(index.Unique);
        Assert.Equal(
            [MongoNotificationsSchema.RecipientField],
            index.Fields.Select(field => field.Name));
    }

    [Fact]
    public void Versiones_Duplicadas_DebenRechazarse()
    {
        var first = CreateMigration("M01-001", "cuentas");
        var second = CreateMigration("M01-001", "sesiones_refresh");

        Assert.Throws<ArgumentException>(() => new MongoMigrationRegistry([first, second]));
    }

    [Fact]
    public void Coleccion_CompartidaEntreMigraciones_DebeRechazarse()
    {
        var first = CreateMigration("M01-001", "cuentas");
        var second = CreateMigration("M02-001", "cuentas");

        Assert.Throws<ArgumentException>(() => new MongoMigrationRegistry([first, second]));
    }

    [Fact]
    public void ColeccionDelLedger_DebeEstarReservada()
    {
        var migration = CreateMigration("M02-001", MongoIdentitySchema.MigrationLedgerCollection);

        Assert.Throws<ArgumentException>(() => new MongoMigrationRegistry([migration]));
    }

    [Fact]
    public void Registro_DebeOrdenarPorVersionYNoPorEntrada()
    {
        var later = CreateMigration("M02-001", "coleccion_b");
        var earlier = CreateMigration("M01-001", "coleccion_a");

        var registry = new MongoMigrationRegistry([later, earlier]);

        Assert.Equal(["M01-001", "M02-001"], registry.Migrations.Select(item => item.Version));
        Assert.Equal("M01-001", registry.GetUpTo("M02-001")[0].Version);
    }

    [Fact]
    public void Checksum_Divergente_DebeDetectarse()
    {
        var registry = new MongoMigrationRegistry([CreateMigration("M01-001", "cuentas")]);

        Assert.Throws<MongoMigrationDivergenceException>(() =>
            registry.EnsureLedgerChecksum("M01-001", "00"));
    }

    [Fact]
    public void Version_NoRegistrada_DebeRechazarse()
    {
        Assert.Throws<MongoMigrationRegistryException>(() =>
            MongoMigrationRegistry.Default.GetRequired("M99-001"));
    }

    private static MongoMigrationDefinition CreateMigration(string version, string collectionName) =>
        new(
            version,
            $"Migración {version}",
            "Prueba",
            [
                new MongoCollectionDefinition(
                    collectionName,
                    [
                        new MongoIndexDefinition(
                            $"ix_{collectionName}",
                            [new MongoIndexField("_id", 1)])
                    ])
            ]);
}
