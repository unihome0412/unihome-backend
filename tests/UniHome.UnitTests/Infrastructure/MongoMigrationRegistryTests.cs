using MongoDB.Bson;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
using UniHome.Infrastructure.Persistence.Mongo.PasswordResets;
using UniHome.Infrastructure.Persistence.Mongo.Publications;
using UniHome.Infrastructure.Persistence.Mongo.Universities;

namespace UniHome.UnitTests.Infrastructure;

public sealed class MongoMigrationRegistryTests
{
    [Fact]
    public void RegistroPorDefecto_DebeContenerM01M01_002M03YM05EnOrden()
    {
        var registry = MongoMigrationRegistry.Default;

        Assert.Equal(
            [
                MongoIdentityMigration.Version,
                MongoPasswordResetMigration.Version,
                MongoUniversityMigration.Version,
                MongoPublicationMigration.Version
            ],
            registry.Migrations.Select(migration => migration.Version));
        Assert.Equal(MongoIdentityMigration.Definition.Checksum, registry.Migrations[0].Checksum);
        Assert.Equal(MongoPasswordResetMigration.Definition.Checksum, registry.Migrations[1].Checksum);
        Assert.Equal(MongoUniversityMigration.Definition.Checksum, registry.Migrations[2].Checksum);
        Assert.Equal(MongoPublicationMigration.Definition.Checksum, registry.Migrations[3].Checksum);
        Assert.Contains(MongoIdentitySchema.AccountsCollection, registry.ManagedCollections);
        Assert.Contains(MongoIdentitySchema.AccountRecoveryCollection, registry.ManagedCollections);
        Assert.Contains(MongoUniversitySchema.Collection, registry.ManagedCollections);
        Assert.Contains(MongoPublicationSchema.PropertiesCollection, registry.ManagedCollections);
        Assert.Contains(MongoPublicationSchema.UnitsCollection, registry.ManagedCollections);
        Assert.Contains(MongoPublicationSchema.TermsCollection, registry.ManagedCollections);
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
