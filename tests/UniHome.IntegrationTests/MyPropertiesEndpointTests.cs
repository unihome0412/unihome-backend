using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Accounts;
using UniHome.Domain.Universities;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// EP-M05-11 (HU-033): "Mis propiedades" agrupa habitaciones por propiedad,
/// cuenta disponibilidad y filtra por tipo de propiedad y por estado de
/// habitación. Cubre también HU-036 (alta reutilizando el endpoint existente)
/// y HU-092 (agregar otra habitación sin volver a capturar la dirección).
/// </summary>
public sealed class MyPropertiesEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private const string OwnerEmail = "mis-propiedades.owner@uni.edu";
    private const string OtherOwnerEmail = "mis-propiedades.other@uni.edu";
    private static readonly string[] SingleFurniture = ["Cama"];

    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public MyPropertiesEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MisPropiedades_AgrupaHabitacionesCuentaDisponiblesYFiltra()
    {
        var token = await AuthenticateAsync(OwnerEmail);
        var universityId = await SeedUniversityAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var houseId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "Tipo", "Casa");
        // HU-092: una segunda habitación sobre la misma propiedad, sin reenviar dirección.
        await CreateUnitAsync(houseId, "Habitacion");
        await CreateUnitAsync(houseId, "Departamento");

        var buildingId = await CreatePropertyAsync(
            universityId, $"Depa {Guid.NewGuid():N}", "Depa", "Departamento");
        await CreateUnitAsync(buildingId, "Estudio");

        var vacantId = await CreatePropertyAsync(
            universityId, $"Vacia {Guid.NewGuid():N}", "Vacia", "Casa");

        using var response = await _client.GetAsync("/api/v1/mis-propiedades");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var properties = json.RootElement.EnumerateArray().ToArray();
        Assert.Equal(3, properties.Length);

        var house = properties.Single(item => item.GetProperty("id").GetGuid() == houseId);
        Assert.Equal("Casa", house.GetProperty("tipoPropiedad").GetString());
        Assert.Equal(2, house.GetProperty("totalHabitaciones").GetInt32());
        Assert.Equal(2, house.GetProperty("habitacionesDisponibles").GetInt32());
        var houseRooms = house.GetProperty("habitaciones").EnumerateArray().ToArray();
        Assert.Equal(2, houseRooms.Length);
        Assert.All(houseRooms, room => Assert.Equal(
            "Disponible", room.GetProperty("estado").GetString()));

        var vacant = properties.Single(item => item.GetProperty("id").GetGuid() == vacantId);
        Assert.Equal(0, vacant.GetProperty("totalHabitaciones").GetInt32());
        Assert.Equal(0, vacant.GetProperty("habitacionesDisponibles").GetInt32());
        Assert.Empty(vacant.GetProperty("habitaciones").EnumerateArray());

        using var byType = await _client.GetAsync("/api/v1/mis-propiedades?tipoPropiedad=Departamento");
        Assert.Equal(HttpStatusCode.OK, byType.StatusCode);
        using var byTypeJson = JsonDocument.Parse(await byType.Content.ReadAsStringAsync());
        var byTypeIds = byTypeJson.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();
        Assert.Equal([buildingId], byTypeIds);

        using var byAvailable = await _client.GetAsync(
            "/api/v1/mis-propiedades?estadoHabitacion=Disponible");
        using var byAvailableJson = JsonDocument.Parse(await byAvailable.Content.ReadAsStringAsync());
        var byAvailableIds = byAvailableJson.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();
        Assert.Contains(houseId, byAvailableIds);
        Assert.Contains(buildingId, byAvailableIds);
        Assert.DoesNotContain(vacantId, byAvailableIds);

        using var byMaintenance = await _client.GetAsync(
            "/api/v1/mis-propiedades?estadoHabitacion=Mantenimiento");
        using var byMaintenanceJson = JsonDocument.Parse(
            await byMaintenance.Content.ReadAsStringAsync());
        Assert.Empty(byMaintenanceJson.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task MisPropiedades_SinSesion_DebeResponder401()
    {
        using var response = await _client.GetAsync("/api/v1/mis-propiedades");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MisPropiedades_EstaAisladoPorCuenta()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var otherToken = await AuthenticateAsync(OtherOwnerEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        await CreatePropertyAsync(universityId, $"Casa Aislada {Guid.NewGuid():N}", "Aislada", "Casa");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", otherToken);
        using var response = await _client.GetAsync("/api/v1/mis-propiedades");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(json.RootElement.EnumerateArray());
    }

    [Theory]
    [InlineData("tipoPropiedad=1")]
    [InlineData("tipoPropiedad=Invalido")]
    [InlineData("estadoHabitacion=1")]
    [InlineData("estadoHabitacion=Invalido")]
    public async Task MisPropiedades_FiltroInvalido_DebeResponder400(string query)
    {
        var token = await AuthenticateAsync(OwnerEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.GetAsync($"/api/v1/mis-propiedades?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> AuthenticateAsync(string email)
    {
        var existing = await _factory.Accounts.FindByEmailAsync(EmailAddress.Create(email));

        if (existing is null)
        {
            await AuthTestHelpers.SeedAccountAsync(_factory, email);
        }

        using var login = await AuthTestHelpers.LoginAsync(_client, email);
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
            new GeoCoordinate(19.4326, -99.1332));
        return university.Id;
    }

    private async Task<Guid> CreatePropertyAsync(
        Guid universityId,
        string name,
        string address,
        string propertyType)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/propiedades",
            new
            {
                nombre = name,
                direccion = address,
                colonia = "Centro",
                tipoPropiedad = propertyType,
                universidadId = universityId,
                latitud = 19.4326,
                longitud = -99.1332
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateUnitAsync(Guid propertyId, string unitType)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/api/v1/propiedades/{propertyId}/unidades",
            new
            {
                tipoUnidad = unitType,
                ocupacion = "Individual",
                lugares = 1,
                precioMensualPorLugarMxn = 3500,
                tamanoM2 = 12,
                paraQuien = "Mixto",
                bano = "Privado",
                mobiliario = "Amueblado",
                muebles = SingleFurniture
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }
}
