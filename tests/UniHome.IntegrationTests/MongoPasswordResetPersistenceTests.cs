using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Identity.PasswordRecovery;
using UniHome.Application.Identity.Sessions;
using UniHome.Domain.Accounts;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
using UniHome.Infrastructure.Persistence.Mongo.PasswordResets;
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
        var attemptGenerated = generator.Generate();
        var attemptToken = PasswordResetToken.Issue(
            attemptGenerated.Id,
            account.Id,
            attemptGenerated.Hash,
            now);
        var expiredGenerated = generator.Generate();
        var expiredToken = PasswordResetToken.Issue(
            expiredGenerated.Id,
            account.Id,
            expiredGenerated.Hash,
            now.AddMinutes(-31));
        var serviceGenerated = generator.Generate();
        var serviceToken = PasswordResetToken.Issue(
            serviceGenerated.Id,
            account.Id,
            serviceGenerated.Hash,
            now);
        var sessionId = Guid.NewGuid();

        try
        {
            await accounts.AddAsync(account);
            await resets.AddAsync(token);
            await resets.AddAsync(attemptToken);
            await resets.AddAsync(expiredToken);
            await resets.AddAsync(serviceToken);

            var results = await Task.WhenAll(
                resets.TryConsumeAsync(token.Id, generated.Hash, now),
                resets.TryConsumeAsync(token.Id, generated.Hash, now));

            Assert.Single(results, result => result);
            Assert.Single(results, result => !result);
            Assert.False(await resets.TryConsumeAsync(token.Id, generated.Hash, now));

            var firstAttempt = await resets.RegisterFailedAttemptAsync(attemptToken.Id, now);
            var secondAttempt = await resets.RegisterFailedAttemptAsync(attemptToken.Id, now);
            Assert.NotNull(firstAttempt);
            Assert.NotNull(secondAttempt);
            Assert.Equal(2, secondAttempt.FailedAttempts);
            Assert.False(await resets.TryConsumeAsync(expiredToken.Id, expiredGenerated.Hash, now));

            var collection = database.GetCollection<BsonDocument>(
                MongoIdentitySchema.AccountRecoveryCollection);
            var document = await collection
                .Find(new BsonDocument("_id", token.Id.ToString("N")))
                .FirstOrDefaultAsync();
            var attemptDocument = await collection
                .Find(new BsonDocument("_id", attemptToken.Id.ToString("N")))
                .FirstOrDefaultAsync();

            Assert.NotNull(document);
            Assert.Equal(generated.Hash, document["tokenHash"].AsString);
            Assert.NotEqual(generated.Value, document["tokenHash"].AsString);
            Assert.False(document["consumedAt"].IsBsonNull);
            Assert.Equal(account.Id.ToString("N"), document["accountId"].AsString);
            Assert.NotNull(attemptDocument);
            Assert.Equal(2, attemptDocument["failedAttempts"].ToInt32());

            var passwordChanges = await Task.WhenAll(
                accounts.TryChangePasswordAsync(
                    account.Id,
                    account.PasswordHash,
                    "hash:concurrente-1"),
                accounts.TryChangePasswordAsync(
                    account.Id,
                    account.PasswordHash,
                    "hash:concurrente-2"));
            Assert.Single(passwordChanges, changed => changed);
            Assert.Single(passwordChanges, changed => !changed);

            var accountDocument = await database
                .GetCollection<BsonDocument>(MongoIdentitySchema.AccountsCollection)
                .Find(new BsonDocument("_id", account.Id.ToString("N")))
                .FirstOrDefaultAsync();
            Assert.NotNull(accountDocument);
            var storedHash = accountDocument["hashContrasena"].AsString;
            Assert.True(storedHash is "hash:concurrente-1" or "hash:concurrente-2");

            var sessions = factory.Services.GetRequiredService<IRefreshSessionRepository>();
            var passwordHasher = factory.Services.GetRequiredService<IPasswordHasher>();
            var confirm = new ConfirmPasswordResetService(
                accounts,
                resets,
                generator,
                passwordHasher,
                sessions,
                factory.Services.GetRequiredService<ISystemClock>());
            await sessions.CreateAsync(new RefreshSession(
                sessionId,
                account.Id,
                "hash:refresh-vigente",
                now,
                now.AddDays(30),
                [],
                RevokedAt: null,
                RevokedReason: null,
                CreatedAt: now,
                LastRotatedAt: now));

            var confirmation = await confirm.ConfirmAsync(
                new ConfirmPasswordResetCommand(serviceGenerated.Value, "NuevaClave1"));
            Assert.Equal(ConfirmPasswordResetStatus.Success, confirmation.Status);

            var storedSession = await sessions.FindByIdAsync(sessionId);
            Assert.NotNull(storedSession);
            Assert.NotNull(storedSession.RevokedAt);
            Assert.Equal("password_reset", storedSession.RevokedReason);

            var updatedAccount = await accounts.FindByIdAsync(account.Id);
            Assert.NotNull(updatedAccount);
            Assert.True(
                passwordHasher.VerifyPassword(updatedAccount.PasswordHash, "NuevaClave1"));
            Assert.False(await resets.TryConsumeAsync(
                serviceToken.Id,
                serviceGenerated.Hash,
                now.AddMinutes(1)));

            var indexes = await ListIndexesAsync(collection);
            var tokenIndex = Assert.Single(
                indexes,
                index => GetName(index) == MongoIdentitySchema.AccountRecoveryUniqueTokenIndex);
            Assert.True(tokenIndex["unique"].AsBoolean);
            Assert.Equal(
                1,
                tokenIndex["key"][MongoIdentitySchema.AccountRecoveryTokenHashField].ToInt32());

            var accountIndex = Assert.Single(
                indexes,
                index => GetName(index) == MongoIdentitySchema.AccountRecoveryAccountIndex);
            Assert.False(
                accountIndex.GetValue("unique", BsonBoolean.False).ToBoolean());
            Assert.Equal(
                1,
                accountIndex["key"][MongoIdentitySchema.AccountRecoveryAccountIdField].ToInt32());

            var ttlIndex = Assert.Single(
                indexes,
                index => GetName(index) == MongoIdentitySchema.AccountRecoveryExpirationTtlIndex);
            Assert.Equal(0, ttlIndex["expireAfterSeconds"].ToInt64());
            Assert.Equal(
                1,
                ttlIndex["key"][MongoIdentitySchema.AccountRecoveryExpiresAtUtcField].ToInt32());
        }
        finally
        {
            var resetsCollection = database.GetCollection<BsonDocument>(
                MongoIdentitySchema.AccountRecoveryCollection);
            foreach (var id in new[]
                     {
                         token.Id,
                         attemptToken.Id,
                         expiredToken.Id,
                         serviceToken.Id
                     })
            {
                await resetsCollection.DeleteOneAsync(
                    new BsonDocument("_id", id.ToString("N")));
            }

            await database
                .GetCollection<BsonDocument>(MongoIdentitySchema.SessionsCollection)
                .DeleteOneAsync(new BsonDocument("_id", sessionId.ToString("N")));

            await database
                .GetCollection<BsonDocument>(MongoIdentitySchema.AccountsCollection)
                .DeleteOneAsync(new BsonDocument("_id", account.Id.ToString("N")));
        }
    }

    private static async Task<List<BsonDocument>> ListIndexesAsync(
        IMongoCollection<BsonDocument> collection)
    {
        using var cursor = await collection.Indexes.ListAsync();
        var indexes = new List<BsonDocument>();

        while (await cursor.MoveNextAsync())
        {
            indexes.AddRange(cursor.Current);
        }

        return indexes;
    }

    private static string GetName(BsonDocument index) =>
        index.TryGetValue("name", out var value) && value.IsString
            ? value.AsString
            : string.Empty;

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
                MongoPasswordResetIndexes.Validator.ToCreateCollectionOptions());
        }

        var collection = database.GetCollection<BsonDocument>(collectionName);
        using var collectionInfos = await database.ListCollectionsAsync(
            new ListCollectionsOptions
            {
                Filter = new BsonDocument("name", collectionName)
            });
        Assert.True(await collectionInfos.MoveNextAsync());
        var collectionInfo = Assert.Single(collectionInfos.Current);
        var options = collectionInfo["options"].AsBsonDocument;
        var validator = options["validator"].AsBsonDocument;
        var level = options.GetValue("validationLevel", "strict").AsString;
        var action = options.GetValue("validationAction", "error").AsString;
        Assert.True(MongoPasswordResetIndexes.Validator.Matches(validator, level, action));
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
