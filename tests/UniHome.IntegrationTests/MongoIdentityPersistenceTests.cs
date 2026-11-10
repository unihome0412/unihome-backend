using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
/// Prueba real y opt-in contra MongoDB (Atlas autorizado o MongoDB efímero de CI).
/// Solo se ejecuta cuando el entorno declara UNIHOME_RUN_MONGO_INTEGRATION_TESTS=1
/// y una base autorizada.
/// </summary>
public sealed class MongoIdentityPersistenceTests
{
    private const string Password = "ClaveSegura1";

    [MongoIntegrationFact]
    public async Task RegistroLoginRefreshConcurrenteYLogout_DebeFuncionarContraMongoReal()
    {
        using var client = MongoIntegrationEnvironment.CreateClient();
        var database = MongoIntegrationEnvironment.GetDatabase(client);
        var migrationRunner = new MongoMigrationRunner(database, MongoIdentityMigration.Definition);

        var idempotentPlan = await migrationRunner.PlanAsync();
        Assert.Empty(idempotentPlan.Writes);

        using var factory = new MongoBackedApiFactory();
        using var http = factory.CreateClient();

        var email = $"mongo.it.{Guid.NewGuid():N}@example.invalid";
        Guid? accountId = null;

        try
        {
            using var registration = await http.PostAsJsonAsync(
                "/api/v1/auth/registro",
                ValidPayload(email));

            Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

            using var registrationJson = JsonDocument.Parse(
                await registration.Content.ReadAsStringAsync());
            var registrationRoot = registrationJson.RootElement;
            accountId = registrationRoot.GetProperty("id").GetGuid();
            Assert.True(registrationRoot.GetProperty("correoConfirmacionEnviado").GetBoolean());

            var accounts = database.GetCollection<BsonDocument>(
                MongoIdentitySchema.AccountsCollection);
            var sessions = database.GetCollection<BsonDocument>(
                MongoIdentitySchema.SessionsCollection);

            var accountDocument = await accounts
                .Find(new BsonDocument("_id", accountId.Value.ToString("N")))
                .FirstOrDefaultAsync();

            Assert.NotNull(accountDocument);
            Assert.Equal(email, accountDocument["correoNormalizado"].AsString);
            Assert.NotEqual(Password, accountDocument["hashContrasena"].AsString);
            Assert.True(accountDocument["hashContrasena"].AsString.Length > 40);

            var passwordHasher = factory.Services.GetRequiredService<IPasswordHasher>();
            Assert.True(passwordHasher.VerifyPassword(
                accountDocument["hashContrasena"].AsString,
                Password));

            var accountIndexes = await ListIndexesAsync(accounts);
            var uniqueEmailIndex = Assert.Single(
                accountIndexes,
                index => GetName(index) == MongoIdentitySchema.AccountsUniqueEmailIndex);
            Assert.True(uniqueEmailIndex["unique"].AsBoolean);
            Assert.Equal(1, uniqueEmailIndex["key"]["correoNormalizado"].ToInt32());

            // Prueba la restricción en Mongo, no solo la preconsulta de la API.
            var duplicateDocument = accountDocument.DeepClone().AsBsonDocument;
            duplicateDocument["_id"] = Guid.NewGuid().ToString("N");
            var duplicateWrite = await Assert.ThrowsAsync<MongoWriteException>(() =>
                accounts.InsertOneAsync(duplicateDocument));
            Assert.Equal(11000, duplicateWrite.WriteError.Code);

            using var duplicate = await http.PostAsJsonAsync(
                "/api/v1/auth/registro",
                ValidPayload(email.ToUpperInvariant()));

            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            Assert.Equal(
                1,
                await accounts.CountDocumentsAsync(
                    new BsonDocument(
                        MongoIdentitySchema.AccountsNormalizedEmailField,
                        email)));
            Assert.Single(factory.EmailSender.Messages, message => message.Email == email);

            var sent = factory.EmailSender.Messages.Single(message => message.Email == email);
            var protector = factory.Services.GetRequiredService<IVerificationCodeProtector>();
            var accountRepository = factory.Services.GetRequiredService<IAccountRepository>();
            var account = await accountRepository.FindByEmailAsync(EmailAddress.Create(email));

            Assert.NotNull(account);
            Assert.Equal(
                EmailConfirmationResult.Confirmed,
                account.ConfirmEmail(
                    protector.Protect(sent.Code),
                    DateTimeOffset.UtcNow));

            await accountRepository.UpdateAsync(account);

            using var login = await http.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { correo = email, contrasena = Password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            var original = await AuthTestHelpers.ReadTokensAsync(login);
            var sessionId = AuthTestHelpers.GetSessionId(original.RefreshToken);
            var sessionDocument = await sessions
                .Find(new BsonDocument("_id", sessionId.ToString("N")))
                .FirstOrDefaultAsync();

            Assert.NotNull(sessionDocument);
            Assert.Equal(
                Sha256Hex(original.RefreshToken),
                sessionDocument["hashRefreshActual"].AsString);
            Assert.NotEqual(original.RefreshToken, sessionDocument["hashRefreshActual"].AsString);
            Assert.Empty(sessionDocument["tokensUsados"].AsBsonArray);

            factory.RotationBarrier.Arm();
            var firstRefresh = http.PostAsJsonAsync(
                "/api/v1/auth/refresh",
                new { refreshToken = original.RefreshToken });
            var secondRefresh = http.PostAsJsonAsync(
                "/api/v1/auth/refresh",
                new { refreshToken = original.RefreshToken });

            using var firstResponse = await firstRefresh;
            using var secondResponse = await secondRefresh;
            Assert.Equal(2, factory.RotationBarrier.Arrivals);

            var statuses = new[] { firstResponse.StatusCode, secondResponse.StatusCode };
            Assert.Contains(HttpStatusCode.OK, statuses);
            Assert.Contains(HttpStatusCode.Unauthorized, statuses);

            var winner = firstResponse.StatusCode == HttpStatusCode.OK
                ? firstResponse
                : secondResponse;
            var winnerTokens = await AuthTestHelpers.ReadTokensAsync(winner);

            using var afterRace = await http.PostAsJsonAsync(
                "/api/v1/auth/refresh",
                new { refreshToken = winnerTokens.RefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, afterRace.StatusCode);

            using var secondLogin = await http.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { correo = email, contrasena = Password });
            Assert.Equal(HttpStatusCode.OK, secondLogin.StatusCode);
            var logoutTokens = await AuthTestHelpers.ReadTokensAsync(secondLogin);
            var logoutSessionId = AuthTestHelpers.GetSessionId(logoutTokens.RefreshToken);

            using var logoutRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/auth/logout");
            logoutRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", logoutTokens.AccessToken);

            using var logout = await http.SendAsync(logoutRequest);
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

            var revokedSession = await sessions
                .Find(new BsonDocument("_id", logoutSessionId.ToString("N")))
                .FirstOrDefaultAsync();
            Assert.NotNull(revokedSession);
            Assert.True(revokedSession.Contains("revocadaEnUtc"));
            Assert.False(revokedSession["revocadaEnUtc"].IsBsonNull);

            using var refreshAfterLogout = await http.PostAsJsonAsync(
                "/api/v1/auth/refresh",
                new { refreshToken = logoutTokens.RefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);

            var sessionIndexes = await ListIndexesAsync(sessions);
            var ttlIndex = Assert.Single(
                sessionIndexes,
                index => GetName(index) == MongoIdentitySchema.SessionsExpirationTtlIndex);
            Assert.Equal(0, ttlIndex["expireAfterSeconds"].ToInt64());

            var accountIndex = Assert.Single(
                sessionIndexes,
                index => GetName(index) == MongoIdentitySchema.SessionsAccountIndex);
            Assert.Equal(1, accountIndex["key"]["cuentaId"].ToInt32());

            // La auditoría M02–M13 se ejecuta por separado: M01 también funciona
            // en una base aislada con solo cuentas, sesiones y ledger.
            Assert.Empty((await migrationRunner.PlanAsync()).Writes);
        }
        finally
        {
            await CleanupAsync(database, email, accountId);
        }
    }

    private static Dictionary<string, object?> ValidPayload(string email) =>
        new()
        {
            ["nombreCompleto"] = "Cuenta de integración",
            ["correo"] = email,
            ["fechaNacimiento"] = "2004-05-10",
            ["genero"] = "Mujer",
            ["contrasena"] = Password,
            ["confirmarContrasena"] = Password,
            ["aceptoAvisoPrivacidad"] = true,
            ["versionAvisoPrivacidad"] = "2026-01"
        };

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

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static async Task CleanupAsync(
        IMongoDatabase database,
        string email,
        Guid? accountId)
    {
        var accounts = database.GetCollection<BsonDocument>(
            MongoIdentitySchema.AccountsCollection);
        var sessions = database.GetCollection<BsonDocument>(
            MongoIdentitySchema.SessionsCollection);

        var filters = new List<BsonDocument>
        {
            new(MongoIdentitySchema.AccountsNormalizedEmailField, email)
        };

        if (accountId.HasValue)
        {
            filters.Add(new BsonDocument("_id", accountId.Value.ToString("N")));
        }

        foreach (var filter in filters)
        {
            var documents = await accounts.Find(filter).ToListAsync();
            foreach (var document in documents)
            {
                var id = document["_id"].AsString;
                await sessions.DeleteManyAsync(
                    new BsonDocument(MongoIdentitySchema.SessionsAccountIdField, id));
                await accounts.DeleteOneAsync(new BsonDocument("_id", id));
            }
        }
    }
}
