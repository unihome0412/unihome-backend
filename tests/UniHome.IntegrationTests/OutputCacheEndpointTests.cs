using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;
using UniHome.Domain.Universities;

namespace UniHome.IntegrationTests;

/// <summary>
/// Bloque 2 (auditoría): caché de salida (output cache) solo para GET
/// públicos sin datos personales. Estas pruebas verifican tanto que el
/// caché funciona de verdad (sirve una respuesta obsoleta dentro del TTL y
/// fresca después) como la regla de seguridad explícita: nunca debe filtrar
/// datos personales de una cuenta a otra.
/// </summary>
public sealed class OutputCacheEndpointTests : IClassFixture<ShortOutputCacheApiFactory>
{
    private readonly ShortOutputCacheApiFactory _factory;
    private readonly HttpClient _client;

    public OutputCacheEndpointTests(ShortOutputCacheApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Mapa_DentroDelTtl_SirveLaRespuestaObsoleta_YFrescaDespues()
    {
        var configuredTtl = _factory.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<UniHome.Api.Caching.OutputCacheOptions>>()
            .Value.PublicTtlSeconds;
        Assert.Equal(ShortOutputCacheApiFactory.PublicTtlSeconds, configuredTtl);

        var universityId = await SeedUniversityAsync();
        var unit = await SeedUnitAsync(
            universityId,
            PublicationState.Aprobada,
            UnitAvailability.Disponible);

        using var first = await _client.GetAsync($"/api/v1/exploracion/mapa?universidadId={universityId}");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains(unit.Id.ToString(), await first.Content.ReadAsStringAsync());

        // Se cambia el dato subyacente directamente en el repositorio (sin
        // pasar por la API) para poder distinguir "sigue cacheado" de
        // "coincidencia porque nada cambió".
        await _factory.Units.AddAsync(RestoreWithAvailability(unit, UnitAvailability.Ocupado));
        var mutated = await _factory.Units.FindByIdAsync(unit.Id);
        Assert.Equal(UnitAvailability.Ocupado, mutated!.Availability);

        using var stillCached = await _client.GetAsync($"/api/v1/exploracion/mapa?universidadId={universityId}");
        Assert.Equal(HttpStatusCode.OK, stillCached.StatusCode);
        Assert.Contains(
            unit.Id.ToString(),
            await stillCached.Content.ReadAsStringAsync());

        await Task.Delay(TimeSpan.FromSeconds(ShortOutputCacheApiFactory.PublicTtlSeconds + 1));

        using var fresh = await _client.GetAsync($"/api/v1/exploracion/mapa?universidadId={universityId}");
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        Assert.DoesNotContain(unit.Id.ToString(), await fresh.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Lista_RespuestaDeUnaCuentaAutenticada_NuncaSeSirveComoCacheAOtraPeticion()
    {
        var universityId = await SeedUniversityAsync();
        var unit = await SeedUnitAsync(
            universityId,
            PublicationState.Aprobada,
            UnitAvailability.Disponible);

        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);

        using var favoriteRequest = new HttpRequestMessage(
            HttpMethod.Put, $"/api/v1/favoritos/{unit.Id}");
        favoriteRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var favoriteResponse = await _client.SendAsync(favoriteRequest);
        Assert.Equal(HttpStatusCode.NoContent, favoriteResponse.StatusCode);

        // Petición autenticada: esFavorito=true. Por la política "With", esto
        // nunca debe escribirse en el caché compartido.
        using var authenticatedRequest = new HttpRequestMessage(
            HttpMethod.Get, "/api/v1/exploracion/lista?universidadId=" + universityId);
        authenticatedRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var authenticatedResponse = await _client.SendAsync(authenticatedRequest);
        var authenticatedBody = await authenticatedResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"esFavorito\":true", authenticatedBody);

        // Petición anónima inmediatamente después: si la respuesta anterior se
        // hubiera cacheado, esta vería esFavorito=true de una cuenta ajena.
        using var anonymousResponse = await _client.GetAsync(
            "/api/v1/exploracion/lista?universidadId=" + universityId);
        var anonymousBody = await anonymousResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"esFavorito\":true", anonymousBody);
    }

    [Fact]
    public async Task Universidades_AlCrearUna_InvalidaDeInmediatoLaBusquedaCacheada()
    {
        var uniqueToken = Guid.NewGuid().ToString("N")[..12];

        using var before = await _client.GetAsync($"/api/v1/universidades?buscar={uniqueToken}");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.DoesNotContain(uniqueToken, await before.Content.ReadAsStringAsync());

        var adminToken = await AuthenticateAsAdminAsync();
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/universidades")
        {
            Content = JsonContent.Create(new { nombreYCampus = $"Universidad {uniqueToken}" })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        using var create = await _client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        // Sin esta invalidación, la búsqueda recién creada seguiría sin
        // aparecer hasta que venza el TTL corto.
        using var after = await _client.GetAsync($"/api/v1/universidades?buscar={uniqueToken}");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Contains(uniqueToken, await after.Content.ReadAsStringAsync());
    }

    private async Task<string> AuthenticateAsAdminAsync()
    {
        const string adminEmail = "admin.outputcache@unihome.test";
        var existing = await _factory.Accounts.FindByIdAsync(UniHomeApiFactory.TestAdminAccountId);
        var admin = existing ?? await AuthTestHelpers.SeedAccountAsync(
            _factory,
            adminEmail,
            id: UniHomeApiFactory.TestAdminAccountId);

        using var login = await AuthTestHelpers.LoginAsync(_client, admin.Email.Value);
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
        _factory.Universities.SetLocation(university.Id, new GeoCoordinate(19.0, -99.0));
        return university.Id;
    }

    private async Task<Unit> SeedUnitAsync(
        Guid universityId,
        PublicationState state,
        UnitAvailability availability)
    {
        var ownerId = Guid.NewGuid();
        var property = Property.Create(
            ownerId,
            $"Propiedad {Guid.NewGuid():N}",
            $"Calle {Guid.NewGuid():N}",
            "Centro",
            PropertyType.Casa,
            universityId,
            19.0,
            -99.0,
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
            Occupancy.Individual,
            1,
            3000,
            12,
            ForWhom.Mixto,
            BathroomType.Privado,
            FurnitureType.Amueblado,
            ["Cama"],
            null,
            "Cerca del campus",
            "No fumar",
            [ServiceCharge.Create(ServiceName.Agua, ServiceMode.Incluido, null)],
            [new UnitPhoto("foto-1", "image/png", 10, "hash-1", 0, true, DateTimeOffset.UtcNow)],
            state,
            DateTimeOffset.UtcNow,
            availability);
        await _factory.Units.AddAsync(unit);
        return unit;
    }

    private static Unit RestoreWithAvailability(Unit unit, UnitAvailability availability) =>
        Unit.Restore(
            unit.Id,
            unit.PropertyId,
            unit.OwnerAccountId,
            unit.UniversityId,
            unit.Type,
            unit.Occupancy,
            unit.Places,
            unit.PricePerPlace,
            unit.SizeM2,
            unit.ForWhom,
            unit.Bathroom,
            unit.Furniture,
            unit.FurnitureItems,
            unit.Template,
            unit.ZoneInfo,
            unit.Rules,
            unit.Services,
            unit.Photos,
            unit.PublicationState,
            unit.CreatedAt,
            availability,
            unit.OccupiedPlaces);
}
