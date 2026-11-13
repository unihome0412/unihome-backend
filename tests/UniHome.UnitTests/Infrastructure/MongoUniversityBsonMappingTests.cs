using MongoDB.Bson;
using UniHome.Infrastructure.Persistence.Mongo.Universities;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.UnitTests.Infrastructure;

public sealed class MongoUniversityBsonMappingTests
{
    private const string Identifier = "00112233445566778899aabbccddeeff";
    private static readonly DateTime Timestamp = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string[] ExpectedFields =
    [
        "_id",
        "nombreYCampus",
        "nombreNormalizado",
        "activa",
        "createdAt"
    ];

    [Fact]
    public void UniversityDocument_DebeUsarLosNombresDelModeloM03()
    {
        var document = new UniversityDocument
        {
            Id = Identifier,
            NameAndCampus = "Universidad Tecnológica de Monterrey",
            NormalizedName = "universidad tecnologica de monterrey",
            IsActive = true,
            CreatedAt = Timestamp
        }.ToBsonDocument();

        Assert.Equal(ExpectedFields.Order(), document.Names.Order());
        Assert.Equal(BsonType.String, document["_id"].BsonType);
        Assert.Equal(BsonType.String, document["nombreYCampus"].BsonType);
        Assert.Equal(BsonType.String, document["nombreNormalizado"].BsonType);
        Assert.Equal(BsonType.Boolean, document["activa"].BsonType);
        Assert.Equal(BsonType.DateTime, document["createdAt"].BsonType);
    }

    [Fact]
    public void Indices_Universidades_DebenExigirNombreNormalizadoUnico()
    {
        var definition = MongoUniversityIndexes.Definition;

        Assert.Equal(MongoUniversitySchema.Collection, definition.Name);
        var index = Assert.Single(definition.Indexes);
        Assert.Equal(MongoUniversitySchema.UniqueNormalizedNameIndex, index.Name);
        Assert.True(index.Unique);
        Assert.Equal(
            MongoUniversitySchema.NormalizedNameField,
            Assert.Single(index.Fields).Name);
    }

    [Fact]
    public void MigracionM03_DebeEstarVersionadaYAdministrarUniversidades()
    {
        var definition = MongoUniversityMigration.Definition;

        Assert.Equal("M03-001", definition.Version);
        var collection = Assert.Single(definition.Collections);
        Assert.Equal(MongoUniversitySchema.Collection, collection.Name);
        Assert.Equal(
            MongoUniversitySchema.UniqueNormalizedNameIndex,
            Assert.Single(collection.Indexes).Name);
    }
}
