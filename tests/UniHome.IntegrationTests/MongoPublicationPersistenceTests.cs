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
using UniHome.Infrastructure.Persistence.Mongo.Publications;
using UniHome.Infrastructure.Persistence.Mongo.Universities;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Prueba real y opt-in de M05 contra MongoDB: exige M05-001 aplicada, usa una
/// base aislada y elimina solo sus documentos. Las fotos usan el doble de
/// almacenamiento porque DEC-04 sigue pendiente; el bloqueo se documenta aparte.
/// </summary>
public sealed class MongoPublicationPersistenceTests
{
    private const string Email = "mongo.publication.owner@example.invalid";
    private static readonly string[] FurnitureItems = ["Cama", "Escritorio"];

    [MongoIntegrationFact]
    public async Task FlujoM05_DebePersistirPropiedadUnidadServiciosZonaFotosYTerminos()
    {
        using var client = MongoIntegrationEnvironment.CreateClient();
        var database = MongoIntegrationEnvironment.GetDatabase(client);
        var migration = new MongoMigrationRunner(database, MongoPublicationMigration.Definition);
        Assert.Empty((await migration.PlanAsync()).Writes);

        var account = TestAccountFactory.Create(Email);
        var university = University.Restore(
            Guid.NewGuid(),
            $"Universidad M05 {Guid.NewGuid():N}",
            true,
            DateTimeOffset.UtcNow);
        var accountIds = new[] { account.Id };
        var universityIds = new[] { university.Id };
        Guid? propertyId = null;
        Guid? unitId = null;

        using var factory = new MongoBackedApiFactory();
        using var http = factory.CreateClient();
        var accounts = factory.Services.GetRequiredService<IAccountRepository>();
        var universities = factory.Services.GetRequiredService<IUniversityRepository>();
        await DeleteAsync(database, MongoIdentitySchema.AccountsCollection, accountIds);
        await database
            .GetCollection<BsonDocument>(MongoIdentitySchema.AccountsCollection)
            .DeleteManyAsync(new BsonDocument(
                MongoIdentitySchema.AccountsNormalizedEmailField,
                Email));
        await DeleteAsync(database, MongoUniversitySchema.Collection, universityIds);
        await accounts.AddAsync(account);
        await universities.AddAsync(university);

        try
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                JwtTestTokenFactory.Create(account.Id, Guid.NewGuid()));

            using var propertyResponse = await http.PostAsJsonAsync(
                "/api/v1/propiedades",
                new
                {
                    nombre = "Casa M05",
                    direccion = "Av. M05 100",
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
            Assert.Equal(
                "Borrador",
                propertyJson.RootElement.GetProperty("estadoPublicacion").GetString());

            using var unitResponse = await http.PostAsJsonAsync(
                $"/api/v1/propiedades/{propertyId}/unidades",
                new
                {
                    tipoUnidad = "Habitacion",
                    ocupacion = "Compartida",
                    lugares = 3,
                    precioMensualPorLugarMxn = 3500,
                    tamanoM2 = 12,
                    paraQuien = "Mixto",
                    bano = "Privado",
                    mobiliario = "Amueblado",
                    muebles = FurnitureItems
                });
            Assert.Equal(HttpStatusCode.Created, unitResponse.StatusCode);
            using var unitJson = JsonDocument.Parse(await unitResponse.Content.ReadAsStringAsync());
            unitId = unitJson.RootElement.GetProperty("id").GetGuid();
            Assert.Equal(3, unitJson.RootElement.GetProperty("lugares").GetInt32());

            using var photos = new MultipartFormDataContent();
            var photo = new ByteArrayContent(
                [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]);
            photo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            photos.Add(photo, "Fotos", "foto.png");
            using var photoResponse = await http.PostAsync(
                $"/api/v1/unidades/{unitId}/fotos",
                photos);
            Assert.Equal(HttpStatusCode.OK, photoResponse.StatusCode);

            using var servicesResponse = await http.PutAsJsonAsync(
                $"/api/v1/unidades/{unitId}/servicios",
                new
                {
                    servicios = new object[]
                    {
                        new { nombre = "Agua", modalidad = "Incluido" },
                        new
                        {
                            nombre = "Internet",
                            modalidad = "Aparte",
                            montoMensualMxn = 300
                        }
                    }
                });
            Assert.Equal(HttpStatusCode.OK, servicesResponse.StatusCode);

            using var zoneResponse = await http.PutAsJsonAsync(
                $"/api/v1/unidades/{unitId}/zona-reglas",
                new
                {
                    informacionZona = "Cerca del metro",
                    reglasConvivencia = "No fumar"
                });
            Assert.Equal(HttpStatusCode.OK, zoneResponse.StatusCode);

            using var previewResponse = await http.GetAsync(
                $"/api/v1/unidades/{unitId}/vista-previa");
            Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
            using var previewJson = JsonDocument.Parse(
                await previewResponse.Content.ReadAsStringAsync());
            Assert.Equal("Borrador", previewJson.RootElement.GetProperty("estadoPublicacion").GetString());
            Assert.NotNull(previewJson.RootElement.GetProperty("fotoPrincipalUrl").GetString());

            using var termsResponse = await http.PostAsJsonAsync(
                "/api/v1/publicaciones/primera/terminos",
                new { versionTerminos = "2026-11" });
            Assert.Equal(HttpStatusCode.OK, termsResponse.StatusCode);

            var properties = database.GetCollection<BsonDocument>(
                MongoPublicationSchema.PropertiesCollection);
            var units = database.GetCollection<BsonDocument>(
                MongoPublicationSchema.UnitsCollection);
            var terms = database.GetCollection<BsonDocument>(
                MongoPublicationSchema.TermsCollection);
            var storedProperty = await properties
                .Find(new BsonDocument("_id", propertyId.Value.ToString("N")))
                .FirstOrDefaultAsync();
            var storedUnit = await units
                .Find(new BsonDocument("_id", unitId.Value.ToString("N")))
                .FirstOrDefaultAsync();
            var storedTerms = await terms
                .Find(new BsonDocument("accountId", account.Id.ToString("N")))
                .FirstOrDefaultAsync();

            Assert.NotNull(storedProperty);
            Assert.NotNull(storedUnit);
            Assert.NotNull(storedTerms);
            Assert.Equal("Borrador", storedUnit["estadoPublicacion"].AsString);
            Assert.Single(storedUnit["fotos"].AsBsonArray);
            Assert.Equal(2, storedUnit["servicios"].AsBsonArray.Count);
            Assert.Equal("2026-11", storedTerms["versionTerminos"].AsString);
        }
        finally
        {
            if (propertyId is not null)
            {
                await DeleteAsync(
                    database,
                    MongoPublicationSchema.PropertiesCollection,
                    [propertyId.Value]);
            }

            if (unitId is not null)
            {
                await DeleteAsync(
                    database,
                    MongoPublicationSchema.UnitsCollection,
                    [unitId.Value]);
            }

            await database
                .GetCollection<BsonDocument>(MongoPublicationSchema.TermsCollection)
                .DeleteManyAsync(new BsonDocument(
                    "accountId",
                    account.Id.ToString("N")));
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
            await collection.DeleteOneAsync(
                new BsonDocument("_id", id.ToString("N")));
        }
    }
}
