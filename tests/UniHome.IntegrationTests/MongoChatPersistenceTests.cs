using System.Globalization;
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
/// base aislada y elimina solo sus documentos. Cubre inicio de conversación,
/// envío/lectura, paginación por cursor y aislamiento entre participantes.
/// </summary>
public sealed class MongoChatPersistenceTests
{
    private const string OwnerEmail = "mongo.chat.owner@example.invalid";
    private const string RequesterEmail = "mongo.chat.requester@example.invalid";
    private const string OutsiderEmail = "mongo.chat.outsider@example.invalid";
    private const string EncryptedNotice = "Comunicación cifrada en tránsito.";
    private static readonly string[] SingleFurniture = ["Cama"];

    [MongoIntegrationFact]
    public async Task FlujoM08_DebePersistirConversacionMensajesPaginacionYAutorizacion()
    {
        using var client = MongoIntegrationEnvironment.CreateClient();
        var database = MongoIntegrationEnvironment.GetDatabase(client);
        var migration = new MongoMigrationRunner(database, MongoChatMigration.Definition);
        Assert.Empty((await migration.PlanAsync()).Writes);

        var owner = TestAccountFactory.Create(OwnerEmail);
        var requester = TestAccountFactory.Create(RequesterEmail);
        var outsider = TestAccountFactory.Create(OutsiderEmail);
        var university = University.Restore(
            Guid.NewGuid(),
            $"Universidad M08 {Guid.NewGuid():N}",
            true,
            DateTimeOffset.UtcNow);
        var accountIds = new[] { owner.Id, requester.Id, outsider.Id };
        var universityIds = new[] { university.Id };
        Guid? propertyId = null;
        Guid? unitId = null;
        Guid? conversationId = null;

        using var factory = new MongoBackedApiFactory();
        using var httpOwner = factory.CreateClient();
        using var httpRequester = factory.CreateClient();
        using var httpOutsider = factory.CreateClient();
        var accounts = factory.Services.GetRequiredService<IAccountRepository>();
        var universities = factory.Services.GetRequiredService<IUniversityRepository>();
        await DeleteAsync(database, MongoIdentitySchema.AccountsCollection, accountIds);
        await DeleteAsync(database, MongoUniversitySchema.Collection, universityIds);
        await accounts.AddAsync(owner);
        await accounts.AddAsync(requester);
        await accounts.AddAsync(outsider);
        await universities.AddAsync(university);

        httpOwner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestTokenFactory.Create(owner.Id, Guid.NewGuid()));
        httpRequester.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestTokenFactory.Create(requester.Id, Guid.NewGuid()));
        httpOutsider.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JwtTestTokenFactory.Create(outsider.Id, Guid.NewGuid()));

        try
        {
            using var propertyResponse = await httpOwner.PostAsJsonAsync(
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

            using var unitResponse = await httpOwner.PostAsJsonAsync(
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

            using var start = await httpRequester.PostAsJsonAsync(
                $"/api/v1/unidades/{unitId}/conversaciones", new { });
            Assert.Equal(HttpStatusCode.OK, start.StatusCode);
            using var startJson = JsonDocument.Parse(await start.Content.ReadAsStringAsync());
            conversationId = startJson.RootElement.GetProperty("conversacionId").GetGuid();

            // Iniciar de nuevo la misma conversación es idempotente.
            using var startAgain = await httpRequester.PostAsJsonAsync(
                $"/api/v1/unidades/{unitId}/conversaciones", new { });
            Assert.Equal(HttpStatusCode.OK, startAgain.StatusCode);
            using var startAgainJson = JsonDocument.Parse(
                await startAgain.Content.ReadAsStringAsync());
            Assert.Equal(
                conversationId,
                startAgainJson.RootElement.GetProperty("conversacionId").GetGuid());

            await SendMessageAsync(httpRequester, conversationId.Value, "Hola, ¿sigue disponible?");
            await SendMessageAsync(httpOwner, conversationId.Value, "Sí, sigue disponible.");
            await SendMessageAsync(httpRequester, conversationId.Value, "¿Cuál es el horario?");
            await SendMessageAsync(httpOwner, conversationId.Value, "Después de las 5.");

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
                "Después de las 5.",
                storedConversation[MongoChatSchema.ConversationLastMessagePreviewField].AsString);

            var messages = database.GetCollection<BsonDocument>(MongoChatSchema.MessagesCollection);
            var storedMessages = await messages
                .Find(new BsonDocument(
                    MongoChatSchema.MessageConversationField,
                    conversationId.Value.ToString("N")))
                .ToListAsync();
            Assert.Equal(4, storedMessages.Count);
            Assert.Contains(storedMessages, message =>
                string.Equals(
                    message[MongoChatSchema.MessageSenderField].AsString,
                    owner.Id.ToString("N"),
                    StringComparison.Ordinal));
            Assert.Contains(storedMessages, message =>
                string.Equals(
                    message[MongoChatSchema.MessageSenderField].AsString,
                    requester.Id.ToString("N"),
                    StringComparison.Ordinal));

            var ownerPage = await GetPageAsync(httpOwner, conversationId.Value, limit: 20);
            Assert.Equal(4, ownerPage.Ids.Count);
            Assert.Equal(EncryptedNotice, ownerPage.Notice);

            using var conversationsResponse = await httpOwner.GetAsync("/api/v1/conversaciones");
            Assert.Equal(HttpStatusCode.OK, conversationsResponse.StatusCode);
            using var conversationsJson = JsonDocument.Parse(
                await conversationsResponse.Content.ReadAsStringAsync());
            Assert.Contains(
                conversationsJson.RootElement.EnumerateArray(),
                item => item.GetProperty("conversacionId").GetGuid() == conversationId.Value);

            var firstPage = await GetPageAsync(httpRequester, conversationId.Value, limit: 2);
            Assert.Equal(2, firstPage.Ids.Count);
            Assert.NotNull(firstPage.CursorDate);
            Assert.NotNull(firstPage.CursorId);
            Assert.Equal(EncryptedNotice, firstPage.Notice);

            var secondPage = await GetPageAsync(
                httpRequester,
                conversationId.Value,
                limit: 2,
                before: firstPage.CursorDate,
                beforeId: firstPage.CursorId);
            Assert.Equal(2, secondPage.Ids.Count);
            Assert.Equal(EncryptedNotice, secondPage.Notice);

            var allIds = firstPage.Ids.Concat(secondPage.Ids).ToArray();
            Assert.Equal(4, allIds.Distinct().Count());
            Assert.Empty(firstPage.Ids.Intersect(secondPage.Ids));

            using var outsiderRead = await httpOutsider.GetAsync(
                $"/api/v1/conversaciones/{conversationId}/mensajes");
            Assert.Equal(HttpStatusCode.Forbidden, outsiderRead.StatusCode);

            using var outsiderSend = await httpOutsider.PostAsJsonAsync(
                $"/api/v1/conversaciones/{conversationId}/mensajes",
                new { texto = "No debería poder escribir." });
            Assert.Equal(HttpStatusCode.Forbidden, outsiderSend.StatusCode);
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

    private static async Task<Guid> SendMessageAsync(
        HttpClient client,
        Guid conversationId,
        string text)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/conversaciones/{conversationId}/mensajes",
            new { texto = text });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            EncryptedNotice,
            json.RootElement.GetProperty("aviso").GetString());

        return json.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<MessagePage> GetPageAsync(
        HttpClient client,
        Guid conversationId,
        int limit,
        DateTimeOffset? before = null,
        Guid? beforeId = null)
    {
        var path = $"/api/v1/conversaciones/{conversationId}/mensajes?limite={limit}";

        if (before is not null && beforeId is not null)
        {
            path +=
                $"&antes={Uri.EscapeDataString(before.Value.ToString("O", CultureInfo.InvariantCulture))}" +
                $"&antesId={beforeId.Value:N}";
        }

        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        var ids = root.GetProperty("mensajes")
            .EnumerateArray()
            .Select(message => message.GetProperty("id").GetGuid())
            .ToArray();
        DateTimeOffset? cursorDate = null;
        Guid? cursorId = null;

        if (root.GetProperty("siguienteCursor").ValueKind != JsonValueKind.Null)
        {
            var cursor = root.GetProperty("siguienteCursor");
            cursorDate = cursor.GetProperty("fecha").GetDateTimeOffset();
            cursorId = cursor.GetProperty("mensajeId").GetGuid();
        }

        return new MessagePage(
            ids,
            cursorDate,
            cursorId,
            root.GetProperty("aviso").GetString() ?? string.Empty);
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

    private sealed record MessagePage(
        IReadOnlyList<Guid> Ids,
        DateTimeOffset? CursorDate,
        Guid? CursorId,
        string Notice);
}
