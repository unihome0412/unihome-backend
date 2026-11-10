using MongoDB.Bson;
using MongoDB.Driver;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Prueba real y opt-in del bloqueo distribuido de promoción contra MongoDB
/// (Atlas autorizado o MongoDB efímero de CI). Solo se ejecuta cuando el entorno
/// declara UNIHOME_RUN_MONGO_INTEGRATION_TESTS=1,
/// UNIHOME_MIGRATION_ALLOWED_DATABASES incluye la base elegida y esa base nunca
/// es unihome_desarrollo (rechazada incondicionalmente por MongoMigrationGuard).
/// Solo toca el documento "promotion-lock" de la colección "migraciones"; no
/// modifica cuentas ni sesiones_refresh.
/// </summary>
public sealed class MongoPromotionLockPersistenceTests
{
    private const string Version = "M01-001";
    private static readonly TimeSpan ShortLease = TimeSpan.FromSeconds(2);

    [MongoIntegrationFact]
    public async Task BloqueoDistribuido_ExclusionMutua_RenovacionYFencingObsoleto_DebeFuncionarContraMongoReal()
    {
        using var clientA = MongoIntegrationEnvironment.CreateClient();
        using var clientB = MongoIntegrationEnvironment.CreateClient();

        var databaseA = MongoIntegrationEnvironment.GetDatabase(clientA);
        var databaseB = MongoIntegrationEnvironment.GetDatabase(clientB);
        var databaseName = databaseA.DatabaseNamespace.DatabaseName;

        var storeA = new MongoPromotionStore(databaseA);
        var storeB = new MongoPromotionStore(databaseB);
        var ledger = databaseA.GetCollection<BsonDocument>(
            MongoIdentitySchema.MigrationLedgerCollection);

        await EnsureNoLeftoverLockAsync(ledger);

        MongoPromotionLockHandle? handleA = null;
        MongoPromotionLockHandle? handleB = null;

        try
        {
            // 7) Exclusión mutua entre dos procesos independientes: A adquiere,
            // B intenta de inmediato y debe quedar fuera.
            handleA = await storeA.TryAcquireLockAsync(databaseName, Version, ShortLease);
            Assert.NotNull(handleA);

            var blockedAttempt = await storeB.TryAcquireLockAsync(databaseName, Version, ShortLease);
            Assert.Null(blockedAttempt);

            // 8) Renovación del lease / heartbeat: A renueva antes de vencer y el
            // bloqueo sigue vigente para su propio token.
            var renewed = await storeA.RenewLockAsync(handleA, ShortLease);
            Assert.True(renewed);
            Assert.True(await storeA.IsLockHeldAsync(handleA));

            // 10) Pérdida de lease: A deja de renovar y el lease vence según el
            // reloj del servidor (no el reloj local).
            await Task.Delay(ShortLease + TimeSpan.FromSeconds(1));

            // B puede tomar el bloqueo vencido; el fencing token se incrementa y
            // el propietario cambia.
            handleB = await storeB.TryAcquireLockAsync(databaseName, Version, ShortLease);
            Assert.NotNull(handleB);
            Assert.True(handleB!.FencingToken > handleA.FencingToken);
            Assert.NotEqual(handleA.OwnerId, handleB.OwnerId);

            // 9) Un fencing token obsoleto no puede escribir: A, con su handle
            // viejo, ya no puede renovar ni se le reconoce el bloqueo.
            var staleRenew = await storeA.RenewLockAsync(handleA, ShortLease);
            Assert.False(staleRenew);
            Assert.False(await storeA.IsLockHeldAsync(handleA));

            // El nuevo propietario (B) sigue teniendo el bloqueo vigente.
            Assert.True(await storeB.IsLockHeldAsync(handleB));

            // Liberación limpia por el propietario correcto únicamente.
            await storeB.ReleaseLockAsync(handleB);
            handleB = null;

            // 12) Limpieza de documentos temporales: no debe quedar el
            // documento de bloqueo, y el intento obsoleto de A no pudo
            // liberarlo ni recrearlo.
            var remainingLock = await ledger
                .Find(new BsonDocument("_id", MongoPromotionStore.PromotionLockId))
                .FirstOrDefaultAsync();
            Assert.Null(remainingLock);

            // 11) Una interrupción (el fallo de A) no deja el ledger
            // inconsistente: una promoción nueva puede adquirir el bloqueo
            // desde cero.
            var freshHandle = await storeA.TryAcquireLockAsync(databaseName, Version, ShortLease);
            Assert.NotNull(freshHandle);
            Assert.Equal(1, freshHandle!.FencingToken);
            await storeA.ReleaseLockAsync(freshHandle);
        }
        finally
        {
            await EnsureNoLeftoverLockAsync(ledger);
        }
    }

    private static async Task EnsureNoLeftoverLockAsync(IMongoCollection<BsonDocument> ledger)
    {
        await ledger.DeleteOneAsync(
            new BsonDocument("_id", MongoPromotionStore.PromotionLockId));
    }
}
