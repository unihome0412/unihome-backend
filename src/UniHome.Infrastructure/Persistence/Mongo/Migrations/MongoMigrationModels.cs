namespace UniHome.Infrastructure.Persistence.Mongo.Migrations;

public enum MongoMigrationWriteKind
{
    CreateCollection,
    CreateIndex,
    RecordMigration
}

public sealed record MongoMigrationWrite(
    MongoMigrationWriteKind Kind,
    string Collection,
    string Name,
    string? Detail = null);

public sealed record MongoMigrationPlan(
    string DatabaseName,
    string Version,
    IReadOnlyList<MongoMigrationWrite> Writes)
{
    public bool RequiresWrites => Writes.Count > 0;
}

public sealed record MongoIndexAudit(
    string Name,
    string Key,
    bool Unique,
    long? ExpireAfterSeconds);

public sealed record MongoCollectionAudit(
    string Name,
    bool HasValidator,
    string? ValidationLevel,
    string? ValidationAction,
    IReadOnlyList<MongoIndexAudit> Indexes,
    long EstimatedDocumentCount,
    string? ValidatorJson = null);

public sealed record MongoSchemaAudit(
    string DatabaseName,
    IReadOnlyList<MongoCollectionAudit> Collections);

public sealed record MongoMigrationVerification(
    string DatabaseName,
    string Version,
    bool Applied,
    bool ChecksumMatches,
    bool IndexesMatch,
    IReadOnlyList<MongoMigrationWrite> PendingWrites,
    IReadOnlyList<MongoCollectionAudit> Collections,
    IReadOnlyList<string> Issues)
{
    public bool IsValid =>
        Applied &&
        ChecksumMatches &&
        IndexesMatch &&
        PendingWrites.Count == 0 &&
        Issues.Count == 0;
}

public sealed class MongoMigrationDivergenceException : Exception
{
    public MongoMigrationDivergenceException(string message)
        : base(message)
    {
    }
}
