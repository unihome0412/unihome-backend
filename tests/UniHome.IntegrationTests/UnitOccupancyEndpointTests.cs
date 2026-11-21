using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Universities;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// EP-M09 (HU-048/049/093/094/095): invitación, vínculo con código,
/// mantenimiento y baja/abandono de ocupación.
/// </summary>
public sealed class UnitOccupancyEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private const string OwnerEmail = "ocupacion.owner@uni.edu";
    private const string TenantEmail = "ocupacion.tenant@uni.edu";
    private const string OtherTenantEmail = "ocupacion.tenant2@uni.edu";
    private static readonly string[] SingleFurniture = ["Cama"];

    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public UnitOccupancyEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FlujoCompleto_InvitacionUnirseMantenimientoBajaYAbandono()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var tenantToken = await AuthenticateAsync(TenantEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(
            universityId, $"Casa {Guid.NewGuid():N}", "Flujo");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        using var invitation = await _client.GetAsync($"/api/v1/unidades/{unitId}/invitacion");
        Assert.Equal(HttpStatusCode.OK, invitation.StatusCode);
        Assert.Contains(
            "no-store",
            invitation.Headers.CacheControl?.ToString() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        using var invitationJson = JsonDocument.Parse(await invitation.Content.ReadAsStringAsync());
        var code = invitationJson.RootElement.GetProperty("codigo").GetString();
        Assert.False(string.IsNullOrWhiteSpace(code));

        using var invitationAgain = await _client.GetAsync($"/api/v1/unidades/{unitId}/invitacion");
        using var invitationAgainJson = JsonDocument.Parse(
            await invitationAgain.Content.ReadAsStringAsync());
        Assert.Equal(code, invitationAgainJson.RootElement.GetProperty("codigo").GetString());

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantToken);
        using var join = await _client.PostAsJsonAsync(
            "/api/v1/unidades/unirse",
            new { codigo = code });
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        using var joinJson = JsonDocument.Parse(await join.Content.ReadAsStringAsync());
        Assert.Equal(unitId, joinJson.RootElement.GetProperty("unidadId").GetGuid());
        Assert.Equal("Activo", joinJson.RootElement.GetProperty("estado").GetString());

        using var profileAfterJoin = await _client.GetAsync("/api/v1/perfil/mi-perfil");
        using var profileAfterJoinJson = JsonDocument.Parse(
            await profileAfterJoin.Content.ReadAsStringAsync());
        Assert.True(profileAfterJoinJson.RootElement.GetProperty("vinculadoAUnidad").GetBoolean());

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        using var previewOccupied = await _client.GetAsync($"/api/v1/unidades/{unitId}/vista-previa");
        using var previewOccupiedJson = JsonDocument.Parse(
            await previewOccupied.Content.ReadAsStringAsync());
        Assert.Equal(
            "Ocupado",
            previewOccupiedJson.RootElement.GetProperty("estadoOcupacion").GetString());

        using var maintenanceOn = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/mantenimiento",
            new { enMantenimiento = true });
        Assert.Equal(HttpStatusCode.OK, maintenanceOn.StatusCode);
        using var maintenanceOnJson = JsonDocument.Parse(
            await maintenanceOn.Content.ReadAsStringAsync());
        Assert.Equal(
            "Mantenimiento",
            maintenanceOnJson.RootElement.GetProperty("estadoOcupacion").GetString());

        using var maintenanceOff = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/mantenimiento",
            new { enMantenimiento = false });
        using var maintenanceOffJson = JsonDocument.Parse(
            await maintenanceOff.Content.ReadAsStringAsync());
        Assert.Equal(
            "Ocupado",
            maintenanceOffJson.RootElement.GetProperty("estadoOcupacion").GetString());

        using var tenants = await _client.GetAsync($"/api/v1/unidades/{unitId}/inquilinos");
        Assert.Equal(HttpStatusCode.OK, tenants.StatusCode);
        using var tenantsJson = JsonDocument.Parse(await tenants.Content.ReadAsStringAsync());
        var tenantsArray = tenantsJson.RootElement.EnumerateArray().ToArray();
        var tenantEntry = Assert.Single(tenantsArray);
        Assert.Equal("Ana López", tenantEntry.GetProperty("nombreCompleto").GetString());
        var tenantAccountId = tenantEntry.GetProperty("accountId").GetGuid();
        var tenantsBody = await tenants.Content.ReadAsStringAsync();
        Assert.DoesNotContain(TenantEmail, tenantsBody, StringComparison.OrdinalIgnoreCase);

        using var remove = await _client.SendAsync(
            BuildDeleteRequest(
                $"/api/v1/unidades/{unitId}/inquilinos/{tenantAccountId}",
                new { confirmacion = true }));
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        using var previewAfterRemove = await _client.GetAsync(
            $"/api/v1/unidades/{unitId}/vista-previa");
        using var previewAfterRemoveJson = JsonDocument.Parse(
            await previewAfterRemove.Content.ReadAsStringAsync());
        Assert.Equal(
            "Disponible",
            previewAfterRemoveJson.RootElement.GetProperty("estadoOcupacion").GetString());

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantToken);
        using var profileAfterRemove = await _client.GetAsync("/api/v1/perfil/mi-perfil");
        using var profileAfterRemoveJson = JsonDocument.Parse(
            await profileAfterRemove.Content.ReadAsStringAsync());
        Assert.False(
            profileAfterRemoveJson.RootElement.GetProperty("vinculadoAUnidad").GetBoolean());

        using var rejoin = await _client.PostAsJsonAsync(
            "/api/v1/unidades/unirse",
            new { codigo = code });
        Assert.Equal(HttpStatusCode.OK, rejoin.StatusCode);

        using var leave = await _client.SendAsync(
            BuildDeleteRequest($"/api/v1/unidades/{unitId}/mi-vinculo", new { confirmacion = true }));
        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);
    }

    [Fact]
    public async Task UnirseConCodigo_CodigoVencido_DebeResponder410()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var tenantToken = await AuthenticateAsync(TenantEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(
            universityId, $"Casa {Guid.NewGuid():N}", "Vencido");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        var expired = UnitInvitation.Restore(
            unitId,
            "VENCIDA1",
            DateTimeOffset.UtcNow.AddDays(-10),
            DateTimeOffset.UtcNow.AddDays(-3));
        await _factory.UnitInvitations.AddAsync(expired);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantToken);
        using var join = await _client.PostAsJsonAsync(
            "/api/v1/unidades/unirse",
            new { codigo = "VENCIDA1" });

        Assert.Equal(HttpStatusCode.Gone, join.StatusCode);
    }

    [Fact]
    public async Task UnirseConCodigo_CodigoInvalidoOVacio_DebeResponder400()
    {
        var tenantToken = await AuthenticateAsync(TenantEmail);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantToken);

        using var invalid = await _client.PostAsJsonAsync(
            "/api/v1/unidades/unirse",
            new { codigo = "NOEXISTE" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var empty = await _client.PostAsJsonAsync(
            "/api/v1/unidades/unirse",
            new { codigo = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task UnirseConCodigo_UnidadLlena_DebeResponder409()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var tenantAToken = await AuthenticateAsync(TenantEmail);
        var tenantBToken = await AuthenticateAsync(OtherTenantEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(
            universityId, $"Casa {Guid.NewGuid():N}", "Llena");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        using var invitation = await _client.GetAsync($"/api/v1/unidades/{unitId}/invitacion");
        using var invitationJson = JsonDocument.Parse(await invitation.Content.ReadAsStringAsync());
        var code = invitationJson.RootElement.GetProperty("codigo").GetString();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantAToken);
        using var first = await _client.PostAsJsonAsync(
            "/api/v1/unidades/unirse", new { codigo = code });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantBToken);
        using var second = await _client.PostAsJsonAsync(
            "/api/v1/unidades/unirse", new { codigo = code });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task UnirseConCodigo_PropietarioIntentaUnirseAsuPropiaUnidad_DebeResponder403()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(
            universityId, $"Casa {Guid.NewGuid():N}", "Propia");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        using var invitation = await _client.GetAsync($"/api/v1/unidades/{unitId}/invitacion");
        using var invitationJson = JsonDocument.Parse(await invitation.Content.ReadAsStringAsync());
        var code = invitationJson.RootElement.GetProperty("codigo").GetString();

        using var join = await _client.PostAsJsonAsync(
            "/api/v1/unidades/unirse", new { codigo = code });

        Assert.Equal(HttpStatusCode.Forbidden, join.StatusCode);
    }

    [Fact]
    public async Task Invitacion_UnidadAjena_DebeResponder403()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var otherToken = await AuthenticateAsync(TenantEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(
            universityId, $"Casa {Guid.NewGuid():N}", "Ajena");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", otherToken);
        using var response = await _client.GetAsync($"/api/v1/unidades/{unitId}/invitacion");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListarInquilinos_SinInquilinos_DevuelveListaVacia()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(
            universityId, $"Casa {Guid.NewGuid():N}", "SinInquilinos");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        using var response = await _client.GetAsync($"/api/v1/unidades/{unitId}/inquilinos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(json.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task ListarInquilinos_SoloIncluyeVinculosActivos()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var tenantToken = await AuthenticateAsync(TenantEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(
            universityId, $"Casa {Guid.NewGuid():N}", "VinculoFinalizado");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);
        using var invitation = await _client.GetAsync($"/api/v1/unidades/{unitId}/invitacion");
        using var invitationJson = JsonDocument.Parse(await invitation.Content.ReadAsStringAsync());
        var code = invitationJson.RootElement.GetProperty("codigo").GetString();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantToken);
        await _client.PostAsJsonAsync("/api/v1/unidades/unirse", new { codigo = code });
        await _client.SendAsync(
            BuildDeleteRequest($"/api/v1/unidades/{unitId}/mi-vinculo", new { confirmacion = true }));

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        using var response = await _client.GetAsync($"/api/v1/unidades/{unitId}/inquilinos");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(json.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task ListarInquilinos_UnidadAjena_DebeResponder403()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var otherToken = await AuthenticateAsync(TenantEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(
            universityId, $"Casa {Guid.NewGuid():N}", "ListaAjena");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", otherToken);
        using var response = await _client.GetAsync($"/api/v1/unidades/{unitId}/inquilinos");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListarInquilinos_SinSesion_DebeResponder401()
    {
        using var response = await _client.GetAsync(
            $"/api/v1/unidades/{Guid.NewGuid()}/inquilinos");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnirseConCodigo_DosSolicitudesSimultaneasConUnLugar_SoloUnaDebeVincularse()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var tenantAToken = await AuthenticateAsync(TenantEmail);
        var tenantBToken = await AuthenticateAsync(OtherTenantEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(
            universityId, $"Casa {Guid.NewGuid():N}", "Concurrencia");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);
        using var invitation = await _client.GetAsync($"/api/v1/unidades/{unitId}/invitacion");
        using var invitationJson = JsonDocument.Parse(await invitation.Content.ReadAsStringAsync());
        var code = invitationJson.RootElement.GetProperty("codigo").GetString();

        // Dos HttpClient independientes (uno por inquilino) para que el
        // TestServer procese ambas solicitudes de verdad en paralelo contra el
        // mismo InMemoryUnitRepository con lock, igual que MongoDB serializa el
        // findOneAndUpdate sobre el mismo documento.
        using var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantAToken);
        using var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantBToken);

        var responses = await Task.WhenAll(
            clientA.PostAsJsonAsync("/api/v1/unidades/unirse", new { codigo = code }),
            clientB.PostAsJsonAsync("/api/v1/unidades/unirse", new { codigo = code }));

        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);

            using var tenants = await _client.GetAsync($"/api/v1/unidades/{unitId}/inquilinos");
            using var tenantsJson = JsonDocument.Parse(await tenants.Content.ReadAsStringAsync());
            Assert.Single(tenantsJson.RootElement.EnumerateArray());
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task BajaInquilino_SinConfirmacion_DebeResponder400()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(
            universityId, $"Casa {Guid.NewGuid():N}", "SinConfirmar");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        using var response = await _client.SendAsync(
            BuildDeleteRequest(
                $"/api/v1/unidades/{unitId}/inquilinos/{Guid.NewGuid()}",
                new { confirmacion = false }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DejarUnidad_SinVinculoActivo_DebeResponder404()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var tenantToken = await AuthenticateAsync(TenantEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(
            universityId, $"Casa {Guid.NewGuid():N}", "SinVinculo");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantToken);
        using var response = await _client.SendAsync(
            BuildDeleteRequest($"/api/v1/unidades/{unitId}/mi-vinculo", new { confirmacion = true }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "invitacion")]
    [InlineData("PUT", "mantenimiento")]
    [InlineData("DELETE_TENANT", "inquilinos")]
    [InlineData("DELETE", "mi-vinculo")]
    public async Task RutasDeOcupacion_SinSesion_DebenResponder401(string method, string suffix)
    {
        var unitId = Guid.Empty.ToString("N");
        var path = suffix switch
        {
            "inquilinos" => $"/api/v1/unidades/{unitId}/inquilinos/{unitId}",
            _ => $"/api/v1/unidades/{unitId}/{suffix}"
        };
        var httpMethod = method switch
        {
            "DELETE_TENANT" => HttpMethod.Delete,
            _ => new HttpMethod(method)
        };

        using var request = new HttpRequestMessage(httpMethod, path);

        if (httpMethod == HttpMethod.Put || httpMethod == HttpMethod.Delete)
        {
            request.Content = JsonContent.Create(new { });
        }

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnirseConCodigo_SinSesion_DebeResponder401()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/unidades/unirse",
            new { codigo = "ABCD2345" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpRequestMessage BuildDeleteRequest(string path, object body) =>
        new(HttpMethod.Delete, path) { Content = JsonContent.Create(body) };

    private async Task<string> AuthenticateAsync(string email)
    {
        var existing = await _factory.Accounts.FindByEmailAsync(
            UniHome.Domain.Accounts.EmailAddress.Create(email));

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

    private async Task<Guid> CreatePropertyAsync(Guid universityId, string name, string address)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/propiedades",
            new
            {
                nombre = name,
                direccion = address,
                colonia = "Centro",
                tipoPropiedad = "Casa",
                universidadId = universityId,
                latitud = 19.4326,
                longitud = -99.1332
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateUnitAsync(Guid propertyId, string occupancy, int places)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/api/v1/propiedades/{propertyId}/unidades",
            new
            {
                tipoUnidad = "Habitacion",
                ocupacion = occupancy,
                lugares = places,
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
