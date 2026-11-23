using MongoDB.Bson;
using UniHome.Infrastructure.Persistence.Mongo.OccupancyLinks;

namespace UniHome.UnitTests.Infrastructure;

public sealed class MongoOccupancyBsonMappingTests
{
    [Fact]
    public void OccupancyLinkDocument_UsaIdDeterministaInquilinoUnidad()
    {
        var document = new OccupancyLinkDocument
        {
            Id = "inquilino:unidad",
            TenantAccountId = "00112233445566778899aabbccddeeff",
            UnitId = "ffeeddccbbaa99887766554433221100",
            Status = "Activo",
            JoinedAt = DateTime.UtcNow
        }.ToBsonDocument();

        Assert.Equal("inquilino:unidad", document["_id"].AsString);
        Assert.Equal("Activo", document["estado"].AsString);
        Assert.Equal(BsonType.DateTime, document["fechaVinculo"].BsonType);
        Assert.False(document.Contains("fechaFin"));
        Assert.False(document.Contains("motivoFin"));
    }

    [Fact]
    public void OccupancyLinkDocument_FinalizadoPersisteMotivoYFecha()
    {
        var document = new OccupancyLinkDocument
        {
            Id = "inquilino:unidad",
            TenantAccountId = "00112233445566778899aabbccddeeff",
            UnitId = "ffeeddccbbaa99887766554433221100",
            Status = "Finalizado",
            JoinedAt = DateTime.UtcNow,
            EndedAt = DateTime.UtcNow,
            EndedReason = "AbandonoInquilino"
        }.ToBsonDocument();

        Assert.Equal("Finalizado", document["estado"].AsString);
        Assert.Equal("AbandonoInquilino", document["motivoFin"].AsString);
        Assert.Equal(BsonType.DateTime, document["fechaFin"].BsonType);
    }

    [Fact]
    public void UnitInvitationDocument_PersisteCodigoYVigencia()
    {
        var document = new UnitInvitationDocument
        {
            Id = "00112233445566778899aabbccddeeff",
            UnitId = "ffeeddccbbaa99887766554433221100",
            Code = "ABCD2345",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        }.ToBsonDocument();

        Assert.Equal("ABCD2345", document["codigo"].AsString);
        Assert.Equal(BsonType.DateTime, document["vigenciaHasta"].BsonType);
    }

    [Fact]
    public void MigracionM09_DebeCrearVinculosEInvitaciones()
    {
        Assert.Equal("M09-001", MongoOccupancyMigration.Version);
        Assert.Equal(2, MongoOccupancyMigration.Definition.Collections.Count);
        Assert.Contains(
            MongoOccupancyMigration.Definition.Collections,
            collection => collection.Name == MongoOccupancySchema.LinksCollection);
        Assert.Contains(
            MongoOccupancyMigration.Definition.Collections,
            collection => collection.Name == MongoOccupancySchema.InvitationsCollection);

        var links = MongoOccupancyMigration.Definition.Collections
            .Single(collection => collection.Name == MongoOccupancySchema.LinksCollection);
        Assert.Equal(2, links.Indexes.Count);
        Assert.DoesNotContain(links.Indexes, index => index.Unique);

        var invitations = MongoOccupancyMigration.Definition.Collections
            .Single(collection => collection.Name == MongoOccupancySchema.InvitationsCollection);
        var uniqueCode = Assert.Single(
            invitations.Indexes,
            index => index.Name == MongoOccupancySchema.InvitationCodeUniqueIndex);
        Assert.True(uniqueCode.Unique);
    }
}
