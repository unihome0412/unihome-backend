using MongoDB.Bson;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
using UniHome.Infrastructure.Persistence.Mongo.Notifications;
using UniHome.Infrastructure.Persistence.Mongo.PasswordResets;
using UniHome.Infrastructure.Persistence.Mongo.Publications;
using UniHome.Infrastructure.Persistence.Mongo.Reparaciones;
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

    /// <summary>
    /// M11-001 (reparaciones) ordena alfabéticamente antes que M12-001
    /// (notificaciones), pero M12-001 ya estaba promovida en producción desde
    /// Sprint 7, antes de que M11-001 existiera: el número de módulo no refleja
    /// el orden real de despliegue. El preflight debe tolerar esa migración
    /// "posterior" ya aplicada y válida, sin tratar su colección como ajena.
    /// </summary>
    [Fact]
    public void M11_ConM12YaAplicadaYValida_DebeSerPromovible()
    {
        var applied = PriorTo(MongoReparacionesMigration.Version)
            .Append(MongoNotificationsMigration.Definition)
            .ToArray();
        var snapshot = Snapshot(
            MongoPromotionGuard.VerificationTargetDatabaseName,
            applied,
            hasDocumentsInPrevious: true);
        var plan = PlanFor(
            MongoPromotionGuard.VerificationTargetDatabaseName,
            MongoReparacionesMigration.Definition);

        var preflight = Evaluate(snapshot, MongoReparacionesMigration.Definition, plan);

        Assert.True(preflight.CanPromote);
        Assert.Empty(preflight.Issues);
        Assert.Equal(MongoPromotionState.PartiallyApplied, preflight.State);
    }

    /// <summary>
    /// Una colección genuinamente desconocida sigue rechazándose aunque el
    /// preflight ahora tolere migraciones posteriores ya aplicadas: la
    /// tolerancia es exclusiva de colecciones que pertenecen a una migración
    /// registrada, nunca a cualquier colección extra.
    /// </summary>
    [Fact]
    public void M11_ConColeccionDesconocida_DebeSeguirSiendoDivergente()
    {
        var applied = PriorTo(MongoReparacionesMigration.Version)
            .Append(MongoNotificationsMigration.Definition)
            .ToArray();
        var snapshot = Snapshot(
            MongoPromotionGuard.VerificationTargetDatabaseName,
            applied,
            hasDocumentsInPrevious: true);
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
            MongoReparacionesMigration.Definition);

        var preflight = Evaluate(snapshot, MongoReparacionesMigration.Definition, plan);

        Assert.False(preflight.CanPromote);
        Assert.Contains(preflight.Issues, issue =>
            issue.Contains("no administrada", StringComparison.Ordinal));
    }

    /// <summary>
    /// Si la migración posterior (M12-001) todavía no está aplicada en esta base,
    /// su colección no se tolera: debe seguir rechazándose como no administrada,
    /// igual que antes del cambio.
    /// </summary>
    [Fact]
    public void M11_ConColeccionDeM12SinEstarAplicadaEnEstaBase_DebeRechazarse()
    {
        var applied = PriorTo(MongoReparacionesMigration.Version);
        var snapshot = Snapshot(
            MongoPromotionGuard.VerificationTargetDatabaseName,
            applied,
            hasDocumentsInPrevious: true);
        snapshot = snapshot with
        {
            Collections =
            [
                .. snapshot.Collections,
                CollectionSnapshot(MongoNotificationsMigration.Definition.Collections[0], hasDocuments: false)
            ]
        };
        var plan = PlanFor(
            MongoPromotionGuard.VerificationTargetDatabaseName,
            MongoReparacionesMigration.Definition);

        var preflight = Evaluate(snapshot, MongoReparacionesMigration.Definition, plan);

        Assert.False(preflight.CanPromote);
        Assert.Contains(preflight.Issues, issue =>
            issue.Contains("no administrada", StringComparison.Ordinal) &&
            issue.Contains(MongoNotificationsMigration.Definition.Collections[0].Name, StringComparison.Ordinal));
    }

    private static MongoMigrationDefinition[] PriorTo(string version) =>
        Registry.GetUpTo(version)
            .Where(item => !string.Equals(item.Version, version, StringComparison.Ordinal))
            .ToArray();

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
