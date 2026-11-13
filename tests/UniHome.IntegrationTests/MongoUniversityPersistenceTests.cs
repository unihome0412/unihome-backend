using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using UniHome.Application.Identity.Abstractions;
using UniHome.Domain.Universities;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
using UniHome.Infrastructure.Persistence.Mongo.Universities;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Prueba real y opt-in de M03 contra MongoDB: exige la migración M03-001 ya
/// aplicada, usa una base aislada, crea datos ficticios identificables y los
/// elimina por identificador. No modifica colecciones ajenas ni ejecuta DDL.
/// </summary>
public sealed class MongoUniversityPersistenceTests
{
    private const string AdminEmail = "mongo.university.admin@example.invalid";
    private const string NormalEmail = "mongo.university.user@example.invalid";

    [MongoIntegrationFact]
    public async Task CrearUniversidad_DebePersistirYAparecerEnBusquedaReal()
    {
        using var client = MongoIntegrationEnvironment.CreateClient();
        var database = MongoIntegrationEnvironment.GetDatabase(client);
        var migrationRunner = new MongoMigrationRunner(
            database,
            MongoUniversityMigration.Definition);

        var migrationPlan = await migrationRunner.PlanAsync();
        Assert.Empty(migrationPlan.Writes);

        var adminId = UniHomeApiFactory.TestAdminAccountId;
        var normalId = Guid.NewGuid();
        var accountIds = new[] { adminId, normalId };
        await DeleteAccountsAsync(database, accountIds);

        using var factory = new MongoBackedApiFactory();
        using var http = factory.CreateClient();
        var accounts = factory.Services.GetRequiredService<IAccountRepository>();
        var universities = database.GetCollection<BsonDocument>(
            MongoUniversitySchema.Collection);
        Guid? universityId = null;
        var name = $"Universidad Tecnológica {Guid.NewGuid():N}";

        try
        {
            await accounts.AddAsync(TestAccountFactory.Create(AdminEmail, id: adminId));
            await accounts.AddAsync(TestAccountFactory.Create(NormalEmail, id: normalId));

            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                JwtTestTokenFactory.Create(adminId, Guid.NewGuid()));

            using var created = await http.PostAsJsonAsync(
                "/api/v1/admin/universidades",
                new { nombreYCampus = name });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            universityId = createdJson.RootElement.GetProperty("id").GetGuid();
            Assert.Equal(
                name,
                createdJson.RootElement.GetProperty("nombreYCampus").GetString());
            Assert.True(createdJson.RootElement.GetProperty("activa").GetBoolean());

            var stored = await universities
                .Find(new BsonDocument("_id", universityId.Value.ToString("N")))
                .FirstOrDefaultAsync();
            Assert.NotNull(stored);
            Assert.Equal(name, stored["nombreYCampus"].AsString);
            Assert.Equal(
                University.Normalize(name),
                stored["nombreNormalizado"].AsString);
            Assert.True(stored["activa"].AsBoolean);

            using var search = await http.GetAsync(
                $"/api/v1/universidades?buscar={Uri.EscapeDataString(name)}");
            Assert.Equal(HttpStatusCode.OK, search.StatusCode);
            using var searchJson = JsonDocument.Parse(await search.Content.ReadAsStringAsync());
            Assert.Contains(
                searchJson.RootElement.EnumerateArray(),
                item => item.GetProperty("id").GetGuid() == universityId.Value);

            using var duplicate = await http.PostAsJsonAsync(
                "/api/v1/admin/universidades",
                new { nombreYCampus = $"  {name.ToUpperInvariant()}  " });
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                JwtTestTokenFactory.Create(normalId, Guid.NewGuid()));

            using var forbidden = await http.PostAsJsonAsync(
                "/api/v1/admin/universidades",
                new { nombreYCampus = $"Universidad No Autorizada {Guid.NewGuid():N}" });
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        finally
        {
            if (universityId is not null)
            {
                await universities.DeleteOneAsync(
                    new BsonDocument("_id", universityId.Value.ToString("N")));
            }

            await DeleteAccountsAsync(database, accountIds);
        }
    }

    private static async Task DeleteAccountsAsync(
        IMongoDatabase database,
        IEnumerable<Guid> accountIds)
    {
        var accounts = database.GetCollection<BsonDocument>(
            MongoIdentitySchema.AccountsCollection);

        foreach (var accountId in accountIds)
        {
            await accounts.DeleteOneAsync(
                new BsonDocument("_id", accountId.ToString("N")));
        }
    }
}
