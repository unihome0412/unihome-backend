using MongoDB.Bson;

namespace UniHome.Infrastructure.Persistence.Mongo.Migrations;

public enum MongoPromotionState
{
    Empty,
    NotStarted,
    PartiallyApplied,
    Applied,
    Divergent
}

public sealed record MongoCollectionSnapshot(
    string Name,
    bool HasValidator,
    string? ValidationLevel,
    string? ValidationAction,
    BsonDocument? Validator,
    IReadOnlyList<BsonDocument> Indexes,
    bool HasDocuments);

public sealed record MongoLedgerRecord(
    string Version,
    string? Checksum,
    DateTime? AppliedAtUtc);

public sealed record MongoPromotionSnapshot(
    string DatabaseName,
    IReadOnlyList<MongoCollectionSnapshot> Collections,
    IReadOnlyList<MongoLedgerRecord> Ledger);

public sealed record MongoPromotionPreflight(
    string DatabaseName,
    string Version,
    string Checksum,
    MongoPromotionState State,
    bool IsTargetEmpty,
    IReadOnlyList<MongoMigrationWrite> PendingWrites,
    IReadOnlyList<string> Issues)
{
    public bool RequiresWrites => PendingWrites.Count > 0;

    public bool CanPromote =>
        State is not MongoPromotionState.Divergent &&
        Issues.Count == 0;

    public bool IsNoOp =>
        State == MongoPromotionState.Applied &&
        CanPromote &&
        !RequiresWrites;
}

public sealed record MongoPromotionLockHandle(
    string OwnerId,
    string DatabaseName,
    string Version,
    DateTime ExpiresAtUtc,
    long FencingToken,
    bool CreatedLedgerCollection = false);

public sealed record MongoPromotionAudit(
    string ApprovalReference,
    string AuthorizedDatabase,
    string Version,
    string Checksum,
    string OwnerId);

public sealed record MongoPromotionResult(
    MongoPromotionPreflight InitialPreflight,
    MongoPromotionPreflight FinalPreflight,
    int WritesApplied,
    MongoMigrationVerification Verification)
{
    public bool IsNoOp => WritesApplied == 0;
}

public sealed class MongoPromotionPreflightException : InvalidOperationException
{
    public MongoPromotionPreflightException(string message)
        : base(message)
    {
    }
}

public sealed class MongoPromotionLockException : InvalidOperationException
{
    public MongoPromotionLockException(string message)
        : base(message)
    {
    }
}

public sealed class MongoPromotionVerificationException : InvalidOperationException
{
    public MongoPromotionVerificationException(string message)
        : base(message)
    {
    }
}

internal interface IMongoPromotionStore
{
    Task<MongoPromotionSnapshot> ReadSnapshotAsync(
        MongoMigrationDefinition migration,
        CancellationToken cancellationToken = default);

    Task<MongoMigrationPlan> PlanAsync(
        MongoMigrationDefinition migration,
        CancellationToken cancellationToken = default);

    Task ApplyAsync(
        MongoMigrationDefinition migration,
        MongoPromotionAudit audit,
        Func<CancellationToken, Task> ensureLockHeld,
        CancellationToken cancellationToken = default);

    Task<MongoMigrationVerification> VerifyAsync(
        MongoMigrationDefinition migration,
        CancellationToken cancellationToken = default);

    Task<MongoPromotionLockHandle?> TryAcquireLockAsync(
        string databaseName,
        string version,
        TimeSpan lease,
        CancellationToken cancellationToken = default);

    Task<bool> RenewLockAsync(
        MongoPromotionLockHandle handle,
        TimeSpan lease,
        CancellationToken cancellationToken = default);

    Task<bool> IsLockHeldAsync(
        MongoPromotionLockHandle handle,
        CancellationToken cancellationToken = default);

    Task ReleaseLockAsync(
        MongoPromotionLockHandle handle,
        CancellationToken cancellationToken = default);
}
