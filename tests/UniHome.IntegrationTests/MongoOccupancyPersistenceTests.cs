using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Units.Abstractions;
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
/// Prueba real y opt-in de M09 contra MongoDB: exige M09-001 aplicada, usa una
/// base aislada y elimina solo sus documentos. Prueba el CAS de un solo
/// documento (<c>lugaresOcupados</c>) bajo concurrencia real, no simulada.
/// </summary>
public sealed class MongoOccupancyPersistenceTests
{
    private const string OwnerEmail = "mongo.occupancy.owner@example.invalid";
    private const string TenantAEmail = "mongo.occupancy.tenant.a@example.invalid";
    private const string TenantBEmail = "mongo.occupancy.tenant.b@example.invalid";

    [MongoIntegrationFact]
    public async Task TryReserveSlotAsync_DosSolicitudesSimultaneasConUnLugar_SoloUnaDebeReservar()
    {
        using var client = MongoIntegrationEnvironment.CreateClient();
        var database = MongoIntegrationEnvironment.GetDatabase(client);
        var migration = new MongoMigrationRunner(database, MongoOccupancyMigration.Definition);
        Assert.Empty((await migration.PlanAsync()).Writes);

        var owner = TestAccountFactory.Create(OwnerEmail);
        var tenantA = TestAccountFactory.Create(TenantAEmail);
        var tenantB = TestAccountFactory.Create(TenantBEmail);
        var university = University.Restore(
            Guid.NewGuid(),
            $"Universidad M09 {Guid.NewGuid():N}",
            true,
            DateTimeOffset.UtcNow);
        var accountIds = new[] { owner.Id, tenantA.Id, tenantB.Id };
        var universityIds = new[] { university.Id };
        Guid? propertyId = null;
        Guid? unitId = null;

        using var factory = new MongoBackedApiFactory();
        using var http = factory.CreateClient();
        var accounts = factory.Services.GetRequiredService<IAccountRepository>();
        var universities = factory.Services.GetRequiredService<IUniversityRepository>();
        await DeleteAsync(database, MongoIdentitySchema.AccountsCollection, accountIds);
        await DeleteAsync(database, MongoUniversitySchema.Collection, universityIds);
        await accounts.AddAsync(owner);
        await accounts.AddAsync(tenantA);
        await accounts.AddAsync(tenantB);
        await universities.AddAsync(university);

        try
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                JwtTestTokenFactory.Create(owner.Id, Guid.NewGuid()));

            using var propertyResponse = await http.PostAsJsonAsync(
                "/api/v1/propiedades",
                new
                {
                    nombre = "Casa M09",
                    direccion = "Av. M09 100",
                    colonia = "Centro",
                    tipoPropiedad = "Casa",
                    universidadId = university.Id,
                    latitud = 19.4326,
                    longitud = -99.1332
                });
            Assert.Equal(HttpStatusCode.Created, propertyResponse.StatusCode);
            using var propertyJson = JsonDocument.Parse(
                await propertyResponse.Content.ReadAsStringAsync());
            propertyId = propertyJson.RootElement.GetProperty("id").GetGuid();

            using var unitResponse = await http.PostAsJsonAsync(
                $"/api/v1/propiedades/{propertyId}/unidades",
                new
                {
                    tipoUnidad = "Habitacion",
                    ocupacion = "Individual",
                    lugares = 1,
                    precioMensualPorLugarMxn = 3500,
                    tamanoM2 = 12,
                    paraQuien = "Mixto",
                    bano = "Privado",
                    mobiliario = "Amueblado",
                    muebles = new[] { "Cama" }
                });
            Assert.Equal(HttpStatusCode.Created, unitResponse.StatusCode);
            using var unitJson = JsonDocument.Parse(await unitResponse.Content.ReadAsStringAsync());
            unitId = unitJson.RootElement.GetProperty("id").GetGuid();

            using var invitationResponse = await http.GetAsync(
                $"/api/v1/unidades/{unitId}/invitacion");
            Assert.Equal(HttpStatusCode.OK, invitationResponse.StatusCode);
            using var invitationJson = JsonDocument.Parse(
                await invitationResponse.Content.ReadAsStringAsync());
            var code = invitationJson.RootElement.GetProperty("codigo").GetString();

            using var httpTenantA = factory.CreateClient();
            httpTenantA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                JwtTestTokenFactory.Create(tenantA.Id, Guid.NewGuid()));
            using var httpTenantB = factory.CreateClient();
            httpTenantB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                JwtTestTokenFactory.Create(tenantB.Id, Guid.NewGuid()));

            // Concurrencia real contra MongoDB: ambas solicitudes llegan a la
            // vez a la misma unidad con un solo lugar. El CAS de un solo
            // documento (findOneAndUpdate con lugaresOcupados < lugares) debe
            // dejar pasar exactamente una.
            var responses = await Task.WhenAll(
                httpTenantA.PostAsJsonAsync("/api/v1/unidades/unirse", new { codigo = code }),
                httpTenantB.PostAsJsonAsync("/api/v1/unidades/unirse", new { codigo = code }));

            try
            {
                Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
                Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }

            var units = database.GetCollection<BsonDocument>(MongoPublicationSchema.UnitsCollection);
            var storedUnit = await units
                .Find(new BsonDocument("_id", unitId.Value.ToString("N")))
                .FirstOrDefaultAsync();
            Assert.NotNull(storedUnit);
            Assert.Equal(1, storedUnit["lugaresOcupados"].ToInt32());
            Assert.Equal("Ocupado", storedUnit["estadoOcupacion"].AsString);

            var links = database.GetCollection<BsonDocument>(MongoOccupancySchema.LinksCollection);
            var activeLinksCount = await links.CountDocumentsAsync(new BsonDocument
            {
                ["unitId"] = unitId.Value.ToString("N"),
                ["estado"] = "Activo"
            });
            Assert.Equal(1, activeLinksCount);
        }
        finally
        {
            if (unitId is not null)
            {
                await DeleteAsync(database, MongoPublicationSchema.UnitsCollection, [unitId.Value]);
                await database
                    .GetCollection<BsonDocument>(MongoOccupancySchema.LinksCollection)
                    .DeleteManyAsync(new BsonDocument("unitId", unitId.Value.ToString("N")));
                await database
                    .GetCollection<BsonDocument>(MongoOccupancySchema.InvitationsCollection)
                    .DeleteManyAsync(new BsonDocument("unitId", unitId.Value.ToString("N")));
            }

            if (propertyId is not null)
            {
                await DeleteAsync(
                    database,
                    MongoPublicationSchema.PropertiesCollection,
                    [propertyId.Value]);
            }

            await DeleteAsync(database, MongoUniversitySchema.Collection, universityIds);
            await DeleteAsync(database, MongoIdentitySchema.AccountsCollection, accountIds);
        }
    }

    [MongoIntegrationFact]
    public async Task TryReserveSlotAsync_UnidadSinCampoLugaresOcupados_LoTrataComoCero()
    {
        using var client = MongoIntegrationEnvironment.CreateClient();
        var database = MongoIntegrationEnvironment.GetDatabase(client);
        var migration = new MongoMigrationRunner(database, MongoOccupancyMigration.Definition);
        Assert.Empty((await migration.PlanAsync()).Writes);

        var owner = TestAccountFactory.Create("mongo.occupancy.legacy.owner@example.invalid");
        var university = University.Restore(
            Guid.NewGuid(),
            $"Universidad M09 legado {Guid.NewGuid():N}",
            true,
            DateTimeOffset.UtcNow);
        var accountIds = new[] { owner.Id };
        var universityIds = new[] { university.Id };
        Guid? propertyId = null;
        Guid? unitId = null;

        using var factory = new MongoBackedApiFactory();
        using var http = factory.CreateClient();
        var accounts = factory.Services.GetRequiredService<IAccountRepository>();
        var universities = factory.Services.GetRequiredService<IUniversityRepository>();
        var units = factory.Services.GetRequiredService<IUnitRepository>();
        await DeleteAsync(database, MongoIdentitySchema.AccountsCollection, accountIds);
        await DeleteAsync(database, MongoUniversitySchema.Collection, universityIds);
        await accounts.AddAsync(owner);
        await universities.AddAsync(university);

        try
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                JwtTestTokenFactory.Create(owner.Id, Guid.NewGuid()));

            using var propertyResponse = await http.PostAsJsonAsync(
                "/api/v1/propiedades",
                new
                {
                    nombre = "Casa M09 legado",
                    direccion = "Av. M09 legado 200",
                    colonia = "Centro",
                    tipoPropiedad = "Casa",
                    universidadId = university.Id,
                    latitud = 19.4326,
                    longitud = -99.1332
                });
            Assert.Equal(HttpStatusCode.Created, propertyResponse.StatusCode);
            using var propertyJson = JsonDocument.Parse(
                await propertyResponse.Content.ReadAsStringAsync());
            propertyId = propertyJson.RootElement.GetProperty("id").GetGuid();

            using var unitResponse = await http.PostAsJsonAsync(
                $"/api/v1/propiedades/{propertyId}/unidades",
                new
                {
                    tipoUnidad = "Habitacion",
                    ocupacion = "Individual",
                    lugares = 1,
                    precioMensualPorLugarMxn = 3500,
                    tamanoM2 = 12,
                    paraQuien = "Mixto",
                    bano = "Privado",
                    mobiliario = "Amueblado",
                    muebles = new[] { "Cama" }
                });
            Assert.Equal(HttpStatusCode.Created, unitResponse.StatusCode);
            using var unitJson = JsonDocument.Parse(await unitResponse.Content.ReadAsStringAsync());
            unitId = unitJson.RootElement.GetProperty("id").GetGuid();

            // Simula una unidad creada antes de M09-001, cuando el campo
            // lugaresOcupados todavía no existía.
            var unitsCollection = database.GetCollection<BsonDocument>(
                MongoPublicationSchema.UnitsCollection);
            var unsetResult = await unitsCollection.UpdateOneAsync(
                new BsonDocument("_id", unitId.Value.ToString("N")),
                new BsonDocument("$unset", new BsonDocument("lugaresOcupados", 1)));
            Assert.Equal(1, unsetResult.ModifiedCount);

            var reserved = await units.TryReserveSlotAsync(unitId.Value, propertyId.Value);
            Assert.Equal(1, reserved);

            var afterReserve = await unitsCollection
                .Find(new BsonDocument("_id", unitId.Value.ToString("N")))
                .FirstOrDefaultAsync();
            Assert.Equal(1, afterReserve["lugaresOcupados"].ToInt32());

            var released = await units.ReleaseSlotAsync(unitId.Value, propertyId.Value);
            Assert.Equal(0, released);

            // Soltar de nuevo sobre una unidad ya en 0 nunca debe quedar negativo.
            var releasedAgain = await units.ReleaseSlotAsync(unitId.Value, propertyId.Value);
            Assert.Equal(0, releasedAgain);

            var afterRelease = await unitsCollection
                .Find(new BsonDocument("_id", unitId.Value.ToString("N")))
                .FirstOrDefaultAsync();
            Assert.Equal(0, afterRelease["lugaresOcupados"].ToInt32());
        }
        finally
        {
            if (unitId is not null)
            {
                await DeleteAsync(database, MongoPublicationSchema.UnitsCollection, [unitId.Value]);
            }

            if (propertyId is not null)
            {
                await DeleteAsync(
                    database,
                    MongoPublicationSchema.PropertiesCollection,
                    [propertyId.Value]);
            }

            await DeleteAsync(database, MongoUniversitySchema.Collection, universityIds);
            await DeleteAsync(database, MongoIdentitySchema.AccountsCollection, accountIds);
        }
    }

    private static async Task DeleteAsync(
        IMongoDatabase database,
        string collectionName,
        IEnumerable<Guid> ids)
    {
        var collection = database.GetCollection<BsonDocument>(collectionName);

        foreach (var id in ids)
        {
            await collection.DeleteOneAsync(new BsonDocument("_id", id.ToString("N")));
        }
    }
}
