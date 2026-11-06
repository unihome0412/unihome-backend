using MongoDB.Bson;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;

namespace UniHome.UnitTests.Infrastructure;

public sealed class MongoMigrationTests
{
    [Fact]
    public void M01_DebeDefinirLasColeccionesEIndicesDelCodigo()
    {
        var definition = MongoIdentityMigration.Definition;

        Assert.Equal(MongoIdentityMigration.Version, definition.Version);
        Assert.Equal(2, definition.Collections.Count);

        var accounts = Assert.Single(
            definition.Collections,
            collection => collection.Name == MongoIdentitySchema.AccountsCollection);
        var emailIndex = Assert.Single(accounts.Indexes);
        Assert.Equal(MongoIdentitySchema.AccountsUniqueEmailIndex, emailIndex.Name);
        Assert.True(emailIndex.Unique);
        Assert.Equal(
            MongoIdentitySchema.AccountsNormalizedEmailField,
            Assert.Single(emailIndex.Fields).Name);

        var sessions = Assert.Single(
            definition.Collections,
            collection => collection.Name == MongoIdentitySchema.SessionsCollection);
        Assert.Equal(3, sessions.Indexes.Count);

        var tokenIndex = Assert.Single(
            sessions.Indexes,
            index => index.Name == MongoIdentitySchema.SessionsUniqueCurrentTokenIndex);
        Assert.True(tokenIndex.Unique);
        Assert.Equal(
            MongoIdentitySchema.SessionsCurrentTokenHashField,
            Assert.Single(tokenIndex.Fields).Name);

        var accountIndex = Assert.Single(
            sessions.Indexes,
            index => index.Name == MongoIdentitySchema.SessionsAccountIndex);
        Assert.False(accountIndex.Unique);
        Assert.Null(accountIndex.ExpireAfterSeconds);
        Assert.Equal(
            MongoIdentitySchema.SessionsAccountIdField,
            Assert.Single(accountIndex.Fields).Name);

        var ttlIndex = Assert.Single(
            sessions.Indexes,
            index => index.Name == MongoIdentitySchema.SessionsExpirationTtlIndex);
        Assert.False(ttlIndex.Unique);
        Assert.Equal(0, ttlIndex.ExpireAfterSeconds);
        Assert.Equal(
            MongoIdentitySchema.SessionsExpiresAtUtcField,
            Assert.Single(ttlIndex.Fields).Name);
    }

    [Fact]
    public void Checksum_DebeSerEstableYCambiarConLaDefinicion()
    {
        var first = CreateDefinition(unique: true);
        var second = CreateDefinition(unique: true);
        var changed = CreateDefinition(unique: false);

        Assert.Equal(first.Checksum, second.Checksum);
        Assert.NotEqual(first.Checksum, changed.Checksum);
    }

    [Fact]
    public void IndexDefinition_DebeAceptarUnIndiceEquivalenteSinOpcionesOpcionales()
    {
        var definition = new MongoIndexDefinition(
            "ux_prueba",
            [new MongoIndexField("campo", 1)],
            unique: true);

        var actual = BsonDocument.Parse(
            "{ name: 'ux_prueba', key: { campo: 1 }, unique: true }");

        Assert.True(definition.Matches(actual));
    }

    [Theory]
    [InlineData("{ name: 'ux_prueba', key: { campo: -1 }, unique: true }")]
    [InlineData("{ name: 'ux_prueba', key: { campo: 1 } }")]
    [InlineData("{ name: 'ux_prueba', key: { campo: 1 }, unique: true, sparse: true }")]
    [InlineData("{ name: 'ux_prueba', key: { campo: 1 }, unique: true, hidden: true }")]
    [InlineData("{ name: 'ux_prueba', key: { campo: 1 }, unique: true, collation: { locale: 'en' } }")]
    [InlineData("{ name: 'ux_prueba', key: { campo: 1 }, unique: true, partialFilterExpression: { campo: { $exists: true } } }")]
    public void IndexDefinition_DebeRechazarDivergenciasCriticas(string json)
    {
        var definition = new MongoIndexDefinition(
            "ux_prueba",
            [new MongoIndexField("campo", 1)],
            unique: true);

        Assert.False(definition.Matches(BsonDocument.Parse(json)));
    }

    [Fact]
    public void IndexDefinition_DebeRechazarTtlDistinto()
    {
        var definition = new MongoIndexDefinition(
            "ttl_prueba",
            [new MongoIndexField("expiraEnUtc", 1)],
            expireAfterSeconds: 0);

        Assert.True(definition.Matches(
            BsonDocument.Parse("{ name: 'ttl_prueba', key: { expiraEnUtc: 1 }, expireAfterSeconds: 0 }")));
        Assert.False(definition.Matches(
            BsonDocument.Parse("{ name: 'ttl_prueba', key: { expiraEnUtc: 1 }, expireAfterSeconds: 60 }")));
        Assert.False(definition.Matches(
            BsonDocument.Parse("{ name: 'ttl_prueba', key: { expiraEnUtc: 1 }, expireAfterSeconds: 0.5 }")));
        Assert.False(definition.Matches(
            BsonDocument.Parse("{ name: 'ttl_prueba', key: { expiraEnUtc: 1 } }")));
    }

    [Fact]
    public void Guard_DebeBloquearDesarrolloYAdministracion()
    {
        Assert.False(MongoMigrationGuard.IsAllowed("unihome_desarrollo"));
        Assert.False(MongoMigrationGuard.IsAllowed("admin"));
        Assert.False(MongoMigrationGuard.IsAllowed("local"));
        Assert.False(MongoMigrationGuard.IsAllowed("config"));
        Assert.False(MongoMigrationGuard.IsAllowed(string.Empty));
    }

    [Fact]
    public void Guard_DebeAdmitirLaBaseDePruebasYUnaListaExplicita()
    {
        Assert.True(MongoMigrationGuard.IsAllowed(MongoMigrationGuard.TestDatabaseName));
        Assert.False(MongoMigrationGuard.IsAllowed("unihome_verificacion"));
        Assert.True(MongoMigrationGuard.IsAllowed(
            "unihome_verificacion",
            "otra, unihome_verificacion;ultima"));
    }

    private static MongoMigrationDefinition CreateDefinition(bool unique) =>
        new(
            "TEST-001",
            "Prueba",
            "Definición de prueba",
            [
                new(
                    "coleccion_prueba",
                    [
                        new(
                            "ux_prueba",
                            [new MongoIndexField("campo", 1)],
                            unique: unique)
                    ])
            ]);
}
