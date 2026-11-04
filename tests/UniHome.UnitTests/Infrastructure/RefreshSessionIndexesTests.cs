using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using UniHome.Infrastructure.Persistence.Mongo.Sessions;

namespace UniHome.UnitTests.Infrastructure;

public sealed class RefreshSessionIndexesTests
{
    [Fact]
    public void Indices_IncluyenUnicidadCuentaYExpurgacion()
    {
        Assert.Equal("sesiones_refresh", RefreshSessionIndexes.CollectionName);
        Assert.Equal(3, RefreshSessionIndexes.All.Count);

        var current = Assert.Single(
            RefreshSessionIndexes.All,
            index => index.Options.Name == RefreshSessionIndexes.UniqueCurrentTokenIndexName);
        Assert.True(current.Options.Unique is true);

        var account = Assert.Single(
            RefreshSessionIndexes.All,
            index => index.Options.Name == RefreshSessionIndexes.AccountIndexName);
        Assert.NotEqual(true, account.Options.Unique);

        var ttl = Assert.Single(
            RefreshSessionIndexes.All,
            index => index.Options.Name == RefreshSessionIndexes.ExpirationTtlIndexName);
        Assert.Equal(TimeSpan.Zero, ttl.Options.ExpireAfter);
    }

    [Fact]
    public void IndiceDeTokenActual_ApuntaAlHashVigente()
    {
        var index = Assert.Single(
            RefreshSessionIndexes.All,
            candidate => candidate.Options.Name == RefreshSessionIndexes.UniqueCurrentTokenIndexName);

        var rendered = Render(index);

        Assert.Equal(1, rendered["hashRefreshActual"].ToInt32());
    }

    [Fact]
    public void IndiceTtl_ApuntaALaExpiracion()
    {
        var index = Assert.Single(
            RefreshSessionIndexes.All,
            candidate => candidate.Options.Name == RefreshSessionIndexes.ExpirationTtlIndexName);

        var rendered = Render(index);

        Assert.Equal(1, rendered["expiraEnUtc"].ToInt32());
    }

    private static BsonDocument Render(CreateIndexModel<RefreshSessionDocument> index) =>
        index.Keys.Render(
            new RenderArgs<RefreshSessionDocument>(
                BsonSerializer.LookupSerializer<RefreshSessionDocument>(),
                BsonSerializer.SerializerRegistry));
}
