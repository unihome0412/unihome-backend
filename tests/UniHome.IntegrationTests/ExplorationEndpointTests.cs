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
        Assert.True(detail.Headers.CacheControl?.NoStore);
        var detailBody = await detail.Content.ReadAsStringAsync();
        using var detailJson = JsonDocument.Parse(detailBody);
        Assert.Equal(2, detailJson.RootElement.GetProperty("galeriaFotos").GetArrayLength());
        Assert.Single(detailJson.RootElement.GetProperty("serviciosIncluidos").EnumerateArray());
        Assert.Single(detailJson.RootElement.GetProperty("serviciosAparte").EnumerateArray());
        Assert.False(detailJson.RootElement.GetProperty("arrendadorVerificado").GetBoolean());
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
        Assert.True(list.Headers.CacheControl?.NoStore);
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
        Assert.False(
            listJson.RootElement[0].GetProperty("tarjeta")
                .GetProperty("arrendadorVerificado").GetBoolean());

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

    [Fact]
    public async Task Tarjeta_SinSesion_EsFavoritoNullYConSesionEsBooleano()
    {
        var account = await SeedAdultAsync();
        var universityId = await SeedUniversityAsync();
        var unit = await SeedUnitAsync(
            account.Id,
            universityId,
            "Casa Favorito Opcional",
            price: 3000,
            occupancy: Occupancy.Individual,
            forWhom: ForWhom.Mixto,
            state: PublicationState.Aprobada,
            availability: UnitAvailability.Disponible,
            latitude: 19.0,
            longitude: -99.0,
            hasPhoto: false);

        using var anonymous = await _client.GetAsync($"/api/v1/unidades/{unit.Id}/tarjeta");
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        using var anonymousJson = JsonDocument.Parse(
            await anonymous.Content.ReadAsStringAsync());
        Assert.Equal(
            JsonValueKind.Null,
            anonymousJson.RootElement.GetProperty("esFavorito").ValueKind);
        Assert.False(
            anonymousJson.RootElement.GetProperty("arrendadorVerificado").GetBoolean());

        using var anonymousList = await _client.GetAsync(
            $"/api/v1/exploracion/lista?universidadId={universityId}");
        using var anonymousListJson = JsonDocument.Parse(
            await anonymousList.Content.ReadAsStringAsync());
        Assert.Equal(
            JsonValueKind.Null,
            Assert.Single(anonymousListJson.RootElement.EnumerateArray())
                .GetProperty("esFavorito").ValueKind);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(account));

        using var withSession = await _client.GetAsync($"/api/v1/unidades/{unit.Id}/tarjeta");
        Assert.True(withSession.Headers.CacheControl?.NoStore);
        using var withSessionJson = JsonDocument.Parse(
            await withSession.Content.ReadAsStringAsync());
        Assert.False(withSessionJson.RootElement.GetProperty("esFavorito").GetBoolean());

        using var add = await _client.PutAsync($"/api/v1/favoritos/{unit.Id}", null);
        Assert.Equal(HttpStatusCode.NoContent, add.StatusCode);

        using var favorite = await _client.GetAsync($"/api/v1/unidades/{unit.Id}/tarjeta");
        using var favoriteJson = JsonDocument.Parse(
            await favorite.Content.ReadAsStringAsync());
        Assert.True(favoriteJson.RootElement.GetProperty("esFavorito").GetBoolean());
    }

    [Fact]
    public async Task Favoritos_EstanAisladosPorCuenta()
    {
        var owner = await SeedAdultAsync();
        var other = await SeedAdultAsync();
        var universityId = await SeedUniversityAsync();
        var unit = await SeedUnitAsync(
            owner.Id,
            universityId,
            "Casa Aislada",
            price: 3000,
            occupancy: Occupancy.Individual,
            forWhom: ForWhom.Mixto,
            state: PublicationState.Aprobada,
            availability: UnitAvailability.Disponible,
            latitude: 19.0,
            longitude: -99.0,
            hasPhoto: false);

        using var ownerClient = _factory.CreateClient();
        using var otherClient = _factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(ownerClient, owner));
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(otherClient, other));

        using var ownerAdd = await ownerClient.PutAsync(
            $"/api/v1/favoritos/{unit.Id}",
            null);
        Assert.Equal(HttpStatusCode.NoContent, ownerAdd.StatusCode);

        using var otherList = await otherClient.GetAsync("/api/v1/favoritos");
        Assert.Equal(HttpStatusCode.OK, otherList.StatusCode);
        Assert.Equal("0", otherList.Headers.GetValues("X-Total-Count").Single());

        using var otherCard = await otherClient.GetAsync(
            $"/api/v1/unidades/{unit.Id}/tarjeta");
        using var otherCardJson = JsonDocument.Parse(
            await otherCard.Content.ReadAsStringAsync());
        Assert.False(otherCardJson.RootElement.GetProperty("esFavorito").GetBoolean());

        using var otherDelete = await otherClient.DeleteAsync(
            $"/api/v1/favoritos/{unit.Id}");
        Assert.Equal(HttpStatusCode.NoContent, otherDelete.StatusCode);

        using var ownerList = await ownerClient.GetAsync("/api/v1/favoritos");
        Assert.Equal(HttpStatusCode.OK, ownerList.StatusCode);
        Assert.True(ownerList.Headers.CacheControl?.NoStore);
        Assert.Equal("1", ownerList.Headers.GetValues("X-Total-Count").Single());

        using var ownerCard = await ownerClient.GetAsync(
            $"/api/v1/unidades/{unit.Id}/tarjeta");
        using var ownerCardJson = JsonDocument.Parse(
            await ownerCard.Content.ReadAsStringAsync());
        Assert.True(ownerCardJson.RootElement.GetProperty("esFavorito").GetBoolean());
    }

    [Fact]
    public async Task Exploracion_Validaciones_DebenResponder400()
    {
        var universityId = await SeedUniversityAsync();
        var invalidRequests = new[]
        {
            "/api/v1/exploracion/mapa",
            $"/api/v1/exploracion/lista?universidadId={universityId}&page=0",
            $"/api/v1/exploracion/lista?universidadId={universityId}&pageSize=101",
            $"/api/v1/exploracion/lista?universidadId={universityId}&distanciaMaxKm=0",
            $"/api/v1/exploracion/lista?universidadId={universityId}&precioMaxMxn=0",
            $"/api/v1/exploracion/lista?universidadId={universityId}&paraQuien=Invalido",
            $"/api/v1/exploracion/lista?universidadId={universityId}&paraQuien=1",
            $"/api/v1/exploracion/mapa?universidadId={Guid.Empty}"
        };

        foreach (var request in invalidRequests)
        {
            using var response = await _client.GetAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(
                response.StatusCode == HttpStatusCode.BadRequest,
                $"{request} -> {(int)response.StatusCode} {body}");
        }

        var account = await SeedAdultAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(account));

        using var invalidFavoritePage = await _client.GetAsync("/api/v1/favoritos?page=0");
        using var invalidFavoriteSize = await _client.GetAsync(
            "/api/v1/favoritos?pageSize=101");

        Assert.Equal(HttpStatusCode.BadRequest, invalidFavoritePage.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidFavoriteSize.StatusCode);
    }

    [Fact]
    public async Task Exploracion_SinUbicacionUniversidad_DistanciaNulaYFiltro400()
    {
        var account = await SeedAdultAsync();
        var universityId = await SeedUniversityWithoutLocationAsync();
        var unit = await SeedUnitAsync(
            account.Id,
            universityId,
            "Casa Sin Ubicacion",
            price: 3000,
            occupancy: Occupancy.Individual,
            forWhom: ForWhom.Mixto,
            state: PublicationState.Aprobada,
            availability: UnitAvailability.Disponible,
            latitude: 19.0,
            longitude: -99.0,
            hasPhoto: false);

        using var list = await _client.GetAsync(
            $"/api/v1/exploracion/lista?universidadId={universityId}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var item = Assert.Single(listJson.RootElement.EnumerateArray());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("distanciaKm").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("tiempoTraslado").ValueKind);

        using var withDistanceFilter = await _client.GetAsync(
            $"/api/v1/exploracion/lista?universidadId={universityId}&distanciaMaxKm=2");
        Assert.Equal(HttpStatusCode.BadRequest, withDistanceFilter.StatusCode);

        using var card = await _client.GetAsync($"/api/v1/unidades/{unit.Id}/tarjeta");
        Assert.Equal(HttpStatusCode.OK, card.StatusCode);
        using var cardJson = JsonDocument.Parse(await card.Content.ReadAsStringAsync());
        Assert.Equal(
            JsonValueKind.Null,
            cardJson.RootElement.GetProperty("distanciaKm").ValueKind);
        Assert.Equal(
            JsonValueKind.Null,
            cardJson.RootElement.GetProperty("tiempoTraslado").ValueKind);
    }

    [Fact]
    public async Task EstadosNoPublicos_NoAparecenEnM04YFichaDevuelve404()
    {
        var account = await SeedAdultAsync();
        var universityId = await SeedUniversityAsync();
        var available = await SeedUnitAsync(
            account.Id,
            universityId,
            "Disponible",
            price: 3000,
            occupancy: Occupancy.Individual,
            forWhom: ForWhom.Mixto,
            state: PublicationState.Aprobada,
            availability: UnitAvailability.Disponible,
            latitude: 19.0,
            longitude: -99.0,
            hasPhoto: false);
        var nonPublic = new[]
        {
            await SeedUnitAsync(
                account.Id, universityId, "Ocupada", 3100, Occupancy.Individual,
                ForWhom.Mixto, PublicationState.Aprobada, UnitAvailability.Ocupado,
                19.001, -99.0, hasPhoto: false),
            await SeedUnitAsync(
                account.Id, universityId, "Mantenimiento", 3200, Occupancy.Individual,
                ForWhom.Mixto, PublicationState.Aprobada, UnitAvailability.Mantenimiento,
                19.002, -99.0, hasPhoto: false),
            await SeedUnitAsync(
                account.Id, universityId, "EnRevision", 3300, Occupancy.Individual,
                ForWhom.Mixto, PublicationState.EnRevision, UnitAvailability.Disponible,
                19.003, -99.0, hasPhoto: false),
            await SeedUnitAsync(
                account.Id, universityId, "Rechazada", 3400, Occupancy.Individual,
                ForWhom.Mixto, PublicationState.Rechazada, UnitAvailability.Disponible,
                19.004, -99.0, hasPhoto: false),
            await SeedUnitAsync(
                account.Id, universityId, "Borrador", 3500, Occupancy.Individual,
                ForWhom.Mixto, PublicationState.Borrador, UnitAvailability.Disponible,
                19.005, -99.0, hasPhoto: false)
        };

        using var list = await _client.GetAsync(
            $"/api/v1/exploracion/lista?universidadId={universityId}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal("1", list.Headers.GetValues("X-Total-Count").Single());
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(
            available.Id,
            Assert.Single(listJson.RootElement.EnumerateArray())
                .GetProperty("id").GetGuid());

        using var map = await _client.GetAsync(
            $"/api/v1/exploracion/mapa?universidadId={universityId}");
        using var mapJson = JsonDocument.Parse(await map.Content.ReadAsStringAsync());
        Assert.Equal(
            available.Id,
            Assert.Single(mapJson.RootElement.EnumerateArray())
                .GetProperty("id").GetGuid());

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(account));

        foreach (var unit in nonPublic)
        {
            using var card = await _client.GetAsync($"/api/v1/unidades/{unit.Id}/tarjeta");
            using var detail = await _client.GetAsync($"/api/v1/unidades/{unit.Id}/ficha");
            Assert.Equal(HttpStatusCode.NotFound, card.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        }
    }

    [Fact]
    public async Task UniversidadInexistenteOInactiva_DebeResponder404()
    {
        var inactive = University.Restore(
            Guid.NewGuid(),
            $"Universidad inactiva {Guid.NewGuid():N}",
            isActive: false,
            DateTimeOffset.UtcNow);
        await _factory.Universities.AddAsync(inactive);
        var missing = Guid.NewGuid();

        using var inactiveMap = await _client.GetAsync(
            $"/api/v1/exploracion/mapa?universidadId={inactive.Id}");
        using var inactiveList = await _client.GetAsync(
            $"/api/v1/exploracion/lista?universidadId={inactive.Id}");
        using var missingMap = await _client.GetAsync(
            $"/api/v1/exploracion/mapa?universidadId={missing}");

        Assert.Equal(HttpStatusCode.NotFound, inactiveMap.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inactiveList.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingMap.StatusCode);
    }

    [Fact]
    public async Task Favoritos_Paginacion_DebeRespetarPaginaYTotal()
    {
        var account = await SeedAdultAsync();
        var universityId = await SeedUniversityAsync();
        var units = new[]
        {
            await SeedUnitAsync(
                account.Id, universityId, "Favorita 1", 3000, Occupancy.Individual,
                ForWhom.Mixto, PublicationState.Aprobada, UnitAvailability.Disponible,
                19.0, -99.0, hasPhoto: false),
            await SeedUnitAsync(
                account.Id, universityId, "Favorita 2", 3100, Occupancy.Individual,
                ForWhom.Mixto, PublicationState.Aprobada, UnitAvailability.Disponible,
                19.0, -99.0, hasPhoto: false),
            await SeedUnitAsync(
                account.Id, universityId, "Favorita 3", 3200, Occupancy.Individual,
                ForWhom.Mixto, PublicationState.Aprobada, UnitAvailability.Disponible,
                19.0, -99.0, hasPhoto: false)
        };

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(account));

        foreach (var unit in units)
        {
            using var add = await _client.PutAsync($"/api/v1/favoritos/{unit.Id}", null);
            Assert.Equal(HttpStatusCode.NoContent, add.StatusCode);
        }

        using var firstPage = await _client.GetAsync("/api/v1/favoritos?page=1&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);
        Assert.Equal("3", firstPage.Headers.GetValues("X-Total-Count").Single());
        Assert.Equal("1", firstPage.Headers.GetValues("X-Page").Single());
        Assert.Equal("2", firstPage.Headers.GetValues("X-Page-Size").Single());
        using var firstJson = JsonDocument.Parse(
            await firstPage.Content.ReadAsStringAsync());
        Assert.Equal(2, firstJson.RootElement.GetArrayLength());

        using var secondPage = await _client.GetAsync("/api/v1/favoritos?page=2&pageSize=2");
        using var secondJson = JsonDocument.Parse(
            await secondPage.Content.ReadAsStringAsync());
        Assert.Single(secondJson.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Favorito_UnidadNoPublicaAjena_DebeResponder404()
    {
        var owner = await SeedAdultAsync();
        var other = await SeedAdultAsync();
        var universityId = await SeedUniversityAsync();
        var draft = await SeedUnitAsync(
            owner.Id,
            universityId,
            "Borrador privado",
            price: 3000,
            occupancy: Occupancy.Individual,
            forWhom: ForWhom.Mixto,
            state: PublicationState.Borrador,
            availability: UnitAvailability.Disponible,
            latitude: 19.0,
            longitude: -99.0,
            hasPhoto: false);

        using var otherClient = _factory.CreateClient();
        using var ownerClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(otherClient, other));
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(ownerClient, owner));

        using var forbidden = await otherClient.PutAsync(
            $"/api/v1/favoritos/{draft.Id}",
            null);
        using var otherList = await otherClient.GetAsync("/api/v1/favoritos");
        using var ownerAdd = await ownerClient.PutAsync(
            $"/api/v1/favoritos/{draft.Id}",
            null);

        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        Assert.Equal("0", otherList.Headers.GetValues("X-Total-Count").Single());
        Assert.Equal(HttpStatusCode.NoContent, ownerAdd.StatusCode);
    }

    [Fact]
    public async Task RecursosInexistentes_DebenResponder404YDeleteIdempotente204()
    {
        var account = await SeedAdultAsync();
        var missing = Guid.NewGuid();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(account));

        using var add = await _client.PutAsync($"/api/v1/favoritos/{missing}", null);
        Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);

        using var remove = await _client.DeleteAsync($"/api/v1/favoritos/{missing}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        using var card = await _client.GetAsync($"/api/v1/unidades/{missing}/tarjeta");
        using var detail = await _client.GetAsync($"/api/v1/unidades/{missing}/ficha");
        Assert.Equal(HttpStatusCode.NotFound, card.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    private async Task<string> LoginAsync(
        HttpClient client,
        Domain.Accounts.Account account)
    {
        using var login = await AuthTestHelpers.LoginAsync(client, account.Email.Value);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        return tokens.AccessToken;
    }

    private async Task<Guid> SeedUniversityWithoutLocationAsync()
    {
        var university = University.Restore(
            Guid.NewGuid(),
            $"Universidad sin ubicacion {Guid.NewGuid():N}",
            true,
            DateTimeOffset.UtcNow);
        await _factory.Universities.AddAsync(university);
        return university.Id;
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
