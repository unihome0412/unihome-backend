using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Verification.Abstractions;
using UniHome.Application.Verification.Admin;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.Domain.Verification;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Identity;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
using UniHome.Infrastructure.Persistence.Mongo.Publications;
using UniHome.Infrastructure.Persistence.Mongo.Units;
using UniHome.Infrastructure.Persistence.Mongo.Verification;
using UniHome.Infrastructure.Verification;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Prueba real opt-in de M06: exige M06-001 aplicada, cifra documentos con
/// AES-256-GCM dentro de MongoDB y comprueba que el binario en claro no se
/// persiste. Usa una base aislada y elimina solo sus documentos.
/// </summary>
public sealed class MongoVerificationPersistenceTests
{
    [MongoIntegrationFact]
    public async Task ExpedienteYDocumentosCifrados_DebenPersistirSinBinarioEnClaro()
    {
        using var client = MongoIntegrationEnvironment.CreateClient();
        var database = MongoIntegrationEnvironment.GetDatabase(client);
        var migration = new MongoMigrationRunner(
            database,
            MongoVerificationMigration.Definition);
        Assert.Empty((await migration.PlanAsync()).Writes);

        var mongoOptions = new MongoDbOptions
        {
            ConnectionString = MongoIntegrationEnvironment.ConnectionString,
            DatabaseName = MongoIntegrationEnvironment.DatabaseName,
            ServerSelectionTimeoutSeconds = 15
        };
        using var provider = new MongoDbClientProvider(Options.Create(mongoOptions));
        var security = new DocumentSecurityOptions
        {
            EncryptionKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            UrlSigningKey = "unihome-mongo-verification-url-key-0123456789",
            UrlLifetimeSeconds = 300
        };
        var repository = new MongoVerificationRepository(
            provider,
            NullLogger<MongoVerificationRepository>.Instance);
        var store = new MongoVerificationDocumentStore(
            provider,
            Options.Create(security),
            NullLogger<MongoVerificationDocumentStore>.Instance);
        var accountId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var plaintextIne = Encoding.UTF8.GetBytes("INE de prueba con nombre Ana Pérez");
        var plaintextProof = Encoding.UTF8.GetBytes("Comprobante de propiedad de prueba");
        var shaIne = Convert.ToHexString(SHA256.HashData(plaintextIne));
        var shaProof = Convert.ToHexString(SHA256.HashData(plaintextProof));
        var expediente = Expediente.Create(
            Guid.NewGuid(),
            $"UH-2026-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            accountId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            now);

        try
        {
            await repository.AddAsync(expediente);

            var ineStored = await store.SaveAsync(
                expediente.Id,
                TipoDocumentoVerificacion.Ine,
                1,
                plaintextIne,
                "image/png",
                "ine.png",
                shaIne);
            var ine = DocumentoVerificacion.Create(
                TipoDocumentoVerificacion.Ine,
                ineStored.StorageKey,
                "ine.png",
                "image/png",
                plaintextIne.LongLength,
                shaIne,
                1,
                now);
            expediente.AddOrReplaceDocument(ine, now);
            Assert.True(await repository.TryReplaceAsync(expediente, now));

            var proofStored = await store.SaveAsync(
                expediente.Id,
                TipoDocumentoVerificacion.Comprobante,
                1,
                plaintextProof,
                "application/pdf",
                "predial.pdf",
                shaProof);
            var expected = expediente.UpdatedAt;
            var proof = DocumentoVerificacion.Create(
                TipoDocumentoVerificacion.Comprobante,
                proofStored.StorageKey,
                "predial.pdf",
                "application/pdf",
                plaintextProof.LongLength,
                shaProof,
                1,
                now.AddMinutes(1));
            expediente.AddOrReplaceDocument(proof, now.AddMinutes(1));
            Assert.True(await repository.TryReplaceAsync(expediente, expected));

            var loaded = await repository.FindByOwnerAsync(accountId);
            Assert.NotNull(loaded);
            Assert.Equal(EstadoExpediente.EnRevision, loaded!.State);
            Assert.False(loaded.IsSuspicious);

            var documents = database.GetCollection<BsonDocument>(
                MongoVerificationSchema.DocumentsCollection);
            var storedIne = await documents
                .Find(new BsonDocument("_id", ineStored.StorageKey))
                .FirstOrDefaultAsync();
            Assert.NotNull(storedIne);
            var ciphertext = storedIne!["ciphertext"].AsByteArray;
            Assert.NotEmpty(ciphertext);
            Assert.DoesNotContain(
                Encoding.UTF8.GetString(plaintextIne),
                Encoding.Latin1.GetString(ciphertext),
                StringComparison.Ordinal);
            Assert.True(storedIne.Contains("keyVersion"));
            Assert.Equal(1, storedIne["keyVersion"].AsInt32);
            Assert.Equal(12, storedIne["nonce"].AsByteArray.Length);
            Assert.Equal(16, storedIne["tag"].AsByteArray.Length);

            var read = await store.ReadAsync(ineStored.StorageKey);
            Assert.NotNull(read);
            Assert.Equal(plaintextIne, read!.Content);
            Assert.Equal(shaIne, read.Sha256);

            var pending = await repository.CountAsync(EstadoExpediente.EnRevision);
            Assert.True(pending >= 1);
            var listed = await repository.ListAsync(null, true, 0, 100);
            Assert.Contains(listed, item => item.Id == expediente.Id);
        }
        finally
        {
            await database
                .GetCollection<BsonDocument>(MongoVerificationSchema.DocumentsCollection)
                .DeleteManyAsync(new BsonDocument(
                    "expedienteId",
                    expediente.Id.ToString("N")));
            await database
                .GetCollection<BsonDocument>(MongoVerificationSchema.ExpedientesCollection)
                .DeleteManyAsync(new BsonDocument("_id", expediente.Id.ToString("N")));
            await database
                .GetCollection<BsonDocument>(MongoVerificationSchema.AuditCollection)
                .DeleteManyAsync(new BsonDocument(
                    "expedienteId",
                    expediente.Id.ToString("N")));
        }
    }

    [MongoIntegrationFact]
    public async Task DecisionAprobacion_DebePersistirExpedienteUnidadYAuditoria()
    {
        using var client = MongoIntegrationEnvironment.CreateClient();
        var database = MongoIntegrationEnvironment.GetDatabase(client);
        var migration = new MongoMigrationRunner(
            database,
            MongoVerificationMigration.Definition);
        Assert.Empty((await migration.PlanAsync()).Writes);

        var mongoOptions = new MongoDbOptions
        {
            ConnectionString = MongoIntegrationEnvironment.ConnectionString,
            DatabaseName = MongoIntegrationEnvironment.DatabaseName,
            ServerSelectionTimeoutSeconds = 15
        };
        using var provider = new MongoDbClientProvider(Options.Create(mongoOptions));
        var security = new DocumentSecurityOptions
        {
            EncryptionKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            UrlSigningKey = "unihome-mongo-decision-url-key-0123456789",
            UrlLifetimeSeconds = 300
        };
        var repository = new MongoVerificationRepository(
            provider,
            NullLogger<MongoVerificationRepository>.Instance);
        var units = new MongoUnitRepository(
            provider,
            NullLogger<MongoUnitRepository>.Instance);
        var audit = new MongoVerificationAuditRepository(
            provider,
            NullLogger<MongoVerificationAuditRepository>.Instance);
        var store = new MongoVerificationDocumentStore(
            provider,
            Options.Create(security),
            NullLogger<MongoVerificationDocumentStore>.Instance);
        var now = DateTimeOffset.UtcNow;
        var ownerId = Guid.NewGuid();
        var unit = Unit.Restore(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ownerId,
            Guid.NewGuid(),
            UnitType.Habitacion,
            Occupancy.Individual,
            1,
            3200,
            12,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.Amueblado,
            [],
            null,
            null,
            null,
            [],
            [],
            PublicationState.EnRevision,
            now);
        var expediente = Expediente.Create(
            Guid.NewGuid(),
            $"UH-2026-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            ownerId,
            unit.Id,
            unit.PropertyId,
            now);

        try
        {
            await units.AddAsync(unit);
            await repository.AddAsync(expediente);

            var ineContent = Encoding.UTF8.GetBytes("INE decisión");
            var proofContent = Encoding.UTF8.GetBytes("Comprobante decisión");
            var ineSha = Convert.ToHexString(SHA256.HashData(ineContent));
            var proofSha = Convert.ToHexString(SHA256.HashData(proofContent));
            var ineStored = await store.SaveAsync(
                expediente.Id,
                TipoDocumentoVerificacion.Ine,
                1,
                ineContent,
                "image/png",
                "ine.png",
                ineSha);
            expediente.AddOrReplaceDocument(
                DocumentoVerificacion.Create(
                    TipoDocumentoVerificacion.Ine,
                    ineStored.StorageKey,
                    "ine.png",
                    "image/png",
                    ineContent.LongLength,
                    ineSha,
                    1,
                    now),
                now);
            Assert.True(await repository.TryReplaceAsync(expediente, now));

            var proofStored = await store.SaveAsync(
                expediente.Id,
                TipoDocumentoVerificacion.Comprobante,
                1,
                proofContent,
                "image/png",
                "predial.png",
                proofSha);
            var expected = expediente.UpdatedAt;
            expediente.AddOrReplaceDocument(
                DocumentoVerificacion.Create(
                    TipoDocumentoVerificacion.Comprobante,
                    proofStored.StorageKey,
                    "predial.png",
                    "image/png",
                    proofContent.LongLength,
                    proofSha,
                    1,
                    now.AddMinutes(1)),
                now.AddMinutes(1));
            Assert.True(await repository.TryReplaceAsync(expediente, expected));

            var service = new DecideExpedienteService(
                repository,
                units,
                audit,
                new TestSupport.NoOpPublicCacheInvalidator(),
                new SystemClock());
            var adminId = Guid.NewGuid();

            var result = await service.ApproveAsync(adminId, expediente.Id);

            Assert.Equal(UniHome.Application.Common.OperationStatus.Success, result.Status);
            var storedExpediente = await repository.FindByIdAsync(expediente.Id);
            Assert.NotNull(storedExpediente);
            Assert.Equal(EstadoExpediente.Aprobado, storedExpediente!.State);
            Assert.Equal(adminId, storedExpediente.Decision!.ActorAccountId);
            var storedUnit = await units.FindByIdAsync(unit.Id);
            Assert.NotNull(storedUnit);
            Assert.Equal(PublicationState.Aprobada, storedUnit!.PublicationState);
            var auditDocument = await database
                .GetCollection<BsonDocument>(MongoVerificationSchema.AuditCollection)
                .Find(new BsonDocument("expedienteId", expediente.Id.ToString("N")))
                .FirstOrDefaultAsync();
            Assert.NotNull(auditDocument);
            Assert.Equal(
                VerificationAuditActions.ExpedienteApproved,
                auditDocument!["accion"].AsString);
        }
        finally
        {
            await database
                .GetCollection<BsonDocument>(MongoVerificationSchema.DocumentsCollection)
                .DeleteManyAsync(new BsonDocument(
                    "expedienteId",
                    expediente.Id.ToString("N")));
            await database
                .GetCollection<BsonDocument>(MongoVerificationSchema.ExpedientesCollection)
                .DeleteManyAsync(new BsonDocument("_id", expediente.Id.ToString("N")));
            await database
                .GetCollection<BsonDocument>(MongoVerificationSchema.AuditCollection)
                .DeleteManyAsync(new BsonDocument(
                    "expedienteId",
                    expediente.Id.ToString("N")));
            await database
                .GetCollection<BsonDocument>(MongoPublicationSchema.UnitsCollection)
                .DeleteManyAsync(new BsonDocument("_id", unit.Id.ToString("N")));
        }
    }
}
