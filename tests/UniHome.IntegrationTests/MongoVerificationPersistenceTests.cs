using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using UniHome.Domain.Verification;
using UniHome.Infrastructure.Configuration;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
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
}
