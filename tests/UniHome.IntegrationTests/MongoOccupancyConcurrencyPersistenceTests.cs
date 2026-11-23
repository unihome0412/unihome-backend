using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Universities;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
using UniHome.Infrastructure.Persistence.Mongo.OccupancyLinks;
using UniHome.Infrastructure.Persistence.Mongo.Publications;
using UniHome.Infrastructure.Persistence.Mongo.Universities;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Pruebas reales de concurrencia de M09: la transición Activo -> Finalizado
/// debe liberar cupo exactamente una vez, y la reactivación no debe inflar el
/// contador. Usan MongoDB real, no dobles en memoria.
/// </summary>
public sealed class MongoOccupancyConcurrencyPersistenceTests
{
    private static readonly string[] SingleFurniture = ["Cama"];

    [MongoIntegrationFact]
    public async Task SalidaConcurrente_DelMismoInquilino_LiberaUnaSolaVez()
    {
        await using var scenario = await Scenario.CreateAsync(places: 2, initialTenants: 2);

        var statuses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => scenario.LeaveAsync(scenario.TenantAId)));

        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.NoContent));
        Assert.All(statuses, status => Assert.True(
            status is HttpStatusCode.NoContent or HttpStatusCode.NotFound,
            $"Estado inesperado: {status}."));
        await AssertCounterAndLinksAsync(scenario, expectedOccupied: 1, expectedActive: 1);

        Assert.Equal(HttpStatusCode.OK, await scenario.JoinAsync(scenario.TenantCId));
        Assert.Equal(HttpStatusCode.Conflict, await scenario.JoinAsync(scenario.TenantDId));
        await AssertCounterAndLinksAsync(scenario, expectedOccupied: 2, expectedActive: 2);
    }

    [MongoIntegrationFact]
    public async Task BajaConcurrente_DelMismoInquilino_LiberaUnaSolaVez()
    {
        await using var scenario = await Scenario.CreateAsync(places: 2, initialTenants: 2);

        var statuses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => scenario.RemoveAsync(scenario.OwnerId, scenario.TenantAId)));

        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.NoContent));
        Assert.All(statuses, status => Assert.True(
            status is HttpStatusCode.NoContent or HttpStatusCode.NotFound,
            $"Estado inesperado: {status}."));
        await AssertCounterAndLinksAsync(scenario, expectedOccupied: 1, expectedActive: 1);

        Assert.Equal(HttpStatusCode.OK, await scenario.JoinAsync(scenario.TenantCId));
        Assert.Equal(HttpStatusCode.Conflict, await scenario.JoinAsync(scenario.TenantDId));
        await AssertCounterAndLinksAsync(scenario, expectedOccupied: 2, expectedActive: 2);
    }

    [MongoIntegrationFact]
    public async Task SalidaYBajaConcurrentes_DelMismoInquilino_LiberanUnaSolaVez()
    {
        await using var scenario = await Scenario.CreateAsync(places: 2, initialTenants: 2);

        var leaveTasks = Enumerable.Range(0, 4)
            .Select(_ => scenario.LeaveAsync(scenario.TenantAId));
        var removeTasks = Enumerable.Range(0, 4)
            .Select(_ => scenario.RemoveAsync(scenario.OwnerId, scenario.TenantAId));
        var statuses = await Task.WhenAll(leaveTasks.Concat(removeTasks));

        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.NoContent));
        Assert.All(statuses, status => Assert.True(
            status is HttpStatusCode.NoContent or HttpStatusCode.NotFound,
            $"Estado inesperado: {status}."));
        await AssertCounterAndLinksAsync(scenario, expectedOccupied: 1, expectedActive: 1);

        Assert.Equal(HttpStatusCode.OK, await scenario.JoinAsync(scenario.TenantCId));
        Assert.Equal(HttpStatusCode.Conflict, await scenario.JoinAsync(scenario.TenantDId));
        await AssertCounterAndLinksAsync(scenario, expectedOccupied: 2, expectedActive: 2);
    }

    [MongoIntegrationFact]
    public async Task ReactivacionConcurrente_DelMismoInquilino_ReservaUnaSolaVez()
    {
        await using var scenario = await Scenario.CreateAsync(places: 2, initialTenants: 1);

        Assert.Equal(HttpStatusCode.NoContent, await scenario.LeaveAsync(scenario.TenantAId));
        await AssertCounterAndLinksAsync(scenario, expectedOccupied: 0, expectedActive: 0);

        var statuses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => scenario.JoinAsync(scenario.TenantAId)));

        Assert.Contains(HttpStatusCode.OK, statuses);
        Assert.All(statuses, status => Assert.True(
            status is HttpStatusCode.OK or HttpStatusCode.Conflict,
            $"Estado inesperado: {status}."));
        await AssertCounterAndLinksAsync(scenario, expectedOccupied: 1, expectedActive: 1);

        Assert.Equal(HttpStatusCode.OK, await scenario.JoinAsync(scenario.TenantBId));
        Assert.Equal(HttpStatusCode.Conflict, await scenario.JoinAsync(scenario.TenantCId));
        await AssertCounterAndLinksAsync(scenario, expectedOccupied: 2, expectedActive: 2);
    }

    private static async Task AssertCounterAndLinksAsync(
        Scenario scenario,
        int expectedOccupied,
        int expectedActive)
    {
        Assert.Equal(expectedOccupied, await scenario.GetOccupiedPlacesAsync());
        Assert.Equal(expectedActive, await scenario.CountActiveLinksAsync());
    }

    private sealed class Scenario : IAsyncDisposable
    {
        private readonly MongoClient _mongoClient;
        private readonly IMongoDatabase _database;
        private Guid? _propertyId;
        private Guid? _unitId;
        private string? _code;

        private Scenario(MongoClient mongoClient, IMongoDatabase database)
        {
            _mongoClient = mongoClient;
            _database = database;
            Factory = new MongoBackedApiFactory();
        }

        public MongoBackedApiFactory Factory { get; }
        public Guid OwnerId { get; } = Guid.NewGuid();
        public Guid TenantAId { get; } = Guid.NewGuid();
        public Guid TenantBId { get; } = Guid.NewGuid();
        public Guid TenantCId { get; } = Guid.NewGuid();
        public Guid TenantDId { get; } = Guid.NewGuid();
        public Guid UniversityId { get; } = Guid.NewGuid();

        private Guid UnitId =>
            _unitId ?? throw new InvalidOperationException("La unidad aún no se creó.");

        private string Code =>
            _code ?? throw new InvalidOperationException("La invitación aún no se creó.");

        public static async Task<Scenario> CreateAsync(int places, int initialTenants)
        {
            var mongoClient = MongoIntegrationEnvironment.CreateClient();
            var database = MongoIntegrationEnvironment.GetDatabase(mongoClient);
            var scenario = new Scenario(mongoClient, database);

            try
            {
                var migration = new MongoMigrationRunner(
                    database,
                    MongoOccupancyMigration.Definition);
                Assert.Empty((await migration.PlanAsync()).Writes);
                await scenario.InitializeAsync(places, initialTenants);
                return scenario;
            }
            catch
            {
                await scenario.DisposeAsync();
                throw;
            }
        }

        public HttpClient CreateClient(Guid accountId)
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                JwtTestTokenFactory.Create(accountId, Guid.NewGuid()));
            return client;
        }

        public async Task<HttpStatusCode> JoinAsync(Guid tenantAccountId)
        {
            using var client = CreateClient(tenantAccountId);
            using var response = await client.PostAsJsonAsync(
                "/api/v1/unidades/unirse",
                new { codigo = Code });
            return response.StatusCode;
        }

        public async Task<HttpStatusCode> LeaveAsync(Guid tenantAccountId)
        {
            using var client = CreateClient(tenantAccountId);
            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/v1/unidades/{UnitId}/mi-vinculo")
            {
                Content = JsonContent.Create(new { confirmacion = true })
            };
            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }

        public async Task<HttpStatusCode> RemoveAsync(Guid ownerAccountId, Guid tenantAccountId)
        {
            using var client = CreateClient(ownerAccountId);
            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/v1/unidades/{UnitId}/inquilinos/{tenantAccountId}")
            {
                Content = JsonContent.Create(new { confirmacion = true })
            };
            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }

        public async Task<int> GetOccupiedPlacesAsync()
        {
            var document = await _database
                .GetCollection<BsonDocument>(MongoPublicationSchema.UnitsCollection)
                .Find(new BsonDocument("_id", UnitId.ToString("N")))
                .FirstOrDefaultAsync();
            Assert.NotNull(document);
            return document.Contains(MongoPublicationSchema.UnitOccupiedPlacesField)
                ? document[MongoPublicationSchema.UnitOccupiedPlacesField].ToInt32()
                : 0;
        }

        public async Task<int> CountActiveLinksAsync() =>
            (int)await _database
                .GetCollection<BsonDocument>(MongoOccupancySchema.LinksCollection)
                .CountDocumentsAsync(new BsonDocument
                {
                    ["unitId"] = UnitId.ToString("N"),
                    ["estado"] = "Activo"
                });

        private async Task InitializeAsync(int places, int initialTenants)
        {
            var accounts = Factory.Services.GetRequiredService<IAccountRepository>();
            var universities = Factory.Services.GetRequiredService<IUniversityRepository>();
            var accountIds = new[]
            {
                OwnerId,
                TenantAId,
                TenantBId,
                TenantCId,
                TenantDId
            };
            var accountEmails = new[]
            {
                "owner",
                "tenant-a",
                "tenant-b",
                "tenant-c",
                "tenant-d"
            };
            await DeleteAsync(MongoIdentitySchema.AccountsCollection, accountIds);
            await DeleteAsync(MongoUniversitySchema.Collection, [UniversityId]);

            for (var index = 0; index < accountIds.Length; index++)
            {
                await accounts.AddAsync(TestAccountFactory.Create(
                    $"mongo.occupancy.{accountEmails[index]}.{Guid.NewGuid():N}@example.invalid",
                    id: accountIds[index]));
            }

            await universities.AddAsync(University.Restore(
                UniversityId,
                $"Universidad M09 concurrencia {Guid.NewGuid():N}",
                true,
                DateTimeOffset.UtcNow));

            using var ownerClient = CreateClient(OwnerId);
            using var propertyResponse = await ownerClient.PostAsJsonAsync(
                "/api/v1/propiedades",
                new
                {
                    nombre = $"Casa M09 concurrencia {Guid.NewGuid():N}",
                    direccion = $"Av. M09 {Guid.NewGuid():N}",
                    colonia = "Centro",
                    tipoPropiedad = "Casa",
                    universidadId = UniversityId,
                    latitud = 19.4326,
                    longitud = -99.1332
                });
            Assert.Equal(HttpStatusCode.Created, propertyResponse.StatusCode);
            using var propertyJson = JsonDocument.Parse(
                await propertyResponse.Content.ReadAsStringAsync());
            _propertyId = propertyJson.RootElement.GetProperty("id").GetGuid();

            using var unitResponse = await ownerClient.PostAsJsonAsync(
                $"/api/v1/propiedades/{_propertyId}/unidades",
                new
                {
                    tipoUnidad = "Habitacion",
                    ocupacion = places > 1 ? "Compartida" : "Individual",
                    lugares = places,
                    precioMensualPorLugarMxn = 3500,
                    tamanoM2 = 12,
                    paraQuien = "Mixto",
                    bano = "Privado",
                    mobiliario = "Amueblado",
                    muebles = SingleFurniture
                });
            Assert.Equal(HttpStatusCode.Created, unitResponse.StatusCode);
            using var unitJson = JsonDocument.Parse(await unitResponse.Content.ReadAsStringAsync());
            _unitId = unitJson.RootElement.GetProperty("id").GetGuid();

            using var invitationResponse = await ownerClient.GetAsync(
                $"/api/v1/unidades/{_unitId}/invitacion");
            Assert.Equal(HttpStatusCode.OK, invitationResponse.StatusCode);
            using var invitationJson = JsonDocument.Parse(
                await invitationResponse.Content.ReadAsStringAsync());
            _code = invitationJson.RootElement.GetProperty("codigo").GetString();
            Assert.False(string.IsNullOrWhiteSpace(_code));

            var tenants = new[] { TenantAId, TenantBId };
            for (var index = 0; index < initialTenants; index++)
            {
                Assert.Equal(HttpStatusCode.OK, await JoinAsync(tenants[index]));
            }
        }

        private async Task DeleteAsync(string collectionName, IEnumerable<Guid> ids)
        {
            var collection = _database.GetCollection<BsonDocument>(collectionName);
            foreach (var id in ids)
            {
                await collection.DeleteOneAsync(new BsonDocument("_id", id.ToString("N")));
            }
        }

        private async Task CleanupAsync()
        {
            if (_unitId is Guid unitId)
            {
                await _database
                    .GetCollection<BsonDocument>(MongoOccupancySchema.LinksCollection)
                    .DeleteManyAsync(new BsonDocument("unitId", unitId.ToString("N")));
                await _database
                    .GetCollection<BsonDocument>(MongoOccupancySchema.InvitationsCollection)
                    .DeleteManyAsync(new BsonDocument("unitId", unitId.ToString("N")));
                await DeleteAsync(MongoPublicationSchema.UnitsCollection, [unitId]);
            }

            if (_propertyId is Guid propertyId)
            {
                await DeleteAsync(MongoPublicationSchema.PropertiesCollection, [propertyId]);
            }

            await DeleteAsync(MongoUniversitySchema.Collection, [UniversityId]);
            await DeleteAsync(MongoIdentitySchema.AccountsCollection, [
                OwnerId,
                TenantAId,
                TenantBId,
                TenantCId,
                TenantDId
            ]);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await CleanupAsync();
            }
            finally
            {
                try
                {
                    Factory.Dispose();
                }
                finally
                {
                    _mongoClient.Dispose();
                }
            }
        }
    }
}
