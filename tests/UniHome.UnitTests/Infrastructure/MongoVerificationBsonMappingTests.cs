using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using UniHome.Infrastructure.Persistence.Mongo.Verification;

namespace UniHome.UnitTests.Infrastructure;

public sealed class MongoVerificationBsonMappingTests
{
    [Fact]
    public void ExpedienteBson_DebeConservarDocumentosRiesgoYDecision()
    {
        var document = new VerificationExpedienteDocument
        {
            Id = Guid.NewGuid().ToString("N"),
            Folio = "UH-2026-ABCDEF123456",
            OwnerAccountId = Guid.NewGuid().ToString("N"),
            PublicationId = Guid.NewGuid().ToString("N"),
            PropertyId = Guid.NewGuid().ToString("N"),
            Estado = "EnRevision",
            EsSospechoso = true,
            SenalesRiesgo = ["Metadatos indican software de edición: photoshop."],
            ResultadoComparacionNombres = "NoDisponible",
            Ine = new VerificationDocumentMetadataDocument
            {
                Tipo = "Ine",
                StorageKey = "doc-1",
                OriginalFileName = "ine.png",
                ContentType = "image/png",
                SizeBytes = 100,
                Sha256 = new string('A', 64),
                Version = 1,
                UploadedAt = DateTime.UtcNow,
                NombreOcr = "Ana Pérez",
                EstadoOcr = "Completado",
                EstadoEdicion = "Completado",
                EdicionDetectada = true,
                ElaDisponible = true,
                MetadatosDisponibles = true,
                SenalesEdicion = ["señal"],
                DetalleAnalisis = "detalle"
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SubmittedAt = DateTime.UtcNow,
            Decision = new VerificationDecisionDocument
            {
                Estado = "Aprobado",
                ActorAccountId = Guid.NewGuid().ToString("N"),
                DecidedAt = DateTime.UtcNow
            }
        };

        var bson = document.ToBsonDocument();
        var restored = BsonSerializer.Deserialize<VerificationExpedienteDocument>(bson);

        Assert.True(bson.Contains("folio"));
        Assert.True(bson.Contains("ownerAccountId"));
        Assert.True(bson.Contains("esSospechoso"));
        Assert.False(bson.Contains("Folio"));
        Assert.Equal(document.Id, restored.Id);
        Assert.Equal("EnRevision", restored.Estado);
        Assert.True(restored.EsSospechoso);
        Assert.Equal("Ine", restored.Ine!.Tipo);
        Assert.Equal("Ana Pérez", restored.Ine.NombreOcr);
        Assert.True(restored.Ine.ElaDisponible);
        Assert.True(restored.Ine.MetadatosDisponibles);
        Assert.NotNull(restored.Decision);
        Assert.Equal("Aprobado", restored.Decision!.Estado);
    }

    [Fact]
    public void DocumentoBlob_DebeGuardarCiphertextNonceYTag()
    {
        var document = new VerificationDocumentBlobDocument
        {
            Id = "doc-1",
            ExpedienteId = Guid.NewGuid().ToString("N"),
            Tipo = "Ine",
            Version = 1,
            ContentType = "image/png",
            OriginalFileName = "ine.png",
            SizeBytes = 4,
            Sha256 = new string('A', 64),
            KeyVersion = 2,
            Nonce = [1, 2, 3],
            Tag = [4, 5, 6],
            Ciphertext = [7, 8, 9],
            CreatedAt = DateTime.UtcNow
        };

        var restored = BsonSerializer.Deserialize<VerificationDocumentBlobDocument>(
            document.ToBsonDocument());

        var bson = document.ToBsonDocument();
        Assert.True(bson.Contains("expedienteId"));
        Assert.True(bson.Contains("ciphertext"));
        Assert.True(bson.Contains("keyVersion"));
        Assert.False(bson.Contains("ExpedienteId"));
        Assert.Equal(2, restored.KeyVersion);
        Assert.Equal(new byte[] { 1, 2, 3 }, restored.Nonce);
        Assert.Equal(new byte[] { 4, 5, 6 }, restored.Tag);
        Assert.Equal(new byte[] { 7, 8, 9 }, restored.Ciphertext);
    }

    [Fact]
    public void MigracionM06_DebeDeclararColeccionesEIndicesEsperados()
    {
        Assert.Equal("M06-001", MongoVerificationMigration.Version);
        Assert.Equal(3, MongoVerificationMigration.Definition.Collections.Count);
        Assert.Contains(
            MongoVerificationMigration.Definition.Collections,
            collection => collection.Name == MongoVerificationSchema.ExpedientesCollection);
        Assert.Contains(
            MongoVerificationMigration.Definition.Collections,
            collection => collection.Name == MongoVerificationSchema.DocumentsCollection);
        Assert.Contains(
            MongoVerificationMigration.Definition.Collections,
            collection => collection.Name == MongoVerificationSchema.AuditCollection);

        var expedientes = MongoVerificationMigration.Definition.Collections
            .Single(collection => collection.Name == MongoVerificationSchema.ExpedientesCollection);
        Assert.Contains(expedientes.Indexes, index =>
            index.Name == MongoVerificationSchema.UniqueOwnerIndex && index.Unique);
        Assert.Contains(expedientes.Indexes, index =>
            index.Name == MongoVerificationSchema.UniqueFolioIndex && index.Unique);

        var documents = MongoVerificationMigration.Definition.Collections
            .Single(collection => collection.Name == MongoVerificationSchema.DocumentsCollection);
        Assert.Contains(documents.Indexes, index =>
            index.Name == MongoVerificationSchema.UniqueDocumentVersionIndex && index.Unique);
    }
}
