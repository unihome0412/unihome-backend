using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniHome.Application.Units.Photos;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

public sealed class ConfiguredCloudinaryApiFactory : UniHomeApiFactory
{
    protected override bool UseCloudinaryStorage => true;
}

public sealed class ConfiguredCloudinaryPhotoEndpointTests
    : IClassFixture<ConfiguredCloudinaryApiFactory>
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFElEQVR42mP4z8DAAMb//0MZUA4Aep8J9x7TI9wAAAAASUVORK5CYII=");

    private readonly ConfiguredCloudinaryApiFactory _factory;
    private readonly HttpClient _client;

    public ConfiguredCloudinaryPhotoEndpointTests(ConfiguredCloudinaryApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Endpoint_ConConfiguracion_DebeUsarElAdaptadorCloudinaryReal()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("test-cloud", configuration["Cloudinary:CloudName"]);
        var storageType = _factory.Services.GetRequiredService<UniHome.Application.Units.Abstractions.IUnitPhotoStorage>().GetType().FullName;
        Assert.Equal("UniHome.Infrastructure.Storage.CloudinaryUnitPhotoStorage", storageType);
        var account = await AuthTestHelpers.SeedAccountAsync(
            _factory,
            AuthTestHelpers.UniqueEmail());
        using var login = await AuthTestHelpers.LoginAsync(_client, account.Email.Value);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokens.AccessToken);

        var property = Property.Create(
            account.Id,
            $"Casa {Guid.NewGuid():N}",
            $"Calle {Guid.NewGuid():N}",
            "Centro",
            PropertyType.Casa,
            Guid.NewGuid(),
            19.4326,
            -99.1332,
            null,
            null,
            DateTimeOffset.UtcNow);
        await _factory.Properties.AddAsync(property);
        var unit = Unit.Restore(
            Guid.NewGuid(),
            property.Id,
            account.Id,
            property.UniversityId,
            UnitType.Habitacion,
            Occupancy.Individual,
            1,
            3000,
            12,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.Amueblado,
            [],
            null,
            null,
            null,
            [],
            [],
            PublicationState.Borrador,
            DateTimeOffset.UtcNow);
        await _factory.Units.AddAsync(unit);

        using var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(file, "Fotos", "foto.png");
        using var upload = await _client.PostAsync(
            $"/api/v1/unidades/{unit.Id}/fotos",
            multipart);

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.Equal(1, _factory.CloudinaryClient.UploadCount);
        Assert.Equal(0, _factory.PhotoStorage.Count);
        using var uploadJson = System.Text.Json.JsonDocument.Parse(
            await upload.Content.ReadAsStringAsync());
        Assert.Equal(1, uploadJson.RootElement.GetProperty("totalFotos").GetInt32());

        var storedUnit = _factory.Units.All.Single(item => item.Id == unit.Id);
        var photoRef = PhotoReference.Encode(storedUnit.Photos.Single().StorageKey);
        using var read = await _client.GetAsync($"/api/v1/fotos/{photoRef}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(Png, await read.Content.ReadAsByteArrayAsync());
    }
}
