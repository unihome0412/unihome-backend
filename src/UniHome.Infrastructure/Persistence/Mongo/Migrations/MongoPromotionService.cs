using MongoDB.Driver;

namespace UniHome.Infrastructure.Persistence.Mongo.Migrations;

/// <summary>
/// Orquesta una promoción explícita: guard de destino, preflight de solo lectura,
/// bloqueo distribuido, revalidación, aplicación idempotente y verificación final.
/// No se registra al iniciar la API ni se ejecuta desde ella.
/// </summary>
public sealed class MongoPromotionService
{
    public static readonly TimeSpan DefaultLockLease = TimeSpan.FromMinutes(30);

    private readonly IMongoPromotionStore _store;
    private readonly MongoMigrationRegistry _registry;
    private readonly TimeSpan _lockLease;
    private readonly TimeSpan _heartbeatInterval;

    public static MongoPromotionService Create(
        IMongoDatabase database,
        MongoMigrationRegistry? registry = null,
        TimeSpan? lockLease = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        return new MongoPromotionService(
            new MongoPromotionStore(database),
            registry,
            lockLease);
    }

    internal MongoPromotionService(
        IMongoPromotionStore store,
        MongoMigrationRegistry? registry = null,
        TimeSpan? lockLease = null,
        TimeSpan? heartbeatInterval = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _registry = registry ?? MongoMigrationRegistry.Default;
        _lockLease = lockLease ?? DefaultLockLease;

        if (_lockLease <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lockLease));
        }

        _heartbeatInterval = heartbeatInterval ??
            TimeSpan.FromTicks(Math.Max(
                TimeSpan.FromMilliseconds(100).Ticks,
                _lockLease.Ticks / 3));

        if (_heartbeatInterval <= TimeSpan.Zero || _heartbeatInterval >= _lockLease)
        {
            throw new ArgumentOutOfRangeException(nameof(heartbeatInterval));
        }
    }

    public async Task<MongoPromotionPreflight> PreflightAsync(
        string version,
        CancellationToken cancellationToken = default)
    {
        var migration = _registry.GetRequired(version);
        var snapshot = await _store.ReadSnapshotAsync(migration, cancellationToken)
            .ConfigureAwait(false);
        MongoMigrationPlan? plan = null;
        var issues = new List<string>();

        try
        {
            plan = await _store.PlanAsync(migration, cancellationToken).ConfigureAwait(false);
        }
        catch (MongoMigrationDivergenceException exception)
        {
            issues.Add(exception.Message);
        }

        return MongoPromotionPreflightEvaluator.Evaluate(
            snapshot,
            migration,
            _registry,
            plan,
            issues);
    }

    public async Task<MongoPromotionResult> PromoteAsync(
        MongoPromotionAuthorization authorization,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        MongoPromotionGuard.EnsureAuthorized(
            authorization.DatabaseName,
            authorization.ConfirmationDatabase,
            authorization.AuthorizedDatabase,
            authorization.ApprovalReference);

        var migration = _registry.GetRequired(authorization.Version);
        var initial = await PreflightAsync(migration.Version, cancellationToken)
            .ConfigureAwait(false);
        EnsureCanPromote(initial);

        var lockHandle = await _store
            .TryAcquireLockAsync(
                authorization.DatabaseName,
                migration.Version,
                _lockLease,
                cancellationToken)
            .ConfigureAwait(false);

        if (lockHandle is null)
        {
            throw new MongoPromotionLockException(
                "Ya existe una promoción activa o el bloqueo no pudo adquirirse.");
        }

        using var leaseCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        var heartbeat = RunHeartbeatAsync(lockHandle, leaseCancellation);

        try
        {
            var operationToken = leaseCancellation.Token;
            var locked = await PreflightAsync(migration.Version, operationToken)
                .ConfigureAwait(false);
            EnsureCanPromote(locked);

            if (!locked.RequiresWrites)
            {
                await EnsureLockHeldAsync(lockHandle, operationToken).ConfigureAwait(false);
                var verification = await VerifyAsync(migration, operationToken)
                    .ConfigureAwait(false);
                return new MongoPromotionResult(
                    initial,
                    locked,
                    lockHandle.CreatedLedgerCollection ? 1 : 0,
                    verification);
            }

            var audit = new MongoPromotionAudit(
                authorization.ApprovalReference,
                authorization.DatabaseName,
                migration.Version,
                migration.Checksum,
                lockHandle.OwnerId);
            await _store.ApplyAsync(
                    migration,
                    audit,
                    token => EnsureLockHeldAsync(lockHandle, token),
                    operationToken)
                .ConfigureAwait(false);
            await EnsureLockHeldAsync(lockHandle, operationToken).ConfigureAwait(false);
            var finalVerification = await VerifyAsync(migration, operationToken)
                .ConfigureAwait(false);

            return new MongoPromotionResult(
                initial,
                locked,
                locked.PendingWrites.Count + (lockHandle.CreatedLedgerCollection ? 1 : 0),
                finalVerification);
        }
        finally
        {
            leaseCancellation.Cancel();
            await ObserveHeartbeatAsync(heartbeat).ConfigureAwait(false);
            await SafeReleaseAsync(lockHandle).ConfigureAwait(false);
        }
    }

    private async Task RunHeartbeatAsync(
        MongoPromotionLockHandle handle,
        CancellationTokenSource leaseCancellation)
    {
        using var timer = new PeriodicTimer(_heartbeatInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(leaseCancellation.Token).ConfigureAwait(false))
            {
                bool renewed;

                try
                {
                    renewed = await _store
                        .RenewLockAsync(handle, _lockLease, leaseCancellation.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (leaseCancellation.IsCancellationRequested)
                {
                    return;
                }
                catch
                {
                    leaseCancellation.Cancel();
                    return;
                }

                if (!renewed)
                {
                    leaseCancellation.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (leaseCancellation.IsCancellationRequested)
        {
            // La operación terminó o el lease se perdió; el llamador ya observa el
            // token enlazado y el bloqueo se libera en el finally.
        }
    }

    private static async Task ObserveHeartbeatAsync(Task heartbeat)
    {
        try
        {
            await heartbeat.ConfigureAwait(false);
        }
        catch
        {
            // El heartbeat no debe enmascarar el error original de la operación.
        }
    }

    private async Task EnsureLockHeldAsync(
        MongoPromotionLockHandle handle,
        CancellationToken cancellationToken)
    {
        if (await _store.IsLockHeldAsync(handle, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        throw new MongoPromotionLockException(
            "El bloqueo de promoción se perdió antes de completar la operación.");
    }

    private async Task<MongoMigrationVerification> VerifyAsync(
        MongoMigrationDefinition migration,
        CancellationToken cancellationToken)
    {
        var verification = await _store.VerifyAsync(migration, cancellationToken)
            .ConfigureAwait(false);

        if (!verification.IsValid)
        {
            var issues = verification.Issues.Count == 0
                ? "La verificación posterior no es válida."
                : string.Join(" ", verification.Issues);
            throw new MongoPromotionVerificationException(issues);
        }

        return verification;
    }

    private async Task SafeReleaseAsync(MongoPromotionLockHandle handle)
    {
        try
        {
            // El bloqueo tiene expiración; no se enmascara el error original si la
            // liberación falla después de una aplicación o verificación fallida.
            await _store.ReleaseLockAsync(handle, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // El bloqueo vence por sí mismo. La ausencia de liberación se detecta
            // como bloqueo activo hasta su expiración, nunca como éxito.
        }
    }

    private static void EnsureCanPromote(MongoPromotionPreflight preflight)
    {
        if (preflight.CanPromote)
        {
            return;
        }

        var issues = preflight.Issues.Count == 0
            ? "El preflight no autoriza la promoción."
            : string.Join(" ", preflight.Issues);
        throw new MongoPromotionPreflightException(issues);
    }
}
