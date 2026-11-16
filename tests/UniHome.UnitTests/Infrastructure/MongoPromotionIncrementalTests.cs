using MongoDB.Bson;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
using UniHome.Infrastructure.Persistence.Mongo.PasswordResets;
using UniHome.Infrastructure.Persistence.Mongo.Publications;
using UniHome.Infrastructure.Persistence.Mongo.Universities;

namespace UniHome.UnitTests.Infrastructure;

/// <summary>
/// Pruebas del preflight incremental usado para migrar la base operativa de
/// verificación: exige las migraciones anteriores y permite datos en ellas, pero
/// mantiene vacía la migración seleccionada.
/// </summary>
public sealed class MongoPromotionIncrementalTests
{
    private static readonly MongoMigrationRegistry Registry = MongoMigrationRegistry.Default;

    [Fact]
    public void M01_002_EnVerificacion_ConM01Aplicada_DebeSerPromovible()
    {
        var snapshot = Snapshot(
            MongoPromotionGuard.VerificationTargetDatabaseName,
            [MongoIdentityMigration.Definition],
            hasDocumentsInPrevious: true);
        var plan = PlanFor(
            MongoPromotionGuard.VerificationTargetDatabaseName,
            MongoPasswordResetMigration.Definition);

        var preflight = Evaluate(snapshot, MongoPasswordResetMigration.Definition, plan);

        Assert.True(preflight.CanPromote);
        Assert.Equal(MongoPromotionState.PartiallyApplied, preflight.State);
        Assert.Equal(5, preflight.PendingWrites.Count);
        Assert.Empty(preflight.Issues);
    }

    [Fact]
    public void M03_SinM01_002_DebeRechazarseAunqueM01EsteAplicada()
    {
        var snapshot = Snapshot(
            MongoPromotionGuard.VerificationTargetDatabaseName,
            [MongoIdentityMigration.Definition],
            hasDocumentsInPrevious: true);
        var plan = PlanFor(
            MongoPromotionGuard.VerificationTargetDatabaseName,
            MongoUniversityMigration.Definition);

        var preflight = Evaluate(snapshot, MongoUniversityMigration.Definition, plan);

        Assert.False(preflight.CanPromote);
        Assert.Equal(MongoPromotionState.Divergent, preflight.State);
        Assert.Contains(preflight.Issues, issue =>
            issue.Contains(MongoPasswordResetMigration.Version, StringComparison.Ordinal));
    }

    [Fact]
    public void M03_ConM01_002Aplicada_DebeSerPromovibleSinExigirVaciarM01()
    {
        var snapshot = Snapshot(
            MongoPromotionGuard.VerificationTargetDatabaseName,
            [MongoIdentityMigration.Definition, MongoPasswordResetMigration.Definition],
            hasDocumentsInPrevious: true);
        var plan = PlanFor(
            MongoPromotionGuard.VerificationTargetDatabaseName,
            MongoUniversityMigration.Definition);

        var preflight = Evaluate(snapshot, MongoUniversityMigration.Definition, plan);

        Assert.True(preflight.CanPromote);
        Assert.Empty(preflight.Issues);
        Assert.Equal(3, preflight.PendingWrites.Count);
    }

    [Fact]
    public void M05_ConM01YM03Aplicadas_DebeSerPromovible()
    {
        var snapshot = Snapshot(
            MongoPromotionGuard.VerificationTargetDatabaseName,
            [
                MongoIdentityMigration.Definition,
                MongoPasswordResetMigration.Definition,
                MongoUniversityMigration.Definition
            ],
            hasDocumentsInPrevious: true);
        var plan = PlanFor(
            MongoPromotionGuard.VerificationTargetDatabaseName,
            MongoPublicationMigration.Definition);

        var preflight = Evaluate(snapshot, MongoPublicationMigration.Definition, plan);

        Assert.True(preflight.CanPromote);
        Assert.Equal(10, preflight.PendingWrites.Count);
    }

    [Fact]
    public void M03_ConColeccionExterna_DebeSeguirSiendoDivergente()
    {
        var snapshot = Snapshot(
            MongoPromotionGuard.VerificationTargetDatabaseName,
            [MongoIdentityMigration.Definition, MongoPasswordResetMigration.Definition]);
        snapshot = snapshot with
        {
            Collections =
            [
                .. snapshot.Collections,
                new MongoCollectionSnapshot("coleccion_externa", false, null, null, null, [], false)
            ]
        };
        var plan = PlanFor(
            MongoPromotionGuard.VerificationTargetDatabaseName,
            MongoUniversityMigration.Definition);

        var preflight = Evaluate(snapshot, MongoUniversityMigration.Definition, plan);

        Assert.False(preflight.CanPromote);
        Assert.Contains(preflight.Issues, issue =>
            issue.Contains("no administrada", StringComparison.Ordinal));
    }

    private static MongoPromotionPreflight Evaluate(
        MongoPromotionSnapshot snapshot,
        MongoMigrationDefinition migration,
        MongoMigrationPlan plan) =>
        MongoPromotionPreflightEvaluator.Evaluate(snapshot, migration, Registry, plan);

    private static MongoPromotionSnapshot Snapshot(
        string databaseName,
        IReadOnlyList<MongoMigrationDefinition> appliedMigrations,
        bool hasDocumentsInPrevious = false)
    {
        var collections = new List<MongoCollectionSnapshot>();
        var ledger = new List<MongoLedgerRecord>();

        foreach (var migration in appliedMigrations)
        {
            foreach (var definition in migration.Collections)
            {
                collections.Add(CollectionSnapshot(definition, hasDocumentsInPrevious));
            }

            ledger.Add(new MongoLedgerRecord(
                migration.Version,
                migration.Checksum,
                DateTime.UtcNow));
        }

        collections.Add(new MongoCollectionSnapshot(
            MongoIdentitySchema.MigrationLedgerCollection,
            false,
            null,
            null,
            null,
            [],
            true));

        return new MongoPromotionSnapshot(databaseName, collections, ledger);
    }

    private static MongoCollectionSnapshot CollectionSnapshot(
        MongoCollectionDefinition definition,
        bool hasDocuments)
    {
        var indexes = definition.Indexes.Select(ToIndexDocument).ToList();
        indexes.Insert(0, BsonDocument.Parse("{ name: '_id_', key: { _id: 1 } }"));

        return new MongoCollectionSnapshot(
            definition.Name,
            definition.Validator is not null,
            definition.Validator?.ValidationLevel,
            definition.Validator?.ValidationAction,
            definition.Validator?.Filter,
            indexes,
            hasDocuments);
    }

    private static MongoMigrationPlan PlanFor(
        string databaseName,
        MongoMigrationDefinition migration)
    {
        var writes = new List<MongoMigrationWrite>();

        foreach (var definition in migration.Collections)
        {
            writes.Add(new MongoMigrationWrite(
                MongoMigrationWriteKind.CreateCollection,
                definition.Name,
                definition.Name));
            writes.AddRange(definition.Indexes.Select(index => new MongoMigrationWrite(
                MongoMigrationWriteKind.CreateIndex,
                definition.Name,
                index.Name)));
        }

        writes.Add(new MongoMigrationWrite(
            MongoMigrationWriteKind.RecordMigration,
            MongoIdentitySchema.MigrationLedgerCollection,
            migration.Version));

        return new MongoMigrationPlan(databaseName, migration.Version, writes);
    }

    private static BsonDocument ToIndexDocument(MongoIndexDefinition index)
    {
        var document = new BsonDocument
        {
            ["name"] = index.Name,
            ["key"] = index.ToKeyDocument()
        };

        if (index.Unique)
        {
            document["unique"] = true;
        }

        if (index.ExpireAfterSeconds.HasValue)
        {
            document["expireAfterSeconds"] = index.ExpireAfterSeconds.Value;
        }

        return document;
    }
}
