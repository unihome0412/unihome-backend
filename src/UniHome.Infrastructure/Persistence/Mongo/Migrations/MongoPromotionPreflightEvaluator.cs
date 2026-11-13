using MongoDB.Bson;

namespace UniHome.Infrastructure.Persistence.Mongo.Migrations;

/// <summary>
/// Evalúa un snapshot de solo lectura y un plan sin escribir. Para permitir la
/// publicación incremental exige que todas las migraciones anteriores del registro
/// estén aplicadas y que sus colecciones/índices/validadores coincidan; solo la
/// migración seleccionada puede estar incompleta. Distingue una base vacía, una
/// ejecución parcial recuperable, una migración aplicada y una divergencia que debe
/// auditarse. No lee documentos ni imprime valores.
/// </summary>
public static class MongoPromotionPreflightEvaluator
{
    public static MongoPromotionPreflight Evaluate(
        MongoPromotionSnapshot snapshot,
        MongoMigrationDefinition migration,
        MongoMigrationRegistry registry,
        MongoMigrationPlan? plan,
        IReadOnlyCollection<string>? planIssues = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(migration);
        ArgumentNullException.ThrowIfNull(registry);

        var issues = new List<string>(planIssues ?? []);
        var orderedMigrations = registry.GetUpTo(migration.Version);
        var priorMigrations = orderedMigrations
            .Where(item => !string.Equals(
                item.Version,
                migration.Version,
                StringComparison.Ordinal))
            .ToArray();
        var allowedCollections = orderedMigrations
            .SelectMany(item => item.Collections)
            .Select(collection => collection.Name)
            .Append(MongoIdentitySchema.MigrationLedgerCollection)
            .ToHashSet(StringComparer.Ordinal);

        if (!MongoPromotionGuard.IsAllowedTarget(snapshot.DatabaseName))
        {
            issues.Add("La base del snapshot no es un destino de promoción autorizado.");
        }

        var duplicateCollections = snapshot.Collections
            .GroupBy(collection => collection.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        foreach (var duplicate in duplicateCollections)
        {
            issues.Add($"Colección duplicada en el snapshot: {duplicate}.");
        }

        foreach (var collection in snapshot.Collections.Where(collection =>
                     !allowedCollections.Contains(collection.Name)))
        {
            issues.Add($"Colección no administrada por las migraciones anteriores: {collection.Name}.");
        }

        var collectionByName = snapshot.Collections
            .Where(collection => !duplicateCollections.Contains(collection.Name, StringComparer.Ordinal))
            .ToDictionary(collection => collection.Name, collection => collection, StringComparer.Ordinal);

        if (collectionByName.TryGetValue(
                MongoIdentitySchema.MigrationLedgerCollection,
                out var ledgerCollection))
        {
            if (ledgerCollection.HasValidator || ledgerCollection.Validator is not null)
            {
                issues.Add("Validador no previsto en el ledger de migraciones.");
            }

            foreach (var index in ledgerCollection.Indexes)
            {
                var indexName = index.TryGetValue("name", out var name) && name.IsString
                    ? name.AsString
                    : string.Empty;

                if (!string.Equals(indexName, "_id_", StringComparison.Ordinal))
                {
                    issues.Add($"Índice no previsto en el ledger: {indexName}.");
                }
            }
        }

        var ledgerGroups = snapshot.Ledger
            .GroupBy(record => record.Version, StringComparer.Ordinal)
            .ToArray();

        foreach (var group in ledgerGroups.Where(group => group.Count() > 1))
        {
            issues.Add($"Versión duplicada en el ledger: {group.Key}.");
        }

        var orderedVersions = orderedMigrations
            .Select(item => item.Version)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var record in snapshot.Ledger)
        {
            if (!orderedVersions.Contains(record.Version))
            {
                issues.Add($"Versión de ledger no registrada o posterior a la migración: {record.Version}.");
            }
        }

        var selectedLedger = snapshot.Ledger.FirstOrDefault(record =>
            string.Equals(record.Version, migration.Version, StringComparison.Ordinal));

        if (selectedLedger is not null &&
            !string.Equals(selectedLedger.Checksum, migration.Checksum, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"La migración {migration.Version} tiene un checksum distinto en el ledger.");
        }

        var indexesComplete = true;
        var managedCollectionsPresent = false;

        foreach (var previous in priorMigrations)
        {
            var previousLedger = snapshot.Ledger.FirstOrDefault(record =>
                string.Equals(record.Version, previous.Version, StringComparison.Ordinal));

            if (previousLedger is null)
            {
                issues.Add($"La migración previa {previous.Version} no está aplicada.");
            }
            else if (!string.Equals(
                         previousLedger.Checksum,
                         previous.Checksum,
                         StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"La migración previa {previous.Version} tiene un checksum distinto en el ledger.");
            }

            foreach (var definition in previous.Collections)
            {
                if (!collectionByName.TryGetValue(definition.Name, out var collection))
                {
                    issues.Add(
                        $"Falta la colección {definition.Name} de la migración previa {previous.Version}.");
                    continue;
                }

                managedCollectionsPresent = true;
                ValidateDefinition(definition, collection, requireEmpty: false, issues);
                ValidateIndexes(definition, collection, issues);
            }
        }

        foreach (var definition in migration.Collections)
        {
            if (!collectionByName.TryGetValue(definition.Name, out var collection))
            {
                continue;
            }

            managedCollectionsPresent = true;
            ValidateDefinition(definition, collection, requireEmpty: true, issues);

            var inspection = MongoIndexCatalog.Inspect(definition, collection.Indexes);
            issues.AddRange(inspection.Issues);

            if (inspection.Missing.Count > 0 || inspection.Issues.Count > 0)
            {
                indexesComplete = false;
            }
        }

        if (plan is not null)
        {
            if (!MongoPromotionGuard.IsAllowedTarget(plan.DatabaseName))
            {
                issues.Add("El plan no corresponde a un destino de promoción autorizado.");
            }

            if (!string.Equals(plan.Version, migration.Version, StringComparison.Ordinal))
            {
                issues.Add("El plan corresponde a otra versión de migración.");
            }

            foreach (var write in plan.Writes)
            {
                if (!allowedCollections.Contains(write.Collection))
                {
                    issues.Add($"Escritura fuera de las colecciones administradas: {write.Collection}.");
                }

                if (write.Kind == MongoMigrationWriteKind.CreateIndex &&
                    string.Equals(
                        write.Collection,
                        MongoIdentitySchema.MigrationLedgerCollection,
                        StringComparison.Ordinal))
                {
                    issues.Add("El ledger no debe recibir un índice adicional.");
                }

                if (write.Kind == MongoMigrationWriteKind.RecordMigration &&
                    !string.Equals(
                        write.Collection,
                        MongoIdentitySchema.MigrationLedgerCollection,
                        StringComparison.Ordinal))
                {
                    issues.Add("El registro de migración debe escribirse en el ledger.");
                }
            }
        }

        if (selectedLedger is not null)
        {
            if (plan?.Writes.Any(write =>
                    write.Kind is MongoMigrationWriteKind.CreateCollection or
                    MongoMigrationWriteKind.CreateIndex) == true)
            {
                issues.Add("La migración está registrada pero faltan colecciones o índices.");
            }

            if (plan?.Writes.Any(write => write.Kind == MongoMigrationWriteKind.RecordMigration) == true)
            {
                issues.Add("La migración ya está registrada; no debe reinsertarse el ledger.");
            }
        }
        else if (plan is not null &&
                 !plan.Writes.Any(write => write.Kind == MongoMigrationWriteKind.RecordMigration))
        {
            issues.Add("El plan no registra la migración en el ledger.");
        }

        var state = issues.Count > 0
            ? MongoPromotionState.Divergent
            : selectedLedger is not null && indexesComplete
                ? MongoPromotionState.Applied
                : snapshot.Collections.Count == 0
                    ? MongoPromotionState.Empty
                    : managedCollectionsPresent
                        ? MongoPromotionState.PartiallyApplied
                        : MongoPromotionState.NotStarted;

        return new MongoPromotionPreflight(
            snapshot.DatabaseName,
            migration.Version,
            migration.Checksum,
            state,
            snapshot.Collections.Count == 0,
            plan?.Writes ?? [],
            issues);
    }

    private static void ValidateDefinition(
        MongoCollectionDefinition definition,
        MongoCollectionSnapshot collection,
        bool requireEmpty,
        List<string> issues)
    {
        if (requireEmpty && collection.HasDocuments)
        {
            issues.Add($"La colección {definition.Name} contiene documentos; no se promueve sobre datos.");
        }

        if (definition.Validator is null)
        {
            if (collection.HasValidator || collection.Validator is not null)
            {
                issues.Add($"Validador no previsto: {definition.Name}.");
            }

            return;
        }

        if (!definition.Validator.Matches(
                collection.Validator,
                collection.ValidationLevel,
                collection.ValidationAction))
        {
            issues.Add($"Validador divergente: {definition.Name}.");
        }
    }

    private static void ValidateIndexes(
        MongoCollectionDefinition definition,
        MongoCollectionSnapshot collection,
        List<string> issues)
    {
        var inspection = MongoIndexCatalog.Inspect(definition, collection.Indexes);

        foreach (var issue in inspection.Issues)
        {
            issues.Add(issue);
        }

        foreach (var missing in inspection.Missing)
        {
            issues.Add($"?ndice ausente en {definition.Name}: {missing.Name}.");
        }
    }
}
