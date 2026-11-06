using MongoDB.Bson;
using MongoDB.Driver;

namespace UniHome.Infrastructure.Persistence.Mongo.Migrations;

/// <summary>
/// Adaptador de persistencia de la promoción. No imprime ni registra la conexión,
/// documentos, usuarios o secretos. Solo crea colecciones/índices, inserta el
/// ledger y administra el documento de bloqueo.
/// </summary>
internal sealed class MongoPromotionStore : IMongoPromotionStore
{
    internal const string PromotionLockId = "promotion-lock";
    private const string PromotionLockKind = "promotion";
    private const int NamespaceExistsCode = 48;

    private readonly IMongoDatabase _database;
    private readonly TimeProvider _timeProvider;

    public MongoPromotionStore(IMongoDatabase database, TimeProvider? timeProvider = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MongoPromotionSnapshot> ReadSnapshotAsync(
        MongoMigrationDefinition migration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(migration);

        var runner = new MongoMigrationRunner(_database, migration);
        var collectionInfos = await runner.GetCollectionInfosAsync(cancellationToken)
            .ConfigureAwait(false);
        var collections = new List<MongoCollectionSnapshot>(collectionInfos.Count);

        foreach (var collectionInfo in collectionInfos)
        {
            var name = GetString(collectionInfo, "name");

            if (string.IsNullOrWhiteSpace(name) || name.StartsWith("system.", StringComparison.Ordinal))
            {
                continue;
            }

            var options = MongoMigrationRunner.GetOptions(collectionInfo);
            var validator = options.TryGetValue("validator", out var validatorValue) &&
                            validatorValue.IsBsonDocument &&
                            validatorValue.AsBsonDocument.ElementCount > 0
                ? validatorValue.AsBsonDocument
                : null;
            var indexes = await runner.ReadIndexesAsync(name, cancellationToken)
                .ConfigureAwait(false);
            var hasDocuments = await runner.HasAnyDocumentAsync(name, cancellationToken)
                .ConfigureAwait(false);

            collections.Add(new MongoCollectionSnapshot(
                name,
                validator is not null,
                validator is null
                    ? null
                    : GetOptionalString(
                        options,
                        "validationLevel",
                        MongoValidatorDefinition.StrictLevel),
                validator is null
                    ? null
                    : GetOptionalString(
                        options,
                        "validationAction",
                        MongoValidatorDefinition.ErrorAction),
                validator,
                indexes,
                hasDocuments));
        }

        var ledger = new List<MongoLedgerRecord>();

        if (collections.Any(collection => string.Equals(
                collection.Name,
                MongoIdentitySchema.MigrationLedgerCollection,
                StringComparison.Ordinal)))
        {
            var records = await _database
                .GetCollection<BsonDocument>(MongoIdentitySchema.MigrationLedgerCollection)
                .Find(new BsonDocument())
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var record in records)
            {
                if (record.TryGetValue("_id", out var id) &&
                    id.IsString &&
                    string.Equals(id.AsString, PromotionLockId, StringComparison.Ordinal))
                {
                    continue;
                }

                var version = record.TryGetValue("_id", out var versionValue)
                    ? versionValue.IsString
                        ? versionValue.AsString
                        : versionValue.ToString() ?? string.Empty
                    : string.Empty;
                var checksum = record.TryGetValue("checksumSha256", out var checksumValue) &&
                               checksumValue.IsString
                    ? checksumValue.AsString
                    : null;
                DateTime? appliedAtUtc = record.TryGetValue("aplicadaEnUtc", out var appliedValue) &&
                                         appliedValue.IsBsonDateTime
                    ? appliedValue.AsBsonDateTime.ToUniversalTime()
                    : null;

                ledger.Add(new MongoLedgerRecord(version, checksum, appliedAtUtc));
            }
        }

        return new MongoPromotionSnapshot(
            _database.DatabaseNamespace.DatabaseName,
            collections,
            ledger);
    }

    public Task<MongoMigrationPlan> PlanAsync(
        MongoMigrationDefinition migration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(migration);
        return new MongoMigrationRunner(_database, migration).PlanAsync(cancellationToken);
    }

    public Task ApplyAsync(
        MongoMigrationDefinition migration,
        MongoPromotionAudit audit,
        Func<CancellationToken, Task> ensureLockHeld,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(migration);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(ensureLockHeld);
        return new MongoMigrationRunner(_database, migration)
            .ApplyForPromotionAsync(audit, ensureLockHeld, cancellationToken);
    }

    public Task<MongoMigrationVerification> VerifyAsync(
        MongoMigrationDefinition migration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(migration);
        return new MongoMigrationRunner(_database, migration).VerifyForPromotionAsync(
            cancellationToken);
    }

    public async Task<MongoPromotionLockHandle?> TryAcquireLockAsync(
        string databaseName,
        string version,
        TimeSpan lease,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        if (lease <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lease));
        }

        var createdLedgerCollection = await EnsureLedgerCollectionAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAtUtc = now.Add(lease);
        var ownerId = Guid.NewGuid().ToString("N");
        var ledger = _database.GetCollection<BsonDocument>(
            MongoIdentitySchema.MigrationLedgerCollection);

        var document = new BsonDocument
        {
            ["_id"] = PromotionLockId,
            ["tipo"] = PromotionLockKind,
            ["database"] = databaseName,
            ["version"] = version,
            ["ownerId"] = ownerId,
            ["fencingToken"] = 1L,
            ["adquiridoEnUtc"] = now,
            ["renovadoEnUtc"] = now,
            ["expiraEnUtc"] = expiresAtUtc
        };

        try
        {
            await ledger.InsertOneAsync(document, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return CreateHandle(
                ownerId,
                databaseName,
                version,
                expiresAtUtc,
                fencingToken: 1,
                createdLedgerCollection);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Code == 11000)
        {
            // La comparación de expiración usa el reloj del servidor. Un bloqueo
            // activo o malformado permanece cerrado y requiere intervención.
            var expiredFilter = Builders<BsonDocument>.Filter.And(
                new BsonDocument("_id", PromotionLockId),
                new BsonDocument(
                    "$expr",
                    new BsonDocument("$lt", new BsonArray { "$expiraEnUtc", "$$NOW" })));
            var leaseMilliseconds = ToLeaseMilliseconds(lease);
            var update = new BsonDocumentStagePipelineDefinition<BsonDocument, BsonDocument>(
                new[]
                {
                    new BsonDocument(
                        "$set",
                        new BsonDocument
                        {
                            ["tipo"] = PromotionLockKind,
                            ["database"] = databaseName,
                            ["version"] = version,
                            ["ownerId"] = ownerId,
                            ["fencingToken"] = new BsonDocument(
                                "$add",
                                new BsonArray
                                {
                                    new BsonDocument(
                                        "$ifNull",
                                        new BsonArray { "$fencingToken", 0L }),
                                    1L
                                }),
                            ["adquiridoEnUtc"] = "$$NOW",
                            ["renovadoEnUtc"] = "$$NOW",
                            ["expiraEnUtc"] = new BsonDocument(
                                "$add",
                                new BsonArray { "$$NOW", leaseMilliseconds })
                        })
                });
            var options = new FindOneAndUpdateOptions<BsonDocument>
            {
                ReturnDocument = ReturnDocument.After
            };
            var acquired = await ledger
                .FindOneAndUpdateAsync(expiredFilter, update, options, cancellationToken)
                .ConfigureAwait(false);

            if (acquired is null)
            {
                return null;
            }

            return CreateHandle(
                ownerId,
                databaseName,
                version,
                ReadDate(acquired, "expiraEnUtc") ?? expiresAtUtc,
                ReadFencingToken(acquired),
                createdLedgerCollection: false);
        }
    }

    public async Task<bool> RenewLockAsync(
        MongoPromotionLockHandle handle,
        TimeSpan lease,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);

        if (lease <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lease));
        }

        var leaseMilliseconds = ToLeaseMilliseconds(lease);
        var update = new BsonDocumentStagePipelineDefinition<BsonDocument, BsonDocument>(
            new[]
            {
                new BsonDocument(
                    "$set",
                    new BsonDocument
                    {
                        ["renovadoEnUtc"] = "$$NOW",
                        ["expiraEnUtc"] = new BsonDocument(
                            "$add",
                            new BsonArray { "$$NOW", leaseMilliseconds })
                    })
            });
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            ReturnDocument = ReturnDocument.After
        };

        var renewed = await _database
            .GetCollection<BsonDocument>(MongoIdentitySchema.MigrationLedgerCollection)
            .FindOneAndUpdateAsync(
                BuildActiveLockFilter(handle),
                update,
                options,
                cancellationToken)
            .ConfigureAwait(false);

        return renewed is not null;
    }

    public async Task<bool> IsLockHeldAsync(
        MongoPromotionLockHandle handle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);

        return await _database
            .GetCollection<BsonDocument>(MongoIdentitySchema.MigrationLedgerCollection)
            .Find(BuildActiveLockFilter(handle))
            .Limit(1)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ReleaseLockAsync(
        MongoPromotionLockHandle handle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);

        await _database
            .GetCollection<BsonDocument>(MongoIdentitySchema.MigrationLedgerCollection)
            .DeleteOneAsync(
                Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("_id", PromotionLockId),
                    Builders<BsonDocument>.Filter.Eq("ownerId", handle.OwnerId),
                    Builders<BsonDocument>.Filter.Eq("fencingToken", handle.FencingToken)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static FilterDefinition<BsonDocument> BuildActiveLockFilter(
        MongoPromotionLockHandle handle) =>
        new BsonDocument
        {
            ["_id"] = PromotionLockId,
            ["database"] = handle.DatabaseName,
            ["version"] = handle.Version,
            ["ownerId"] = handle.OwnerId,
            ["fencingToken"] = handle.FencingToken,
            ["$expr"] = new BsonDocument(
                "$gt",
                new BsonArray { "$expiraEnUtc", "$$NOW" })
        };

    private static long ToLeaseMilliseconds(TimeSpan lease) =>
        Math.Max(1, (long)Math.Ceiling(lease.TotalMilliseconds));

    private static long ReadFencingToken(BsonDocument document) =>
        document.TryGetValue("fencingToken", out var value) && value.IsNumeric
            ? value.ToInt64()
            : 1;

    private static DateTime? ReadDate(BsonDocument document, string name) =>
        document.TryGetValue(name, out var value) && value.IsBsonDateTime
            ? value.AsBsonDateTime.ToUniversalTime()
            : null;

    private async Task<bool> EnsureLedgerCollectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _database
                .CreateCollectionAsync(
                    MongoIdentitySchema.MigrationLedgerCollection,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (MongoCommandException exception) when (exception.Code == NamespaceExistsCode)
        {
            // El ledger ya existe; es el caso normal en una promoción repetida.
            return false;
        }
    }

    private static MongoPromotionLockHandle CreateHandle(
        string ownerId,
        string databaseName,
        string version,
        DateTime expiresAtUtc,
        long fencingToken,
        bool createdLedgerCollection) =>
        new(ownerId, databaseName, version, expiresAtUtc, fencingToken, createdLedgerCollection);

    private static string GetString(BsonDocument document, string name) =>
        document.TryGetValue(name, out var value) && value.IsString
            ? value.AsString
            : string.Empty;

    private static string GetOptionalString(
        BsonDocument document,
        string name,
        string defaultValue) =>
        document.TryGetValue(name, out var value) && value.IsString
            ? value.AsString
            : defaultValue;
}
