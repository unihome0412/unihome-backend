using MongoDB.Bson;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.UnitTests.Infrastructure;

public sealed class MongoPromotionServiceTests
{
    private static readonly MongoMigrationDefinition M01 = MongoIdentityMigration.Definition;

    [Fact]
    public async Task DestinoNoAutorizado_DebeFallarAntesDeLeerElStore()
    {
        var store = new FakePromotionStore(EmptySnapshot());
        var service = new MongoPromotionService(store);
        var authorization = new MongoPromotionAuthorization(
            "unihome_modelo_pruebas",
            "unihome_modelo_pruebas",
            "APR-001",
            MongoPromotionGuard.TargetDatabaseName);

        await Assert.ThrowsAsync<MongoPromotionAuthorizationException>(
            () => service.PromoteAsync(authorization));

        Assert.Equal(0, store.SnapshotReads);
    }

    [Fact]
    public async Task ConfirmacionIncorrecta_DebeFallarAntesDeLeerElStore()
    {
        var store = new FakePromotionStore(EmptySnapshot());
        var service = new MongoPromotionService(store);
        var authorization = Authorization() with { ConfirmationDatabase = "unihome_modelo_pruebas" };

        await Assert.ThrowsAsync<MongoPromotionAuthorizationException>(
            () => service.PromoteAsync(authorization));

        Assert.Equal(0, store.SnapshotReads);
    }

    [Fact]
    public async Task BaseVacia_DebeAplicarYVerificar()
    {
        var store = new FakePromotionStore(EmptySnapshot());
        var service = new MongoPromotionService(store);

        var result = await service.PromoteAsync(Authorization());

        Assert.Equal(MongoPromotionState.Empty, result.InitialPreflight.State);
        Assert.Equal(8, result.InitialPreflight.PendingWrites.Count);
        Assert.Equal(1, store.ApplyCalls);
        Assert.Equal(8, result.WritesApplied);
        Assert.True(result.Verification.IsValid);
        Assert.True(store.ReleaseCalls > 0);
        Assert.NotNull(store.LastAudit);
        Assert.Equal("APR-001", store.LastAudit.ApprovalReference);
        Assert.Equal(MongoPromotionGuard.TargetDatabaseName, store.LastAudit.AuthorizedDatabase);
        Assert.Equal(M01.Checksum, store.LastAudit.Checksum);
        Assert.False(string.IsNullOrWhiteSpace(store.LastAudit.OwnerId));
    }

    [Fact]
    public async Task BaseVacia_CuandoElBloqueoCreaElLedger_DebeContarOchoEscrituras()
    {
        var store = new FakePromotionStore(EmptySnapshot())
        {
            SimulateLedgerCreatedByLock = true
        };
        var service = new MongoPromotionService(store);

        var result = await service.PromoteAsync(Authorization());

        Assert.Equal(8, result.InitialPreflight.PendingWrites.Count);
        Assert.Equal(7, result.FinalPreflight.PendingWrites.Count);
        Assert.Equal(8, result.WritesApplied);
        Assert.True(result.Verification.IsValid);
    }

    [Fact]
    public async Task MigracionAplicada_DebeSerNoOpSinAplicar()
    {
        var store = new FakePromotionStore(AppliedSnapshot());
        var service = new MongoPromotionService(store);

        var result = await service.PromoteAsync(Authorization());

        Assert.True(result.IsNoOp);
        Assert.Equal(0, store.ApplyCalls);
        Assert.Equal(1, store.LockAttempts);
        Assert.True(result.Verification.IsValid);
    }

    [Fact]
    public async Task DosPromocionesSimultaneas_SoloUnaDebeAplicar()
    {
        var store = new FakePromotionStore(EmptySnapshot()) { BlockApply = true };
        var service = new MongoPromotionService(store, lockLease: TimeSpan.FromMinutes(1));

        var first = service.PromoteAsync(Authorization());
        await store.ApplyStarted.Task;

        var second = service.PromoteAsync(Authorization());
        await Assert.ThrowsAsync<MongoPromotionLockException>(() => second);

        store.ReleaseApply.TrySetResult();
        var result = await first;

        Assert.Equal(1, store.ApplyCalls);
        Assert.Equal(2, store.LockAttempts);
        Assert.Equal(8, result.WritesApplied);
        Assert.True(result.Verification.IsValid);
    }

    [Fact]
    public async Task InterrupcionParcial_DebeRecuperarseEnLaSiguienteEjecucion()
    {
        var store = new FakePromotionStore(EmptySnapshot()) { FailFirstApply = true };
        var service = new MongoPromotionService(store);

        await Assert.ThrowsAsync<IOException>(() => service.PromoteAsync(Authorization()));
        Assert.Empty(store.Snapshot.Ledger);
        Assert.Equal(2, store.Snapshot.Collections.Count);

        var result = await service.PromoteAsync(Authorization());

        Assert.Equal(2, store.ApplyCalls);
        Assert.Equal(1, result.WritesApplied);
        Assert.Equal(MongoPromotionState.PartiallyApplied, result.InitialPreflight.State);
        Assert.True(result.Verification.IsValid);
    }

    [Fact]
    public async Task PerdidaDeBloqueo_DebeCancelarLaAplicacion()
    {
        var store = new FakePromotionStore(EmptySnapshot())
        {
            BlockApply = true,
            FailRenewal = true
        };
        var service = new MongoPromotionService(
            store,
            lockLease: TimeSpan.FromMilliseconds(300),
            heartbeatInterval: TimeSpan.FromMilliseconds(30));

        var promotion = service.PromoteAsync(Authorization());
        await store.ApplyStarted.Task;
        await store.RenewAttempted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => promotion);
        Assert.True(store.RenewCalls > 0);
        Assert.True(store.ReleaseCalls > 0);
    }

    [Fact]
    public async Task VerificacionInvalida_NoDebeDeclararExito()
    {
        var store = new FakePromotionStore(EmptySnapshot()) { InvalidateVerification = true };
        var service = new MongoPromotionService(store);

        await Assert.ThrowsAsync<MongoPromotionVerificationException>(
            () => service.PromoteAsync(Authorization()));
    }

    private static MongoPromotionAuthorization Authorization() =>
        new(
            MongoPromotionGuard.TargetDatabaseName,
            MongoPromotionGuard.TargetDatabaseName,
            "APR-001",
            MongoPromotionGuard.TargetDatabaseName);

    private static MongoPromotionSnapshot EmptySnapshot() =>
        new(MongoPromotionGuard.TargetDatabaseName, [], []);

    private static MongoPromotionSnapshot PartialSnapshot()
    {
        return new MongoPromotionSnapshot(
            MongoPromotionGuard.TargetDatabaseName,
            M01.Collections.Select(Snapshot).ToList(),
            []);
    }

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

    private static MongoMigrationPlan EmptyPlanWithoutLedgerCollection()
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

    private sealed class FakePromotionStore : IMongoPromotionStore
    {
        private readonly object _gate = new();
        private bool _locked;
        private string? _lockOwner;
        private long _fencingToken;

        public FakePromotionStore(MongoPromotionSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public MongoPromotionSnapshot Snapshot { get; set; }

        public int SnapshotReads { get; private set; }

        public int ApplyCalls { get; private set; }

        public int LockAttempts { get; private set; }

        public int ReleaseCalls { get; private set; }

        public int RenewCalls { get; private set; }

        public MongoPromotionAudit? LastAudit { get; private set; }

        public bool BlockApply { get; init; }

        public bool FailFirstApply { get; init; }

        public bool InvalidateVerification { get; init; }

        public bool SimulateLedgerCreatedByLock { get; init; }

        public bool FailRenewal { get; init; }

        private bool _ledgerCreatedByLock;

        public TaskCompletionSource ApplyStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseApply { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource RenewAttempted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<MongoPromotionSnapshot> ReadSnapshotAsync(
            MongoMigrationDefinition migration,
            CancellationToken cancellationToken = default)
        {
            SnapshotReads++;
            return Task.FromResult(Snapshot);
        }

        public Task<MongoMigrationPlan> PlanAsync(
            MongoMigrationDefinition migration,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PlanForSnapshot());
        }

        public async Task ApplyAsync(
            MongoMigrationDefinition migration,
            MongoPromotionAudit audit,
            Func<CancellationToken, Task> ensureLockHeld,
            CancellationToken cancellationToken = default)
        {
            ApplyCalls++;
            LastAudit = audit;
            await ensureLockHeld(cancellationToken).ConfigureAwait(false);

            if (BlockApply)
            {
                ApplyStarted.TrySetResult();
                await ReleaseApply.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            lock (_gate)
            {
                if (FailFirstApply && ApplyCalls == 1)
                {
                    Snapshot = PartialSnapshot();
                    throw new IOException("synthetic-interruption");
                }

                Snapshot = AppliedSnapshot();
            }
        }

        public Task<MongoMigrationVerification> VerifyAsync(
            MongoMigrationDefinition migration,
            CancellationToken cancellationToken = default)
        {
            var valid = !InvalidateVerification &&
                        Snapshot.Ledger.Any(record =>
                            string.Equals(record.Version, migration.Version, StringComparison.Ordinal) &&
                            string.Equals(record.Checksum, migration.Checksum, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(new MongoMigrationVerification(
                Snapshot.DatabaseName,
                migration.Version,
                valid,
                valid,
                valid,
                [],
                [],
                valid ? [] : ["synthetic-verification"]));
        }

        public Task<MongoPromotionLockHandle?> TryAcquireLockAsync(
            string databaseName,
            string version,
            TimeSpan lease,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                LockAttempts++;

                if (_locked)
                {
                    return Task.FromResult<MongoPromotionLockHandle?>(null);
                }

                _locked = true;
                _ledgerCreatedByLock = SimulateLedgerCreatedByLock;
                _lockOwner = Guid.NewGuid().ToString("N");
                _fencingToken++;
                return Task.FromResult<MongoPromotionLockHandle?>(new MongoPromotionLockHandle(
                    _lockOwner,
                    databaseName,
                    version,
                    DateTime.UtcNow.Add(lease),
                    _fencingToken,
                    _ledgerCreatedByLock));
            }
        }

        public Task<bool> RenewLockAsync(
            MongoPromotionLockHandle handle,
            TimeSpan lease,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                RenewCalls++;
                RenewAttempted.TrySetResult();
                var held = !FailRenewal &&
                           _locked &&
                           string.Equals(_lockOwner, handle.OwnerId, StringComparison.Ordinal) &&
                           handle.FencingToken == _fencingToken;
                return Task.FromResult(held);
            }
        }

        public Task<bool> IsLockHeldAsync(
            MongoPromotionLockHandle handle,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(
                    _locked &&
                    string.Equals(_lockOwner, handle.OwnerId, StringComparison.Ordinal) &&
                    handle.FencingToken == _fencingToken);
            }
        }

        public Task ReleaseLockAsync(
            MongoPromotionLockHandle handle,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                ReleaseCalls++;

                if (string.Equals(_lockOwner, handle.OwnerId, StringComparison.Ordinal) &&
                    handle.FencingToken == _fencingToken)
                {
                    _locked = false;
                    _lockOwner = null;
                }
            }

            return Task.CompletedTask;
        }

        private MongoMigrationPlan PlanForSnapshot()
        {
            if (Snapshot.Ledger.Any(record =>
                    string.Equals(record.Version, M01.Version, StringComparison.Ordinal)))
            {
                return NoWritesPlan();
            }

            if (Snapshot.Collections.Count == 0)
            {
                return _ledgerCreatedByLock ? EmptyPlanWithoutLedgerCollection() : EmptyPlan();
            }

            return RecordOnlyPlan();
        }
    }
}
