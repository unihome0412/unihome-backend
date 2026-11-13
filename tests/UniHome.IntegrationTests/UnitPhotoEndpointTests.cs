using System.Net;
using System.Net.Http.Headers;
using UniHome.Application.Units.Photos;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

public sealed class UnitPhotoEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFElEQVR42mP4z8DAAMb//0MZUA4Aep8J9x7TI9wAAAAASUVORK5CYII=");

    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public UnitPhotoEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FotoPublica_DebeEntregarseSinSesionSinExponerCloudinary()
    {
        var owner = await SeedAccountAsync();
        var (unit, key) = await SeedUnitWithPhotoAsync(owner.Id, PublicationState.Aprobada);
        var reference = PhotoReference.Encode(key);

        using var response = await _client.GetAsync($"/api/v1/fotos/{reference}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Png, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("public, max-age=300", response.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.DoesNotContain("cloudinary", response.Headers.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(Guid.Empty, unit.Id);
    }

    [Fact]
    public async Task FotoBorrador_SoloDebeEntregarseAlPropietario()
    {
        var owner = await SeedAccountAsync();
        var other = await SeedAccountAsync();
        var (_, key) = await SeedUnitWithPhotoAsync(owner.Id, PublicationState.Borrador);
        var reference = PhotoReference.Encode(key);

        using var anonymous = await _client.GetAsync($"/api/v1/fotos/{reference}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(other));
        using var foreign = await _client.GetAsync($"/api/v1/fotos/{reference}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(owner));
        using var own = await _client.GetAsync($"/api/v1/fotos/{reference}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Contains("no-store", own.Headers.CacheControl?.ToString());
        Assert.Contains("private", own.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task FotoInexistente_DebeResponder404()
    {
        using var unknownReference = await _client.GetAsync(
            $"/api/v1/fotos/{PhotoReference.Encode("no/existe")}");
        using var malformed = await _client.GetAsync("/api/v1/fotos/%25%25%25");

        Assert.Equal(HttpStatusCode.NotFound, unknownReference.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
    }

    private async Task<Domain.Accounts.Account> SeedAccountAsync() =>
        await AuthTestHelpers.SeedAccountAsync(_factory, AuthTestHelpers.UniqueEmail());

    private async Task<string> LoginAsync(Domain.Accounts.Account account)
    {
        using var login = await AuthTestHelpers.LoginAsync(_client, account.Email.Value);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        return tokens.AccessToken;
    }

    private async Task<(Unit Unit, string StorageKey)> SeedUnitWithPhotoAsync(
        Guid ownerId,
        PublicationState state)
    {
        var property = Property.Create(
            ownerId,
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

        await using var photoStream = new MemoryStream(Png);
        var stored = await _factory.PhotoStorage.SaveAsync(photoStream, "image/png");
        var unit = Unit.Restore(
            Guid.NewGuid(),
            property.Id,
            ownerId,
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
            [
                new UnitPhoto(
                    stored.StorageKey,
                    "image/png",
                    Png.LongLength,
                    stored.Sha256,
                    0,
                    true,
                    DateTimeOffset.UtcNow)
            ],
            state,
            DateTimeOffset.UtcNow);
        await _factory.Units.AddAsync(unit);
        return (unit, stored.StorageKey);
    }
}
