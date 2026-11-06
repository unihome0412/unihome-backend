using MongoDB.Bson;
using MongoDB.Driver;

namespace UniHome.Infrastructure.Persistence.Mongo.Migrations;

/// <summary>
/// Planifica, aplica y verifica una migración declarativa. Nunca imprime ni
/// registra la cadena de conexión, documentos de cuenta o tokens.
/// </summary>
public sealed class MongoMigrationRunner
{
    private const int NamespaceExistsCode = 48;

    private readonly IMongoDatabase _database;
    private readonly MongoMigrationDefinition _migration;

    public MongoMigrationRunner(IMongoDatabase database, MongoMigrationDefinition migration)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _migration = migration ?? throw new ArgumentNullException(nameof(migration));
    }

    public string DatabaseName => _database.DatabaseNamespace.DatabaseName;

    public async Task<MongoMigrationPlan> PlanAsync(CancellationToken cancellationToken = default)
    {
        var collections = await GetCollectionInfosAsync(cancellationToken).ConfigureAwait(false);
        var collectionInfos = collections
            .Select(collection => new
            {
                Name = GetString(collection, "name"),
                Document = collection
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToDictionary(item => item.Name, item => item.Document, StringComparer.Ordinal);

        var writes = new List<MongoMigrationWrite>();
        var issues = new List<string>();

        foreach (var collectionDefinition in _migration.Collections)
        {
            if (!collectionInfos.TryGetValue(collectionDefinition.Name, out var collectionInfo))
            {
                writes.Add(new MongoMigrationWrite(
                    MongoMigrationWriteKind.CreateCollection,
                    collectionDefinition.Name,
                    collectionDefinition.Name,
                    "Colección ausente."));

                writes.AddRange(collectionDefinition.Indexes.Select(index =>
                    new MongoMigrationWrite(
                        MongoMigrationWriteKind.CreateIndex,
                        collectionDefinition.Name,
                        index.Name,
                        index.ToKeyString())));

                continue;
            }

            ValidateCollectionOptions(collectionDefinition, collectionInfo, issues);

            var indexes = await ReadIndexesAsync(collectionDefinition.Name, cancellationToken)
                .ConfigureAwait(false);

            ValidateIndexes(collectionDefinition, indexes, writes, issues);
        }

        var ledgerCollection = MongoIdentitySchema.MigrationLedgerCollection;

        if (!collectionInfos.ContainsKey(ledgerCollection))
        {
            // El identificador de versión se guarda en _id, que MongoDB ya indexa
            // de forma única; no se crea un índice adicional sobre _id.
            writes.Add(new MongoMigrationWrite(
                MongoMigrationWriteKind.CreateCollection,
                ledgerCollection,
                ledgerCollection,
                "Colección de versiones ausente."));
        }

        BsonDocument? existingMigration = null;

        if (collectionInfos.ContainsKey(ledgerCollection))
        {
            existingMigration = await FindMigrationRecordAsync(cancellationToken).ConfigureAwait(false);
        }

        if (existingMigration is null)
        {
            writes.Add(new MongoMigrationWrite(
                MongoMigrationWriteKind.RecordMigration,
                ledgerCollection,
                _migration.Version,
                _migration.Checksum));
        }
        else
        {
            ValidateRecordedChecksum(existingMigration, _migration);
        }

        if (issues.Count > 0)
        {
            throw new MongoMigrationDivergenceException(string.Join(" ", issues));
        }

        return new MongoMigrationPlan(DatabaseName, _migration.Version, writes);
    }

    public async Task<MongoMigrationPlan> ApplyAsync(CancellationToken cancellationToken = default)
    {
        MongoMigrationGuard.EnsureAllowed(
            DatabaseName,
            Environment.GetEnvironmentVariable(MongoMigrationGuard.AllowlistEnvironmentVariable));

        return await ApplyPlanAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Aplicación interna usada exclusivamente por la promoción explícita, que ya
    /// validó MongoPromotionGuard y la credencial técnica. No amplía la allowlist
    /// de ApplyAsync.
    /// </summary>
    internal Task<MongoMigrationPlan> ApplyForPromotionAsync(
        MongoPromotionAudit audit,
        Func<CancellationToken, Task> ensureLockHeld,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(ensureLockHeld);
        return ApplyPlanAsync(cancellationToken, ensureLockHeld, audit);
    }

    private async Task<MongoMigrationPlan> ApplyPlanAsync(
        CancellationToken cancellationToken,
        Func<CancellationToken, Task>? ensureLockHeld = null,
        MongoPromotionAudit? audit = null)
    {
        var plan = await PlanAsync(cancellationToken).ConfigureAwait(false);

        if (!plan.RequiresWrites)
        {
            return plan;
        }

        foreach (var write in plan.Writes)
        {
            if (ensureLockHeld is not null)
            {
                await ensureLockHeld(cancellationToken).ConfigureAwait(false);
            }

            switch (write.Kind)
            {
                case MongoMigrationWriteKind.CreateCollection:
                    await CreateCollectionIfMissingAsync(write.Collection, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case MongoMigrationWriteKind.CreateIndex:
                    await CreateIndexAsync(write.Collection, write.Name, cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case MongoMigrationWriteKind.RecordMigration:
                    await RecordMigrationAsync(audit, cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    throw new InvalidOperationException($"Tipo de escritura desconocido: {write.Kind}.");
            }
        }

        return plan;
    }

    public async Task<MongoSchemaAudit> AuditAsync(CancellationToken cancellationToken = default)
    {
        var collections = await GetCollectionInfosAsync(cancellationToken).ConfigureAwait(false);
        var audits = new List<MongoCollectionAudit>(collections.Count);

        foreach (var collection in collections)
        {
            var name = GetString(collection, "name");

            if (string.IsNullOrWhiteSpace(name) || name.StartsWith("system.", StringComparison.Ordinal))
            {
                continue;
            }

            var options = collection.TryGetValue("options", out var optionsValue) && optionsValue.IsBsonDocument
                ? optionsValue.AsBsonDocument
                : new BsonDocument();

            var hasValidator = options.TryGetValue("validator", out var validator) &&
                validator.IsBsonDocument && validator.AsBsonDocument.ElementCount > 0;
            var validationLevel = GetOptionalString(
                options,
                "validationLevel",
                hasValidator ? "strict" : null);
            var validationAction = GetOptionalString(
                options,
                "validationAction",
                hasValidator ? "error" : null);

            var indexes = await ReadIndexesAsync(name, cancellationToken).ConfigureAwait(false);
            var indexAudits = indexes.Select(ToAudit).ToArray();
            var count = await _database
                .GetCollection<BsonDocument>(name)
                .EstimatedDocumentCountAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            audits.Add(new MongoCollectionAudit(
                name,
                hasValidator,
                validationLevel,
                validationAction,
                indexAudits,
                count,
                hasValidator
                    ? MongoBsonCanonicalizer.ToCanonicalJson(validator.AsBsonDocument)
                    : null));
        }

        return new MongoSchemaAudit(DatabaseName, audits);
    }

    public Task<MongoMigrationVerification> VerifyAsync(
        CancellationToken cancellationToken = default) =>
        VerifyCoreAsync(requireEmptyCollections: false, cancellationToken);

    internal Task<MongoMigrationVerification> VerifyForPromotionAsync(
        CancellationToken cancellationToken = default) =>
        VerifyCoreAsync(requireEmptyCollections: true, cancellationToken);

    private async Task<MongoMigrationVerification> VerifyCoreAsync(
        bool requireEmptyCollections,
        CancellationToken cancellationToken)
    {
        var issues = new List<string>();
        MongoMigrationPlan? plan = null;

        try
        {
            plan = await PlanAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MongoMigrationDivergenceException exception)
        {
            issues.Add(exception.Message);
        }

        var existingMigration = await FindMigrationRecordAsync(cancellationToken).ConfigureAwait(false);
        var applied = existingMigration is not null;
        var checksumMatches = applied &&
            string.Equals(
                GetString(existingMigration!, "checksumSha256"),
                _migration.Checksum,
                StringComparison.OrdinalIgnoreCase);

        if (!applied)
        {
            issues.Add($"La migración {_migration.Version} no está registrada.");
        }
        else if (!checksumMatches)
        {
            issues.Add($"La migración {_migration.Version} tiene un checksum distinto.");
        }

        var indexesMatch = plan is not null &&
            !plan.Writes.Any(write =>
                write.Kind is MongoMigrationWriteKind.CreateCollection or MongoMigrationWriteKind.CreateIndex);

        var audit = await AuditAsync(cancellationToken).ConfigureAwait(false);

        if (requireEmptyCollections)
        {
            foreach (var collectionDefinition in _migration.Collections)
            {
                if (await HasAnyDocumentAsync(collectionDefinition.Name, cancellationToken)
                        .ConfigureAwait(false))
                {
                    issues.Add(
                        $"La colección {collectionDefinition.Name} contiene documentos; la verificación no es válida.");
                }
            }
        }

        return new MongoMigrationVerification(
            DatabaseName,
            _migration.Version,
            applied,
            checksumMatches,
            indexesMatch,
            plan?.Writes ?? [],
            audit.Collections,
            issues);
    }

    private static void ValidateCollectionOptions(
        MongoCollectionDefinition collectionDefinition,
        BsonDocument collectionInfo,
        ICollection<string> issues)
    {
        var options = GetOptions(collectionInfo);
        var existingValidator = options.TryGetValue("validator", out var validatorValue) &&
                                validatorValue.IsBsonDocument &&
                                validatorValue.AsBsonDocument.ElementCount > 0
            ? validatorValue.AsBsonDocument
            : null;

        if (collectionDefinition.Validator is null)
        {
            if (existingValidator is not null)
            {
                issues.Add($"Validador no previsto: {collectionDefinition.Name}.");
            }

            return;
        }

        var level = GetOptionalString(
            options,
            "validationLevel",
            existingValidator is null ? null : MongoValidatorDefinition.StrictLevel);
        var action = GetOptionalString(
            options,
            "validationAction",
            existingValidator is null ? null : MongoValidatorDefinition.ErrorAction);

        if (!collectionDefinition.Validator.Matches(existingValidator, level, action))
        {
            issues.Add($"Validador divergente: {collectionDefinition.Name}.");
        }
    }

    private void ValidateIndexes(
        MongoCollectionDefinition collectionDefinition,
        IReadOnlyList<BsonDocument> existingIndexes,
        ICollection<MongoMigrationWrite> writes,
        ICollection<string> issues)
    {
        var inspection = MongoIndexCatalog.Inspect(collectionDefinition, existingIndexes);
        foreach (var issue in inspection.Issues)
        {
            issues.Add(issue);
        }
        foreach (var desired in inspection.Missing)
        {
            writes.Add(new MongoMigrationWrite(
                MongoMigrationWriteKind.CreateIndex,
                collectionDefinition.Name,
                desired.Name,
                desired.ToKeyString()));
        }
    }

    private async Task CreateCollectionIfMissingAsync(
        string collectionName,
        CancellationToken cancellationToken)
    {
        if (string.Equals(
                collectionName,
                MongoIdentitySchema.MigrationLedgerCollection,
                StringComparison.Ordinal))
        {
            try
            {
                await _database
                    .CreateCollectionAsync(collectionName, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (MongoCommandException exception) when (exception.Code == NamespaceExistsCode)
            {
                // El ledger ya existe; es el caso normal en una promoción repetida.
            }

            return;
        }

        var definition = _migration.Collections.FirstOrDefault(collection =>
            string.Equals(collection.Name, collectionName, StringComparison.Ordinal));

        if (definition is null)
        {
            throw new InvalidOperationException(
                $"La migración no define la colección '{collectionName}'.");
        }

        try
        {
            await _database
                .CreateCollectionAsync(
                    collectionName,
                    definition.Validator?.ToCreateCollectionOptions(),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoCommandException exception) when (exception.Code == NamespaceExistsCode)
        {
            // Idempotencia ante una ejecución concurrente o una colección ya creada.
        }
    }

    private async Task CreateIndexAsync(
        string collectionName,
        string indexName,
        CancellationToken cancellationToken)
    {
        var desired = FindIndex(collectionName, indexName);

        await _database
            .GetCollection<BsonDocument>(collectionName)
            .Indexes
            .CreateOneAsync(desired.ToCreateIndexModel(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private MongoIndexDefinition FindIndex(string collectionName, string indexName)
    {
        var index = _migration.Collections
            .Where(collection => string.Equals(collection.Name, collectionName, StringComparison.Ordinal))
            .SelectMany(collection => collection.Indexes)
            .FirstOrDefault(candidate => string.Equals(candidate.Name, indexName, StringComparison.Ordinal));

        return index ?? throw new InvalidOperationException(
            $"La migración no define el índice '{indexName}' para '{collectionName}'.");
    }

    private async Task RecordMigrationAsync(
        MongoPromotionAudit? audit,
        CancellationToken cancellationToken)
    {
        var document = new BsonDocument
        {
            ["_id"] = _migration.Version,
            ["nombre"] = _migration.Name,
            ["descripcion"] = _migration.Description,
            ["checksumSha256"] = _migration.Checksum,
            ["aplicadaEnUtc"] = DateTime.UtcNow
        };

        if (audit is not null)
        {
            document["promocion"] = new BsonDocument
            {
                ["aprobacionRef"] = audit.ApprovalReference,
                ["baseAutorizada"] = audit.AuthorizedDatabase,
                ["versionAprobada"] = audit.Version,
                ["checksumAprobado"] = audit.Checksum,
                ["ownerId"] = audit.OwnerId
            };
        }

        try
        {
            // Inmutable: nunca reemplazar el checksum ganador de otro proceso.
            await _database
                .GetCollection<BsonDocument>(MongoIdentitySchema.MigrationLedgerCollection)
                .InsertOneAsync(document, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Code == 11000)
        {
            var existing = await FindMigrationRecordAsync(cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                throw new MongoMigrationDivergenceException("No se pudo verificar el registro concurrente de migración.");
            }
            ValidateRecordedChecksum(existing, _migration);
        }
    }

    internal static void ValidateRecordedChecksum(BsonDocument existing, MongoMigrationDefinition migration)
    {
        if (!string.Equals(GetString(existing, "checksumSha256"), migration.Checksum, StringComparison.OrdinalIgnoreCase))
        {
            throw new MongoMigrationDivergenceException(
                $"La migración {migration.Version} ya tiene otro checksum; requiere una versión nueva.");
        }
    }

    private async Task<BsonDocument?> FindMigrationRecordAsync(CancellationToken cancellationToken)
    {
        var collections = await GetCollectionInfosAsync(cancellationToken).ConfigureAwait(false);

        if (!collections.Any(collection =>
                string.Equals(
                    GetString(collection, "name"),
                    MongoIdentitySchema.MigrationLedgerCollection,
                    StringComparison.Ordinal)))
        {
            return null;
        }

        return await _database
            .GetCollection<BsonDocument>(MongoIdentitySchema.MigrationLedgerCollection)
            .Find(new BsonDocument("_id", _migration.Version))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task<bool> HasAnyDocumentAsync(
        string collectionName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

        return await _database
            .GetCollection<BsonDocument>(collectionName)
            .Find(FilterDefinition<BsonDocument>.Empty)
            .Limit(1)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task<IReadOnlyList<BsonDocument>> GetCollectionInfosAsync(
        CancellationToken cancellationToken)
    {
        using var cursor = await _database
            .ListCollectionsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var result = new List<BsonDocument>();

        while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
        {
            result.AddRange(cursor.Current);
        }

        return result;
    }

    internal async Task<IReadOnlyList<BsonDocument>> ReadIndexesAsync(
        string collectionName,
        CancellationToken cancellationToken)
    {
        using var cursor = await _database
            .GetCollection<BsonDocument>(collectionName)
            .Indexes
            .ListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new List<BsonDocument>();

        while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
        {
            result.AddRange(cursor.Current);
        }

        return result;
    }

    internal static BsonDocument GetOptions(BsonDocument collectionInfo)
    {
        ArgumentNullException.ThrowIfNull(collectionInfo);
        return collectionInfo.TryGetValue("options", out var options) && options.IsBsonDocument
            ? options.AsBsonDocument
            : new BsonDocument();
    }

    private static MongoIndexAudit ToAudit(BsonDocument index)
    {
        var key = index.TryGetValue("key", out var keyValue) && keyValue.IsBsonDocument
            ? keyValue.AsBsonDocument.ToJson()
            : "{}";

        var unique = index.GetValue("unique", BsonBoolean.False).ToBoolean();
        long? expireAfterSeconds = index.TryGetValue("expireAfterSeconds", out var ttl) && ttl.IsNumeric
            ? ttl.ToInt64()
            : null;

        return new MongoIndexAudit(
            GetString(index, "name"),
            key,
            unique,
            expireAfterSeconds);
    }

    private static string GetString(BsonDocument document, string name) =>
        document.TryGetValue(name, out var value) && value.IsString
            ? value.AsString
            : string.Empty;

    private static string? GetOptionalString(
        BsonDocument document,
        string name,
        string? defaultValue) =>
        document.TryGetValue(name, out var value) && value.IsString
            ? value.AsString
            : defaultValue;
}
