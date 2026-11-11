using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Accounts;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Prueba real y opt-in del repositorio de recuperación contra MongoDB. Solo se
/// ejecuta sobre una base aislada y autorizada; crea la colección vacía si no
/// existe en esa base de pruebas para poder verificar los índices y el CAS real.
/// No aplica DDL en producción ni toca documentos ajenos.
/// </summary>
public sealed class MongoPasswordResetPersistenceTests
{
    [MongoIntegrationFact]
    public async Task ConsumoConcurrente_DebePersistirSoloHashYPermitirUnUnicoGanador()
    {
        using var client = MongoIntegrationEnvironment.CreateClient();
        var database = MongoIntegrationEnvironment.GetDatabase(client);
        await EnsurePasswordResetSchemaAsync(database);

        using var factory = new MongoBackedApiFactory();
        var accounts = factory.Services.GetRequiredService<IAccountRepository>();
        var resets = factory.Services.GetRequiredService<IPasswordResetRepository>();
        var generator = factory.Services.GetRequiredService<IPasswordResetTokenGenerator>();

        var email = $"mongo.reset.{Guid.NewGuid():N}@example.invalid";
        var account = TestAccountFactory.Create(email);
        var generated = generator.Generate();
        var now = DateTimeOffset.UtcNow;
        var token = PasswordResetToken.Issue(
            generated.Id,
            account.Id,
            generated.Hash,
            now);

        try
        {
            await accounts.AddAsync(account);
            await resets.AddAsync(token);

            var results = await Task.WhenAll(
                resets.TryConsumeAsync(token.Id, generated.Hash, now),
                resets.TryConsumeAsync(token.Id, generated.Hash, now));

            Assert.Single(results, result => result);
            Assert.Single(results, result => !result);

            var document = await database
                .GetCollection<BsonDocument>(MongoIdentitySchema.AccountRecoveryCollection)
                .Find(new BsonDocument("_id", token.Id.ToString("N")))
                .FirstOrDefaultAsync();

            Assert.NotNull(document);
            Assert.Equal(generated.Hash, document["tokenHash"].AsString);
            Assert.NotEqual(generated.Value, document["tokenHash"].AsString);
            Assert.False(document["consumedAt"].IsBsonNull);
            Assert.Equal(account.Id.ToString("N"), document["accountId"].AsString);
        }
        finally
        {
            await database
                .GetCollection<BsonDocument>(MongoIdentitySchema.AccountRecoveryCollection)
                .DeleteOneAsync(new BsonDocument("_id", token.Id.ToString("N")));
            await database
                .GetCollection<BsonDocument>(MongoIdentitySchema.AccountsCollection)
                .DeleteOneAsync(new BsonDocument("_id", account.Id.ToString("N")));
        }
    }

    private static async Task EnsurePasswordResetSchemaAsync(IMongoDatabase database)
    {
        const string collectionName = MongoIdentitySchema.AccountRecoveryCollection;
        using var names = await database
            .ListCollectionNamesAsync(new ListCollectionNamesOptions
            {
                Filter = new BsonDocument("name", collectionName)
            });
        var exists = await names.MoveNextAsync();

        if (!exists || !names.Current.Any())
        {
            await database.CreateCollectionAsync(
                collectionName,
                new MongoValidatorDefinition(BsonDocument.Parse(
                    """
                    {
                      "$jsonSchema": {
                        "bsonType": "object",
                        "required": ["accountId", "tokenHash", "expiresAt"],
                        "properties": {
                          "_id": { "bsonType": ["objectId", "string", "binData"] },
                          "accountId": { "bsonType": ["objectId", "string", "binData"] },
                          "tokenHash": { "bsonType": "string", "maxLength": 4096 },
                          "expiresAt": { "bsonType": "date" },
                          "consumedAt": { "bsonType": "date" },
                          "createdAt": { "bsonType": "date" }
                        }
                      }
                    }
                    """)).ToCreateCollectionOptions());
        }

        var collection = database.GetCollection<BsonDocument>(collectionName);
        await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            new BsonDocumentIndexKeysDefinition<BsonDocument>(
                new BsonDocument(MongoIdentitySchema.AccountRecoveryTokenHashField, 1)),
            new CreateIndexOptions
            {
                Name = MongoIdentitySchema.AccountRecoveryUniqueTokenIndex,
                Unique = true
            }));
        await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            new BsonDocumentIndexKeysDefinition<BsonDocument>(
                new BsonDocument(MongoIdentitySchema.AccountRecoveryAccountIdField, 1)),
            new CreateIndexOptions { Name = MongoIdentitySchema.AccountRecoveryAccountIndex }));
        await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            new BsonDocumentIndexKeysDefinition<BsonDocument>(
                new BsonDocument(MongoIdentitySchema.AccountRecoveryExpiresAtUtcField, 1)),
            new CreateIndexOptions
            {
                Name = MongoIdentitySchema.AccountRecoveryExpirationTtlIndex,
                ExpireAfter = TimeSpan.Zero
            }));
    }
}
