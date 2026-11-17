using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;

namespace UniHome.IntegrationTests;

/// <summary>
/// POST /api/v1/unidades/{unitId}/fotos debe documentarse y ejercerse como
/// multipart/form-data real con el campo repetible Fotos, no como urlencoded.
/// </summary>
public sealed class OpenApiMultipartContractTests : IClassFixture<SwaggerEnabledApiFactory>
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFElEQVR42mP4z8DAAMb//0MZUA4Aep8J9x7TI9wAAAAASUVORK5CYII=");

    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public OpenApiMultipartContractTests(SwaggerEnabledApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAdminClient();
    }

    [Fact]
    public async Task OpenApi_FotosDeclaraMultipartConArrayDeBinariosYLimites()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/unidades/{unitId}/fotos")
            .GetProperty("post");

        var requestBody = operation.GetProperty("requestBody");
        Assert.True(requestBody.GetProperty("required").GetBoolean());
        var content = requestBody.GetProperty("content");

        Assert.True(content.TryGetProperty("multipart/form-data", out var multipart));
        Assert.False(content.TryGetProperty("application/x-www-form-urlencoded", out _));

        var schema = multipart.GetProperty("schema");
        Assert.Equal("object", schema.GetProperty("type").GetString());
        var photos = schema.GetProperty("properties").GetProperty("Fotos");
        Assert.Equal("array", photos.GetProperty("type").GetString());
        Assert.Equal(10, photos.GetProperty("maxItems").GetInt32());
        Assert.Equal(1, photos.GetProperty("minItems").GetInt32());
        var item = photos.GetProperty("items");
        Assert.Equal("string", item.GetProperty("type").GetString());
        Assert.Equal("binary", item.GetProperty("format").GetString());

        var encoding = multipart.GetProperty("encoding").GetProperty("Fotos");
        Assert.Equal("form", encoding.GetProperty("style").GetString());
        Assert.True(encoding.GetProperty("explode").GetBoolean());

        var security = operation.GetProperty("security");
        Assert.Contains(
            security.EnumerateArray(),
            requirement => requirement.TryGetProperty("Bearer", out _));

        var responses = operation.GetProperty("responses");
        foreach (var statusCode in new[] { "200", "400", "401", "403", "404", "409", "413", "415", "503" })
        {
            Assert.True(
                responses.TryGetProperty(statusCode, out _),
                $"Falta la respuesta {statusCode} en el contrato de fotos.");
        }
    }

    [Fact]
    public async Task Runtime_RechazaUrlencodedYAceptaMultipartReal()
    {
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);
        var universityId = Guid.NewGuid();

        var property = Property.Create(
            account.Id,
            $"Casa {Guid.NewGuid():N}",
            $"Calle {Guid.NewGuid():N}",
            "Centro",
            PropertyType.Casa,
            universityId,
            19.4326,
            -99.1332,
            null,
            null,
            DateTimeOffset.UtcNow);
        await _factory.Properties.AddAsync(property);

        var unit = Unit.Create(
            property.Id,
            account.Id,
            universityId,
            UnitType.Habitacion,
            Occupancy.Individual,
            1,
            3500,
            12,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.Amueblado,
            [],
            null,
            DateTimeOffset.UtcNow);
        await _factory.Units.AddAsync(unit);

        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        using var urlEncoded = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Fotos", "no-es-un-archivo")
        ]);

        using var rejected = await _client.PostAsync(
            $"/api/v1/unidades/{unit.Id}/fotos",
            urlEncoded);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, rejected.StatusCode);

        using var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(file, "Fotos", "foto.png");

        using var accepted = await _client.PostAsync(
            $"/api/v1/unidades/{unit.Id}/fotos",
            multipart);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        using var result = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
        Assert.Equal(1, result.RootElement.GetProperty("totalFotos").GetInt32());
    }
}
