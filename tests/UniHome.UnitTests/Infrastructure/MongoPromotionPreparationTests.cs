using MongoDB.Bson;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Accounts;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
using UniHome.Infrastructure.Persistence.Mongo.Sessions;

namespace UniHome.UnitTests.Infrastructure;

/// <summary>
/// Regresiones locales de preparación. No habilitan una ruta de promoción ni
/// construyen un MongoClient; no leen credenciales ni acceden a Atlas.
/// </summary>
public sealed class MongoPromotionPreparationTests
{
    [Theory]
    [InlineData("unihome_desarrollo")]
    [InlineData("UNIHOME_DESARROLLO")]
    [InlineData(" unihome_desarrollo ")]
    [InlineData("admin")]
    [InlineData("local")]
    [InlineData("config")]
    public void AllowlistNoDebeAnularDestinosSiempreProhibidos(string database)
    {
        var allowlist = $"unihome_modelo_pruebas,{database.Trim()},unihome_verificacion";
        Assert.False(MongoMigrationGuard.IsAllowed(database, allowlist));
        Assert.Throws<InvalidOperationException>(() => MongoMigrationGuard.EnsureAllowed(database, allowlist));
    }

    [Fact]
    public void M01Registrada_DebeConservarSuChecksumPublicado()
    {
        // Hash observado en el ledger y plan; no modificar M01-001 para evolucionar
        // el modelo. Cambios posteriores requieren otra versión.
        Assert.Equal("M01-001", MongoIdentityMigration.Definition.Version);
        Assert.Equal(
            "6FE8415A25A62BFA8C2A6D2F12C591B6645FF388D69E1B5260298B940E08CC4D",
            MongoIdentityMigration.Definition.Checksum);
    }

    [Fact]
    public void CamposIndexadosM01_DebenExistirEnElBsonSerializado()
    {
        var documents = new Dictionary<string, BsonDocument>
        {
            [MongoIdentitySchema.AccountsCollection] = new AccountDocument().ToBsonDocument(),
            [MongoIdentitySchema.SessionsCollection] = new RefreshSessionDocument().ToBsonDocument()
        };

        foreach (var collection in MongoIdentityMigration.Definition.Collections)
        {
            Assert.True(documents.TryGetValue(collection.Name, out var document));
            Assert.NotNull(document);
            foreach (var index in collection.Indexes)
            {
                foreach (var field in index.Fields)
                {
                    Assert.True(document.Contains(field.Name), $"{collection.Name}/{index.Name}/{field.Name}");
                }
            }
        }
    }

    [Fact]
    public void M01_NoDebeDuplicarColeccionesNiNombresDeIndices()
    {
        var collections = MongoIdentityMigration.Definition.Collections;
        Assert.Equal(collections.Count, collections.Select(collection => collection.Name).Distinct().Count());
        foreach (var collection in collections)
        {
            Assert.Equal(collection.Indexes.Count, collection.Indexes.Select(index => index.Name).Distinct().Count());
        }
    }
}
