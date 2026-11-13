using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Properties.Abstractions;
using UniHome.Application.Units.Abstractions;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.Domain.Universities;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Exploration;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
using UniHome.Infrastructure.Persistence.Mongo.Publications;
using UniHome.Infrastructure.Persistence.Mongo.Universities;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Prueba real opt-in de M04/M07 contra MongoDB: exige M05-001 y M07-001,
/// usa una base aislada y elimina únicamente sus documentos. DEC-04 sigue
/// pendiente, por lo que las fotos se representan con el doble de almacenamiento.
/// </summary>
public sealed class MongoExplorationPersistenceTests
{
    private const string Email = "mongo.exploration.owner@example.invalid";

    [MongoIntegrationFact]
    public async Task ExploracionYFavoritos_DebenPersistirYReflejarEstadoReal()
    {
        using var client = MongoIntegrationEnvironment.CreateClient();
        var database = MongoIntegrationEnvironment.GetDatabase(client);
        var migration = new MongoMigrationRunner(
            database,
            MongoExplorationMigration.Definition);
        Assert.Empty((await migration.PlanAsync()).Writes);

        var account = TestAccountFactory.Create(Email);
        var university = University.Restore(
            Guid.NewGuid(),
            $"Universidad M04 {Guid.NewGuid():N}",
            true,
            DateTimeOffset.UtcNow);
        var property = Property.Create(
            account.Id,
            "Casa M04",
            $"Calle M04 {Guid.NewGuid():N}",
            "Centro",
            PropertyType.Casa,
            university.Id,
            19.0045,
            -99.0,
            null,
            null,
            DateTimeOffset.UtcNow);
        var unit = Unit.Restore(
            Guid.NewGuid(),
            property.Id,
            account.Id,
            university.Id,
            UnitType.Habitacion,
            Occupancy.Individual,
            1,
            3000,
            12,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.Amueblado,
            ["Cama"],
            null,
            "Cerca",
            "No fumar",
            [
                ServiceCharge.Create(ServiceName.Agua, ServiceMode.Incluido, null),
                ServiceCharge.Create(ServiceName.Internet, ServiceMode.Aparte, 300)
            ],
            [
                new UnitPhoto("mongo-foto-1", "image/png", 10, "hash", 0, true, DateTimeOffset.UtcNow)
            ],
            PublicationState.Aprobada,
            DateTimeOffset.UtcNow,
            UnitAvailability.Disponible);

        using var factory = new MongoBackedApiFactory();
        using var http = factory.CreateClient();
        var accounts = factory.Services.GetRequiredService<IAccountRepository>();
        var universities = factory.Services.GetRequiredService<IUniversityRepository>();
        var properties = factory.Services.GetRequiredService<IPropertyRepository>();
        var units = factory.Services.GetRequiredService<IUnitRepository>();

        await DeleteAccountAsync(database, Email);
        await universities.AddAsync(university);
        await accounts.AddAsync(account);
        await properties.AddAsync(property);
        await units.AddAsync(unit);

        try
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                JwtTestTokenFactory.Create(account.Id, Guid.NewGuid()));

            using var list = await http.GetAsync(
                $"/api/v1/exploracion/lista?universidadId={university.Id}");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
            Assert.Single(listJson.RootElement.EnumerateArray());
            Assert.Equal(
                unit.Id,
                listJson.RootElement[0].GetProperty("id").GetGuid());

            using var card = await http.GetAsync($"/api/v1/unidades/{unit.Id}/tarjeta");
            Assert.Equal(HttpStatusCode.OK, card.StatusCode);
            var cardBody = await card.Content.ReadAsStringAsync();
            Assert.DoesNotContain("correo", cardBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("telefono", cardBody, StringComparison.OrdinalIgnoreCase);

            using var favoriteOne = await http.PutAsync(
                $"/api/v1/favoritos/{unit.Id}",
                null);
            using var favoriteTwo = await http.PutAsync(
                $"/api/v1/favoritos/{unit.Id}",
                null);
            Assert.Equal(HttpStatusCode.NoContent, favoriteOne.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, favoriteTwo.StatusCode);

            using var favorites = await http.GetAsync("/api/v1/favoritos");
            Assert.Equal(HttpStatusCode.OK, favorites.StatusCode);
            using var favoritesJson = JsonDocument.Parse(
                await favorites.Content.ReadAsStringAsync());
            Assert.Single(favoritesJson.RootElement.EnumerateArray());
            Assert.Equal(
                "Disponible",
                favoritesJson.RootElement[0].GetProperty("tarjeta")
                    .GetProperty("estadoActual").GetString());

            var unitsCollection = database.GetCollection<BsonDocument>(
                MongoPublicationSchema.UnitsCollection);
            var update = await unitsCollection.UpdateOneAsync(
                new BsonDocument("_id", unit.Id.ToString("N")),
                Builders<BsonDocument>.Update.Set("estadoOcupacion", "Ocupado"));
            Assert.Equal(1, update.ModifiedCount);

            using var afterState = await http.GetAsync("/api/v1/favoritos");
            using var afterStateJson = JsonDocument.Parse(
                await afterState.Content.ReadAsStringAsync());
            Assert.Single(afterStateJson.RootElement.EnumerateArray());
            Assert.Equal(
                "Ocupado",
                afterStateJson.RootElement[0].GetProperty("tarjeta")
                    .GetProperty("estadoActual").GetString());
        }
        finally
        {
            await database
                .GetCollection<BsonDocument>(MongoExplorationSchema.FavoritesCollection)
                .DeleteManyAsync(new BsonDocument(
                    "accountId",
                    account.Id.ToString("N")));
            await DeleteByIdAsync(
                database,
                MongoPublicationSchema.UnitsCollection,
                unit.Id);
            await DeleteByIdAsync(
                database,
                MongoPublicationSchema.PropertiesCollection,
                property.Id);
            await DeleteByIdAsync(
                database,
                MongoUniversitySchema.Collection,
                university.Id);
            await DeleteByIdAsync(
                database,
                MongoIdentitySchema.AccountsCollection,
                account.Id);
            await DeleteAccountAsync(database, Email);
        }
    }

    private static async Task DeleteByIdAsync(
        IMongoDatabase database,
        string collectionName,
        Guid id) =>
        await database
            .GetCollection<BsonDocument>(collectionName)
            .DeleteOneAsync(new BsonDocument("_id", id.ToString("N")));

    private static async Task DeleteAccountAsync(
        IMongoDatabase database,
        string email) =>
        await database
            .GetCollection<BsonDocument>(MongoIdentitySchema.AccountsCollection)
            .DeleteManyAsync(new BsonDocument(
                MongoIdentitySchema.AccountsNormalizedEmailField,
                email));
}
