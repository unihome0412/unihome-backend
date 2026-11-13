using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.Domain.Universities;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

public sealed class ExplorationEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public ExplorationEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Exploracion_Publica_FiltraAprobadasDisponiblesYNoExponePii()
    {
        var account = await SeedAdultAsync();
        var universityId = await SeedUniversityAsync();
        var available = await SeedUnitAsync(
            account.Id,
            universityId,
            "Casa Disponible",
            price: 3000,
            occupancy: Occupancy.Individual,
            forWhom: ForWhom.Mixto,
            state: PublicationState.Aprobada,
            availability: UnitAvailability.Disponible,
            latitude: 19.0045,
            longitude: -99.0,
            hasPhoto: true);
        var occupied = await SeedUnitAsync(
            account.Id,
            universityId,
            "Casa Ocupada",
            price: 3200,
            occupancy: Occupancy.Individual,
            forWhom: ForWhom.Mixto,
            state: PublicationState.Aprobada,
            availability: UnitAvailability.Ocupado,
            latitude: 19.001,
            longitude: -99.0,
            hasPhoto: false);
        var inReview = await SeedUnitAsync(
            account.Id,
            universityId,
            "Casa En Revision",
            price: 3300,
            occupancy: Occupancy.Individual,
            forWhom: ForWhom.Mixto,
            state: PublicationState.EnRevision,
            availability: UnitAvailability.Disponible,
            latitude: 19.002,
            longitude: -99.0,
            hasPhoto: false);
        var farShared = await SeedUnitAsync(
            account.Id,
            universityId,
            "Casa Compartida",
            price: 5000,
            occupancy: Occupancy.Compartida,
            forWhom: ForWhom.SoloMujeres,
            state: PublicationState.Aprobada,
            availability: UnitAvailability.Disponible,
            latitude: 19.027,
            longitude: -99.0,
            hasPhoto: false);

        using var map = await _client.GetAsync(
            $"/api/v1/exploracion/mapa?universidadId={universityId}");
        Assert.Equal(HttpStatusCode.OK, map.StatusCode);
        using var mapJson = JsonDocument.Parse(await map.Content.ReadAsStringAsync());
        Assert.Equal(2, mapJson.RootElement.GetArrayLength());
        Assert.DoesNotContain(
            occupied.Id.ToString(),
            mapJson.RootElement.ToString());
        Assert.DoesNotContain(
            inReview.Id.ToString(),
            mapJson.RootElement.ToString());

        using var list = await _client.GetAsync(
            $"/api/v1/exploracion/lista?universidadId={universityId}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal("2", list.Headers.GetValues("X-Total-Count").Single());
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(2, listJson.RootElement.GetArrayLength());

        using var filtered = await _client.GetAsync(
            $"/api/v1/exploracion/lista?universidadId={universityId}" +
            "&distanciaMaxKm=2&precioMaxMxn=4500&soloIndividual=true");
        using var filteredJson = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync());
        Assert.Single(filteredJson.RootElement.EnumerateArray());
        Assert.Equal(
            available.Id,
            filteredJson.RootElement[0].GetProperty("id").GetGuid());

        using var women = await _client.GetAsync(
            $"/api/v1/exploracion/lista?universidadId={universityId}&paraQuien=SoloMujeres");
        using var womenJson = JsonDocument.Parse(await women.Content.ReadAsStringAsync());
        Assert.Single(womenJson.RootElement.EnumerateArray());
        Assert.Equal(
            farShared.Id,
            womenJson.RootElement[0].GetProperty("id").GetGuid());

        using var card = await _client.GetAsync($"/api/v1/unidades/{available.Id}/tarjeta");
        Assert.Equal(HttpStatusCode.OK, card.StatusCode);
        var cardBody = await card.Content.ReadAsStringAsync();
        Assert.DoesNotContain("correo", cardBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("telefono", cardBody, StringComparison.OrdinalIgnoreCase);

        using var occupiedCard = await _client.GetAsync(
            $"/api/v1/unidades/{occupied.Id}/tarjeta");
        Assert.Equal(HttpStatusCode.NotFound, occupiedCard.StatusCode);

        using var detailWithoutSession = await _client.GetAsync(
            $"/api/v1/unidades/{available.Id}/ficha");
        Assert.Equal(HttpStatusCode.Unauthorized, detailWithoutSession.StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(account));
        using var detail = await _client.GetAsync($"/api/v1/unidades/{available.Id}/ficha");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var detailBody = await detail.Content.ReadAsStringAsync();
        using var detailJson = JsonDocument.Parse(detailBody);
        Assert.Equal(2, detailJson.RootElement.GetProperty("galeriaFotos").GetArrayLength());
        Assert.Single(detailJson.RootElement.GetProperty("serviciosIncluidos").EnumerateArray());
        Assert.Single(detailJson.RootElement.GetProperty("serviciosAparte").EnumerateArray());
        Assert.Equal("a pie", detailJson.RootElement.GetProperty("tiempoTraslado").GetString());
        Assert.DoesNotContain("correo", detailBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("telefono", detailBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Favoritos_SonIdempotentesYConservanEstadoNoPublico()
    {
        var account = await SeedAdultAsync();
        var universityId = await SeedUniversityAsync();
        var unit = await SeedUnitAsync(
            account.Id,
            universityId,
            "Casa Favorita",
            price: 3000,
            occupancy: Occupancy.Individual,
            forWhom: ForWhom.Mixto,
            state: PublicationState.Aprobada,
            availability: UnitAvailability.Disponible,
            latitude: 19.0,
            longitude: -99.0,
            hasPhoto: false);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(account));

        using var first = await _client.PutAsync($"/api/v1/favoritos/{unit.Id}", null);
        using var second = await _client.PutAsync($"/api/v1/favoritos/{unit.Id}", null);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        using var cardWithSession = await _client.GetAsync(
            $"/api/v1/unidades/{unit.Id}/tarjeta");
        using var cardWithSessionJson = JsonDocument.Parse(
            await cardWithSession.Content.ReadAsStringAsync());
        Assert.True(
            cardWithSessionJson.RootElement.GetProperty("esFavorito").GetBoolean());

        using var list = await _client.GetAsync("/api/v1/favoritos");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal("1", list.Headers.GetValues("X-Total-Count").Single());
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Single(listJson.RootElement.EnumerateArray());
        Assert.Equal(
            "Disponible",
            listJson.RootElement[0].GetProperty("tarjeta")
                .GetProperty("estadoActual").GetString());
        Assert.True(
            listJson.RootElement[0].GetProperty("tarjeta")
                .GetProperty("esFavorito").GetBoolean());

        _factory.Units.Replace(RestoreUnit(unit, PublicationState.Aprobada, UnitAvailability.Ocupado));

        using var afterState = await _client.GetAsync("/api/v1/favoritos");
        using var afterStateJson = JsonDocument.Parse(await afterState.Content.ReadAsStringAsync());
        Assert.Single(afterStateJson.RootElement.EnumerateArray());
        Assert.Equal(
            "Ocupado",
            afterStateJson.RootElement[0].GetProperty("tarjeta")
                .GetProperty("estadoActual").GetString());

        using var card = await _client.GetAsync($"/api/v1/unidades/{unit.Id}/tarjeta");
        Assert.Equal(HttpStatusCode.NotFound, card.StatusCode);

        using var removed = await _client.DeleteAsync($"/api/v1/favoritos/{unit.Id}");
        using var removedAgain = await _client.DeleteAsync($"/api/v1/favoritos/{unit.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removedAgain.StatusCode);

        using var empty = await _client.GetAsync("/api/v1/favoritos");
        Assert.Equal("0", empty.Headers.GetValues("X-Total-Count").Single());
    }

    [Fact]
    public async Task Favoritos_SinSesion_DebenResponder401()
    {
        using var list = await _client.GetAsync("/api/v1/favoritos");
        using var add = await _client.PutAsync($"/api/v1/favoritos/{Guid.NewGuid()}", null);

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, add.StatusCode);
    }

    private async Task<Domain.Accounts.Account> SeedAdultAsync()
    {
        var email = AuthTestHelpers.UniqueEmail();
        return await AuthTestHelpers.SeedAccountAsync(_factory, email);
    }

    private async Task<string> LoginAsync(Domain.Accounts.Account account)
    {
        using var login = await AuthTestHelpers.LoginAsync(_client, account.Email.Value);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        return tokens.AccessToken;
    }

    private async Task<Guid> SeedUniversityAsync()
    {
        var university = University.Restore(
            Guid.NewGuid(),
            $"Universidad {Guid.NewGuid():N}",
            true,
            DateTimeOffset.UtcNow);
        await _factory.Universities.AddAsync(university);
        _factory.Universities.SetLocation(
            university.Id,
            new GeoCoordinate(19.0, -99.0));
        return university.Id;
    }

    private async Task<Unit> SeedUnitAsync(
        Guid ownerId,
        Guid universityId,
        string name,
        decimal price,
        Occupancy occupancy,
        ForWhom forWhom,
        PublicationState state,
        UnitAvailability availability,
        double latitude,
        double longitude,
        bool hasPhoto)
    {
        var property = Property.Create(
            ownerId,
            name,
            $"Calle {Guid.NewGuid():N}",
            "Centro",
            PropertyType.Casa,
            universityId,
            latitude,
            longitude,
            null,
            null,
            DateTimeOffset.UtcNow);
        await _factory.Properties.AddAsync(property);

        var unit = Unit.Restore(
            Guid.NewGuid(),
            property.Id,
            ownerId,
            universityId,
            UnitType.Habitacion,
            occupancy,
            occupancy == Occupancy.Individual ? 1 : 3,
            price,
            12,
            forWhom,
            BathroomType.Privado,
            FurnitureType.Amueblado,
            ["Cama"],
            null,
            "Cerca del campus",
            "No fumar",
            [
                ServiceCharge.Create(ServiceName.Agua, ServiceMode.Incluido, null),
                ServiceCharge.Create(ServiceName.Internet, ServiceMode.Aparte, 300)
            ],
            hasPhoto
                ?
                [
                    new UnitPhoto("foto-1", "image/png", 10, "hash-1", 0, true, DateTimeOffset.UtcNow),
                    new UnitPhoto("foto-2", "image/png", 10, "hash-2", 1, false, DateTimeOffset.UtcNow)
                ]
                : [],
            state,
            DateTimeOffset.UtcNow,
            availability);
        await _factory.Units.AddAsync(unit);
        return unit;
    }

    private static Unit RestoreUnit(
        Unit source,
        PublicationState state,
        UnitAvailability availability) =>
        Unit.Restore(
            source.Id,
            source.PropertyId,
            source.OwnerAccountId,
            source.UniversityId,
            source.Type,
            source.Occupancy,
            source.Places,
            source.PricePerPlace,
            source.SizeM2,
            source.ForWhom,
            source.Bathroom,
            source.Furniture,
            source.FurnitureItems,
            source.Template,
            source.ZoneInfo,
            source.Rules,
            source.Services,
            source.Photos,
            state,
            source.CreatedAt,
            availability);
}
