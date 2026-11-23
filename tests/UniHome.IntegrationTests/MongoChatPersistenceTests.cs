using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using UniHome.Application.Identity.Abstractions;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.Domain.Universities;
using UniHome.Infrastructure.Persistence.Mongo;
using UniHome.Infrastructure.Persistence.Mongo.Chat;
using UniHome.Infrastructure.Persistence.Mongo.Migrations;
using UniHome.Infrastructure.Persistence.Mongo.Publications;
using UniHome.Infrastructure.Persistence.Mongo.Universities;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Prueba real y opt-in de M08 contra MongoDB: exige M08-001 aplicada, usa una
/// base aislada y elimina solo sus documentos.
/// </summary>
public sealed class MongoChatPersistenceTests
{
    private const string OwnerEmail = "mongo.chat.owner@example.invalid";
    private const string RequesterEmail = "mongo.chat.requester@example.invalid";
    private static readonly string[] SingleFurniture = ["Cama"];

    [MongoIntegrationFact]
    public async Task FlujoM08_DebePersistirConversacionYMensajesConParticipantes()
    {
        using var client = MongoIntegrationEnvironment.CreateClient();
        var database = MongoIntegrationEnvironment.GetDatabase(client);
        var migration = new MongoMigrationRunner(database, MongoChatMigration.Definition);
        Assert.Empty((await migration.PlanAsync()).Writes);

        var owner = TestAccountFactory.Create(OwnerEmail);
        var requester = TestAccountFactory.Create(RequesterEmail);
        var university = University.Restore(
            Guid.NewGuid(),
            $"Universidad M08 {Guid.NewGuid():N}",
            true,
            DateTimeOffset.UtcNow);
        var accountIds = new[] { owner.Id, requester.Id };
        var universityIds = new[] { university.Id };
        Guid? propertyId = null;
        Guid? unitId = null;
        Guid? conversationId = null;

        using var factory = new MongoBackedApiFactory();
        using var http = factory.CreateClient();
        var accounts = factory.Services.GetRequiredService<IAccountRepository>();
        var universities = factory.Services.GetRequiredService<IUniversityRepository>();
        await DeleteAsync(database, MongoIdentitySchema.AccountsCollection, accountIds);
        await DeleteAsync(database, MongoUniversitySchema.Collection, universityIds);
        await accounts.AddAsync(owner);
        await accounts.AddAsync(requester);
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
                    nombre = "Casa M08",
                    direccion = "Av. M08 100",
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
                    muebles = SingleFurniture
                });
            Assert.Equal(HttpStatusCode.Created, unitResponse.StatusCode);
            using var unitJson = JsonDocument.Parse(await unitResponse.Content.ReadAsStringAsync());
            unitId = unitJson.RootElement.GetProperty("id").GetGuid();

            var units = database.GetCollection<BsonDocument>(MongoPublicationSchema.UnitsCollection);
            var approveResult = await units.UpdateOneAsync(
                new BsonDocument("_id", unitId.Value.ToString("N")),
                new BsonDocument("$set", new BsonDocument(
                    MongoPublicationSchema.UnitStateField,
                    PublicationState.Aprobada.ToString())));
            Assert.Equal(1, approveResult.ModifiedCount);

            using var httpRequester = factory.CreateClient();
            httpRequester.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                JwtTestTokenFactory.Create(requester.Id, Guid.NewGuid()));

            using var start = await httpRequester.PostAsJsonAsync(
                $"/api/v1/unidades/{unitId}/conversaciones", new { });
            Assert.Equal(HttpStatusCode.OK, start.StatusCode);
            using var startJson = JsonDocument.Parse(await start.Content.ReadAsStringAsync());
            conversationId = startJson.RootElement.GetProperty("conversacionId").GetGuid();

            using var send = await httpRequester.PostAsJsonAsync(
                $"/api/v1/conversaciones/{conversationId}/mensajes",
                new { texto = "Hola, ¿sigue disponible?" });
            Assert.Equal(HttpStatusCode.Created, send.StatusCode);

            var conversations = database.GetCollection<BsonDocument>(MongoChatSchema.ConversationsCollection);
            var storedConversation = await conversations
                .Find(new BsonDocument("_id", conversationId.Value.ToString("N")))
                .FirstOrDefaultAsync();
            Assert.NotNull(storedConversation);
            var participants = storedConversation[MongoChatSchema.ConversationParticipantsField]
                .AsBsonArray
                .Select(value => value.AsString)
                .ToArray();
            Assert.Contains(owner.Id.ToString("N"), participants);
            Assert.Contains(requester.Id.ToString("N"), participants);
            Assert.Equal(
                "Hola, ¿sigue disponible?",
                storedConversation[MongoChatSchema.ConversationLastMessagePreviewField].AsString);

            var messages = database.GetCollection<BsonDocument>(MongoChatSchema.MessagesCollection);
            var messageCount = await messages.CountDocumentsAsync(new BsonDocument(
                MongoChatSchema.MessageConversationField, conversationId.Value.ToString("N")));
            Assert.Equal(1, messageCount);
        }
        finally
        {
            if (conversationId is not null)
            {
                await DeleteAsync(
                    database, MongoChatSchema.ConversationsCollection, [conversationId.Value]);
                await database
                    .GetCollection<BsonDocument>(MongoChatSchema.MessagesCollection)
                    .DeleteManyAsync(new BsonDocument(
                        MongoChatSchema.MessageConversationField,
                        conversationId.Value.ToString("N")));
            }

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
