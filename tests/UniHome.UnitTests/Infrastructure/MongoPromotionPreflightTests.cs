using MongoDB.Bson;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.UnitTests.Infrastructure;

public sealed class MongoPromotionPreflightTests
{
    private static readonly MongoMigrationDefinition M01 = MongoIdentityMigration.Definition;
    private static readonly MongoMigrationRegistry Registry = MongoMigrationRegistry.Default;

    [Fact]
    public void BaseVacia_ConPlanM01_DebeSerPromovibleYReportarOchoEscrituras()
    {
        var plan = EmptyPlan();

        var preflight = Evaluate(new MongoPromotionSnapshot(
            MongoPromotionGuard.TargetDatabaseName,
            [],
            []), plan);

        Assert.Equal(MongoPromotionState.Empty, preflight.State);
        Assert.True(preflight.IsTargetEmpty);
        Assert.True(preflight.CanPromote);
        Assert.True(preflight.RequiresWrites);
        Assert.Equal(8, preflight.PendingWrites.Count);
        Assert.Empty(preflight.Issues);
    }

    [Fact]
    public void EjecucionParcial_SinLedger_DebeIdentificarseYCompletarse()
    {
        var snapshot = new MongoPromotionSnapshot(
            MongoPromotionGuard.TargetDatabaseName,
            [Snapshot(M01.Collections[0]), Snapshot(M01.Collections[1])],
            []);

        var preflight = Evaluate(snapshot, RecordOnlyPlan());

        Assert.Equal(MongoPromotionState.PartiallyApplied, preflight.State);
        Assert.True(preflight.CanPromote);
        Assert.False(preflight.IsTargetEmpty);
        Assert.Single(preflight.PendingWrites);
    }

    [Fact]
    public void MigracionAplicada_ConChecksumEIndices_DebeSerNoOp()
    {
        var snapshot = AppliedSnapshot();

        var preflight = Evaluate(snapshot, NoWritesPlan());

        Assert.Equal(MongoPromotionState.Applied, preflight.State);
        Assert.True(preflight.CanPromote);
        Assert.True(preflight.IsNoOp);
        Assert.False(preflight.RequiresWrites);
    }

    [Fact]
    public void ChecksumDiferente_DebeSerDivergencia()
    {
        var snapshot = AppliedSnapshot() with
        {
            Ledger = [new MongoLedgerRecord(M01.Version, "00", DateTime.UtcNow)]
        };

        var preflight = Evaluate(snapshot, NoWritesPlan());

        Assert.Equal(MongoPromotionState.Divergent, preflight.State);
        Assert.False(preflight.CanPromote);
        Assert.NotEmpty(preflight.Issues);
    }

    [Fact]
    public void IndiceIncompatible_DebeSerDivergencia()
    {
        var accounts = Snapshot(M01.Collections[0]) with
        {
            Indexes =
            [
                BsonDocument.Parse(
                    "{ name: 'ux_cuentas_correo_normalizado', key: { correoNormalizado: 1 }, unique: false }")
            ]
        };
        var snapshot = new MongoPromotionSnapshot(
            MongoPromotionGuard.TargetDatabaseName,
            [accounts, Snapshot(M01.Collections[1])],
            []);

        var preflight = Evaluate(snapshot, RecordOnlyPlan());

        Assert.Equal(MongoPromotionState.Divergent, preflight.State);
        Assert.Contains(preflight.Issues, issue => issue.Contains("divergente", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ColeccionNoAdministrada_DebeSerDivergencia()
    {
        var snapshot = new MongoPromotionSnapshot(
            MongoPromotionGuard.TargetDatabaseName,
            [Snapshot(M01.Collections[0]), Snapshot(M01.Collections[1]),
             new MongoCollectionSnapshot("coleccion_externa", false, null, null, null, [], false)],
            []);

        var preflight = Evaluate(snapshot, RecordOnlyPlan());

        Assert.Equal(MongoPromotionState.Divergent, preflight.State);
        Assert.Contains(preflight.Issues, issue => issue.Contains("no administrada", StringComparison.Ordinal));
    }

    [Fact]
    public void ColeccionConDocumentos_DebeRechazarseParaNoMezclarDatos()
    {
        var accounts = Snapshot(M01.Collections[0]) with { HasDocuments = true };
        var snapshot = new MongoPromotionSnapshot(
            MongoPromotionGuard.TargetDatabaseName,
            [accounts],
            []);

        var preflight = Evaluate(snapshot, EmptyPlan());

        Assert.Equal(MongoPromotionState.Divergent, preflight.State);
        Assert.Contains(preflight.Issues, issue => issue.Contains("contiene documentos", StringComparison.Ordinal));
    }

    [Fact]
    public void LedgerConIndiceAdicional_DebeSerDivergencia()
    {
        var snapshot = AppliedSnapshot();
        var ledger = snapshot.Collections.Single(collection =>
            collection.Name == MongoIdentitySchema.MigrationLedgerCollection);
        var ledgerWithIndex = ledger with
        {
            Indexes =
            [
                BsonDocument.Parse("{ name: '_id_', key: { _id: 1 } }"),
                BsonDocument.Parse("{ name: 'ix_ledger_extra', key: { version: 1 } }")
            ]
        };

        var snapshotWithIndex = snapshot with
        {
            Collections = snapshot.Collections
                .Select(collection => ReferenceEquals(collection, ledger) ? ledgerWithIndex : collection)
                .ToList()
        };

        var preflight = Evaluate(snapshotWithIndex, NoWritesPlan());

        Assert.Equal(MongoPromotionState.Divergent, preflight.State);
        Assert.Contains(preflight.Issues, issue => issue.Contains("ledger", StringComparison.Ordinal));
    }

    [Fact]
    public void VersionDeLedgerDesconocida_DebeSerDivergencia()
    {
        var snapshot = AppliedSnapshot() with
        {
            Ledger = [.. AppliedSnapshot().Ledger, new MongoLedgerRecord("M99-001", "00", DateTime.UtcNow)]
        };

        var preflight = Evaluate(snapshot, NoWritesPlan());

        Assert.Equal(MongoPromotionState.Divergent, preflight.State);
        Assert.Contains(preflight.Issues, issue => issue.Contains("no registrada", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidadorNoPrevisto_DebeSerDivergencia()
    {
        var accounts = Snapshot(M01.Collections[0]) with
        {
            HasValidator = true,
            Validator = BsonDocument.Parse("{ required: ['correoNormalizado'] }"),
            ValidationLevel = MongoValidatorDefinition.StrictLevel,
            ValidationAction = MongoValidatorDefinition.ErrorAction
        };
        var snapshot = new MongoPromotionSnapshot(
            MongoPromotionGuard.TargetDatabaseName,
            [accounts, Snapshot(M01.Collections[1])],
            []);

        var preflight = Evaluate(snapshot, RecordOnlyPlan());

        Assert.Equal(MongoPromotionState.Divergent, preflight.State);
        Assert.Contains(preflight.Issues, issue => issue.Contains("Validador no previsto", StringComparison.Ordinal));
    }

    [Fact]
    public void SoloLedgerVacio_DebeSerNotStartedPeroPromovible()
    {
        var snapshot = new MongoPromotionSnapshot(
            MongoPromotionGuard.TargetDatabaseName,
            [new MongoCollectionSnapshot(
                MongoIdentitySchema.MigrationLedgerCollection,
                false,
                null,
                null,
                null,
                [],
                false)],
            []);

        var preflight = Evaluate(snapshot, RecordOnlyPlan());

        Assert.Equal(MongoPromotionState.NotStarted, preflight.State);
        Assert.False(preflight.IsTargetEmpty);
        Assert.True(preflight.CanPromote);
    }

    [Fact]
    public void PlanDeOtroDestino_DebeSerDivergencia()
    {
        var plan = EmptyPlan() with { DatabaseName = "unihome_modelo_pruebas" };

        var preflight = Evaluate(new MongoPromotionSnapshot(
            MongoPromotionGuard.TargetDatabaseName,
            [],
            []), plan);

        Assert.Equal(MongoPromotionState.Divergent, preflight.State);
        Assert.Contains(preflight.Issues, issue => issue.Contains("destino", StringComparison.Ordinal));
    }

    [Fact]
    public void PlanConEscrituraFueraDelContrato_DebeSerDivergencia()
    {
        var plan = new MongoMigrationPlan(
            MongoPromotionGuard.TargetDatabaseName,
            M01.Version,
            [
                new MongoMigrationWrite(
                    MongoMigrationWriteKind.CreateCollection,
                    "coleccion_externa",
                    "coleccion_externa"),
                new MongoMigrationWrite(
                    MongoMigrationWriteKind.RecordMigration,
                    MongoIdentitySchema.MigrationLedgerCollection,
                    M01.Version)
            ]);

        var preflight = Evaluate(new MongoPromotionSnapshot(
            MongoPromotionGuard.TargetDatabaseName,
            [],
            []), plan);

        Assert.Equal(MongoPromotionState.Divergent, preflight.State);
        Assert.Contains(preflight.Issues, issue => issue.Contains("fuera", StringComparison.Ordinal));
    }

    private static MongoPromotionPreflight Evaluate(
        MongoPromotionSnapshot snapshot,
        MongoMigrationPlan plan) =>
        MongoPromotionPreflightEvaluator.Evaluate(snapshot, M01, Registry, plan);

    private static MongoMigrationPlan EmptyPlan()
    {
        var writes = new List<MongoMigrationWrite>();

        foreach (var collection in M01.Collections)
        {
            writes.Add(new MongoMigrationWrite(
                MongoMigrationWriteKind.CreateCollection,
                collection.Name,
                collection.Name));
            writes.AddRange(collection.Indexes.Select(index => new MongoMigrationWrite(
                MongoMigrationWriteKind.CreateIndex,
                collection.Name,
                index.Name)));
        }

        writes.Add(new MongoMigrationWrite(
            MongoMigrationWriteKind.CreateCollection,
            MongoIdentitySchema.MigrationLedgerCollection,
            MongoIdentitySchema.MigrationLedgerCollection));
        writes.Add(new MongoMigrationWrite(
            MongoMigrationWriteKind.RecordMigration,
            MongoIdentitySchema.MigrationLedgerCollection,
            M01.Version));

        return new MongoMigrationPlan(MongoPromotionGuard.TargetDatabaseName, M01.Version, writes);
    }

    private static MongoMigrationPlan RecordOnlyPlan() =>
        new(
            MongoPromotionGuard.TargetDatabaseName,
            M01.Version,
            [
                new MongoMigrationWrite(
                    MongoMigrationWriteKind.RecordMigration,
                    MongoIdentitySchema.MigrationLedgerCollection,
                    M01.Version)
            ]);

    private static MongoMigrationPlan NoWritesPlan() =>
        new(MongoPromotionGuard.TargetDatabaseName, M01.Version, []);

    private static MongoPromotionSnapshot AppliedSnapshot()
    {
        var collections = M01.Collections.Select(Snapshot).ToList();
        collections.Add(new MongoCollectionSnapshot(
            MongoIdentitySchema.MigrationLedgerCollection,
            false,
            null,
            null,
            null,
            [],
            true));

        return new MongoPromotionSnapshot(
            MongoPromotionGuard.TargetDatabaseName,
            collections,
            [new MongoLedgerRecord(M01.Version, M01.Checksum, DateTime.UtcNow)]);
    }

    private static MongoCollectionSnapshot Snapshot(MongoCollectionDefinition definition)
    {
        var indexes = definition.Indexes.Select(ToIndexDocument).ToList();
        indexes.Insert(0, BsonDocument.Parse("{ name: '_id_', key: { _id: 1 } }"));

        return new MongoCollectionSnapshot(
            definition.Name,
            false,
            null,
            null,
            null,
            indexes,
            false);
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
