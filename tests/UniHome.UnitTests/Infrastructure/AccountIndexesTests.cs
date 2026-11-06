using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using UniHome.Infrastructure.Persistence.Mongo.Accounts;

namespace UniHome.UnitTests.Infrastructure;

public sealed class AccountIndexesTests
{
    [Fact]
    public void IndiceDeCorreo_EsUnicoYSobreElCorreoNormalizado()
    {
        var index = AccountIndexes.UniqueNormalizedEmail;

        Assert.True(index.Options.Unique is true);
        Assert.Equal(AccountIndexes.UniqueEmailIndexName, index.Options.Name);
        Assert.Equal("cuentas", AccountIndexes.CollectionName);

        var rendered = index.Keys.Render(
            new RenderArgs<AccountDocument>(
                BsonSerializer.LookupSerializer<AccountDocument>(),
                BsonSerializer.SerializerRegistry));

        Assert.Equal(1, rendered["correoNormalizado"].ToInt32());
    }
}
