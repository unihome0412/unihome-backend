using MongoDB.Bson;
using UniHome.Infrastructure.Persistence.Mongo.Exploration;
using UniHome.Infrastructure.Persistence.Mongo.Publications;

namespace UniHome.UnitTests.Infrastructure;

public sealed class MongoExplorationBsonMappingTests
{
    [Fact]
    public void FavoriteDocument_DebeUsarElIndiceUnicoCuentaUnidad()
    {
        var document = new FavoriteDocument
        {
            Id = "cuenta:unidad",
            AccountId = "00112233445566778899aabbccddeeff",
            UnitId = "ffeeddccbbaa99887766554433221100",
            CreatedAt = DateTime.UtcNow
        }.ToBsonDocument();

        Assert.Equal("cuenta:unidad", document["_id"].AsString);
        Assert.Equal(
            "00112233445566778899aabbccddeeff",
            document["accountId"].AsString);
        Assert.Equal(BsonType.DateTime, document["createdAt"].BsonType);
        Assert.Contains(
            MongoExplorationMigration.Definition.Collections
                .Single()
                .Indexes,
            index => index.Name == MongoExplorationSchema.FavoriteUniqueIndex &&
                     index.Unique);
    }

    [Fact]
    public void UnitDocument_DebePersistirEstadoDeDisponibilidad()
    {
        var document = new UnitDocument
        {
            Id = "00112233445566778899aabbccddeeff",
            PropertyId = "00112233445566778899aabbccddeeff",
            OwnerAccountId = "00112233445566778899aabbccddeeff",
            UniversityId = "00112233445566778899aabbccddeeff",
            UnitType = "Habitacion",
            Occupancy = "Individual",
            Places = 1,
            PricePerPlace = 3000,
            SizeM2 = 12,
            ForWhom = "Mixto",
            Bathroom = "Privado",
            Furniture = "Amueblado",
            PublicationState = "Aprobada",
            AvailabilityState = "Disponible",
            CreatedAt = DateTime.UtcNow
        }.ToBsonDocument();

        Assert.Equal("Disponible", document["estadoOcupacion"].AsString);
        Assert.Equal("Aprobada", document["estadoPublicacion"].AsString);
    }

    [Fact]
    public void MigracionM07_DebeSerSoloFavoritos()
    {
        Assert.Equal("M07-001", MongoExplorationMigration.Version);
        Assert.Single(MongoExplorationMigration.Definition.Collections);
        Assert.Equal(
            MongoExplorationSchema.FavoritesCollection,
            MongoExplorationMigration.Definition.Collections[0].Name);
    }
}
