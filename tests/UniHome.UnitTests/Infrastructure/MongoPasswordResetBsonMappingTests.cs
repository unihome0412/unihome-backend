using MongoDB.Bson;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.PasswordResets;

namespace UniHome.UnitTests.Infrastructure;

public sealed class MongoPasswordResetBsonMappingTests
{
    private const string Identifier = "00112233445566778899aabbccddeeff";
    private static readonly DateTime Timestamp = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string[] ExpectedFields =
    [
        "_id",
        "accountId",
        "tokenHash",
        "expiresAt",
        "consumedAt",
        "createdAt",
        "failedAttempts"
    ];

    [Fact]
    public void PasswordReset_DebeUsarNombresYTiposDelModeloExistente()
    {
        var document = new PasswordResetDocument
        {
            Id = Identifier,
            AccountId = Identifier,
            TokenHash = "hash",
            ExpiresAt = Timestamp.AddMinutes(30),
            ConsumedAt = Timestamp,
            CreatedAt = Timestamp,
            FailedAttempts = 2
        }.ToBsonDocument();

        Assert.Equal(
            ExpectedFields.Order(),
            document.Names.Order());

        Assert.Equal(BsonType.String, document["_id"].BsonType);
        Assert.Equal(BsonType.String, document["accountId"].BsonType);
        Assert.Equal(BsonType.String, document["tokenHash"].BsonType);
        Assert.Equal(BsonType.DateTime, document["expiresAt"].BsonType);
        Assert.Equal(BsonType.DateTime, document["consumedAt"].BsonType);
        Assert.Equal(BsonType.DateTime, document["createdAt"].BsonType);
        Assert.Equal(BsonType.Int32, document["failedAttempts"].BsonType);
    }

    [Fact]
    public void Indices_Reset_DebenCoincidirConLaColeccionExistente()
    {
        var definition = MongoPasswordResetIndexes.Definition;

        Assert.Equal(MongoIdentitySchema.AccountRecoveryCollection, definition.Name);
        Assert.Equal(3, definition.Indexes.Count);

        var token = Assert.Single(
            definition.Indexes,
            index => index.Name == MongoIdentitySchema.AccountRecoveryUniqueTokenIndex);
        Assert.True(token.Unique);
        Assert.Equal(
            MongoIdentitySchema.AccountRecoveryTokenHashField,
            Assert.Single(token.Fields).Name);

        var account = Assert.Single(
            definition.Indexes,
            index => index.Name == MongoIdentitySchema.AccountRecoveryAccountIndex);
        Assert.False(account.Unique);
        Assert.Equal(
            MongoIdentitySchema.AccountRecoveryAccountIdField,
            Assert.Single(account.Fields).Name);

        var ttl = Assert.Single(
            definition.Indexes,
            index => index.Name == MongoIdentitySchema.AccountRecoveryExpirationTtlIndex);
        Assert.Equal(0, ttl.ExpireAfterSeconds);
        Assert.Equal(
            MongoIdentitySchema.AccountRecoveryExpiresAtUtcField,
            Assert.Single(ttl.Fields).Name);
    }
}
