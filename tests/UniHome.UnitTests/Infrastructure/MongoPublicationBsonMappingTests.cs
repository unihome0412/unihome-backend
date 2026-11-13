using MongoDB.Bson;
using UniHome.Infrastructure.Persistence.Mongo.Publications;

namespace UniHome.UnitTests.Infrastructure;

public sealed class MongoPublicationBsonMappingTests
{
    [Fact]
    public void PropertyDocument_DebeUsarCamposYTiposDelModeloM05()
    {
        var document = new PropertyDocument
        {
            Id = "00112233445566778899aabbccddeeff",
            OwnerAccountId = "00112233445566778899aabbccddeeff",
            Name = "Casa",
            NameNormalized = "casa",
            Address = "Calle 1",
            AddressNormalized = "calle 1",
            Colonia = "Centro",
            ColoniaNormalized = "centro",
            PropertyType = "Casa",
            UniversityId = "00112233445566778899aabbccddeeff",
            PublicationState = "Borrador",
            CreatedAt = DateTime.UtcNow
        }.ToBsonDocument();

        Assert.Equal("Casa", document["nombre"].AsString);
        Assert.Equal("casa", document["nombreNormalizado"].AsString);
        Assert.Equal("Casa", document["tipoPropiedad"].AsString);
        Assert.Equal("Borrador", document["estadoPublicacion"].AsString);
        Assert.Equal(BsonType.DateTime, document["createdAt"].BsonType);
    }

    [Fact]
    public void UnitDocument_DebeGuardarDecimalesMueblesServiciosYFotos()
    {
        var document = new UnitDocument
        {
            Id = "00112233445566778899aabbccddeeff",
            PropertyId = "00112233445566778899aabbccddeeff",
            OwnerAccountId = "00112233445566778899aabbccddeeff",
            UniversityId = "00112233445566778899aabbccddeeff",
            UnitType = "Habitacion",
            Occupancy = "Compartida",
            Places = 3,
            PricePerPlace = 3500.50m,
            SizeM2 = 12.75m,
            ForWhom = "Mixto",
            Bathroom = "Privado",
            Furniture = "Amueblado",
            FurnitureItems = ["Cama"],
            Services =
            [
                new ServiceChargeDocument
                {
                    Name = "Internet",
                    Mode = "Aparte",
                    Amount = 300
                }
            ],
            Photos =
            [
                new UnitPhotoDocument
                {
                    StorageKey = "key",
                    ContentType = "image/png",
                    SizeBytes = 10,
                    Sha256 = "hash",
                    Order = 0,
                    IsPrimary = true,
                    CreatedAt = DateTime.UtcNow
                }
            ],
            PublicationState = "Borrador",
            CreatedAt = DateTime.UtcNow
        }.ToBsonDocument();

        Assert.Equal(BsonType.Decimal128, document["precioMensualPorLugarMxn"].BsonType);
        Assert.Equal(BsonType.Decimal128, document["tamanoM2"].BsonType);
        Assert.Equal(3, document["lugares"].ToInt32());
        Assert.Single(document["muebles"].AsBsonArray);
        Assert.Single(document["servicios"].AsBsonArray);
        Assert.Single(document["fotos"].AsBsonArray);
        Assert.True(document["fotos"].AsBsonArray[0].AsBsonDocument["esPrincipal"].AsBoolean);
    }

    [Fact]
    public void IndicesYMigracionM05_DebenEstarVersionados()
    {
        Assert.Equal("M05-001", MongoPublicationMigration.Version);
        var names = MongoPublicationMigration.Definition.Collections
            .Select(collection => collection.Name)
            .ToArray();
        Assert.Contains(MongoPublicationSchema.PropertiesCollection, names);
        Assert.Contains(MongoPublicationSchema.UnitsCollection, names);
        Assert.Contains(MongoPublicationSchema.TermsCollection, names);

        var properties = MongoPublicationMigration.Definition.Collections
            .Single(collection => collection.Name == MongoPublicationSchema.PropertiesCollection);
        Assert.Contains(
            properties.Indexes,
            index => index.Name == MongoPublicationSchema.PropertyUniqueAddressIndex &&
                     index.Unique);

        var terms = MongoPublicationMigration.Definition.Collections
            .Single(collection => collection.Name == MongoPublicationSchema.TermsCollection);
        Assert.Contains(
            terms.Indexes,
            index => index.Name == MongoPublicationSchema.TermsUniqueIndex &&
                     index.Unique);
    }

    [Fact]
    public async Task AlmacenamientoNoConfigurado_DebeFallarCerrado()
    {
        var storage = new UniHome.Infrastructure.Storage.UnconfiguredUnitPhotoStorage();

        Assert.Throws<UniHome.Application.Identity.Exceptions.StorageUnavailableException>(() =>
            storage.GetReadUrl("key"));
        await Assert.ThrowsAsync<UniHome.Application.Identity.Exceptions.StorageUnavailableException>(
            () => storage.SaveAsync(new MemoryStream([1]), "image/png"));
    }
}
