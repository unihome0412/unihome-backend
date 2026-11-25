using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Domain.Universities;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// EP-M11 (HU-062/063/064/065/066): reportes de reparación de una unidad.
/// </summary>
public sealed class ReparacionesEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
    private static readonly string[] SingleFurniture = ["Cama"];

    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public ReparacionesEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Crear_ComoPropietarioSinFoto_DebeResponder201ConEstadoPendiente()
    {
        var ownerToken = await AuthenticateAsync($"reparaciones.owner.{Guid.NewGuid():N}@uni.edu");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var universityId = await SeedUniversityAsync();
        var propertyId = await CreatePropertyAsync(universityId);
        var unitId = await CreateUnitAsync(propertyId);

        using var response = await CreateReparacionAsync(unitId, "Fuga de agua", "Hay una fuga en el baño");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal("Fuga de agua", root.GetProperty("titulo").GetString());
        Assert.Equal("Pendiente", root.GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("foto").ValueKind);
    }

    [Fact]
    public async Task Crear_ComoInquilinoConFoto_DebeResponder201ConFotoAdjunta()
    {
        var ownerToken = await AuthenticateAsync($"reparaciones.owner2.{Guid.NewGuid():N}@uni.edu");
        var tenantToken = await AuthenticateAsync($"reparaciones.tenant.{Guid.NewGuid():N}@uni.edu");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var universityId = await SeedUniversityAsync();
        var propertyId = await CreatePropertyAsync(universityId);
        var unitId = await CreateUnitAsync(propertyId);
        var code = await GetInvitationCodeAsync(unitId);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
        await JoinUnitAsync(code);

        using var response = await CreateReparacionAsync(
            unitId, "Foco fundido", "El foco de la entrada no funciona", withPhoto: true);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var foto = document.RootElement.GetProperty("foto");
        Assert.Equal("image/png", foto.GetProperty("contentType").GetString());
        Assert.True(foto.GetProperty("sizeBytes").GetInt64() > 0);
    }

    [Fact]
    public async Task Crear_UnidadInexistente_DebeResponder404()
    {
        var ownerToken = await AuthenticateAsync($"reparaciones.owner3.{Guid.NewGuid():N}@uni.edu");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        using var response = await CreateReparacionAsync(Guid.NewGuid(), "Título", "Descripción");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Crear_CuentaSinAccesoALaUnidad_DebeResponder403()
    {
        var ownerToken = await AuthenticateAsync($"reparaciones.owner4.{Guid.NewGuid():N}@uni.edu");
        var otherToken = await AuthenticateAsync($"reparaciones.other.{Guid.NewGuid():N}@uni.edu");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var universityId = await SeedUniversityAsync();
        var propertyId = await CreatePropertyAsync(universityId);
        var unitId = await CreateUnitAsync(propertyId);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

        using var response = await CreateReparacionAsync(unitId, "Título", "Descripción");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Crear_ConTituloVacio_DebeResponder400()
    {
        var ownerToken = await AuthenticateAsync($"reparaciones.owner5.{Guid.NewGuid():N}@uni.edu");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var universityId = await SeedUniversityAsync();
        var propertyId = await CreatePropertyAsync(universityId);
        var unitId = await CreateUnitAsync(propertyId);

        using var response = await CreateReparacionAsync(unitId, string.Empty, "Descripción");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Crear_SinSesion_DebeResponder401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        using var response = await CreateReparacionAsync(Guid.NewGuid(), "Título", "Descripción");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Listar_ComoPropietario_DebeIncluirEstadoYQuienLoLevantoSinCorreo()
    {
        var ownerEmail = $"reparaciones.owner6.{Guid.NewGuid():N}@uni.edu";
        var tenantEmail = $"reparaciones.tenant2.{Guid.NewGuid():N}@uni.edu";
        var ownerToken = await AuthenticateAsync(ownerEmail);
        var tenantToken = await AuthenticateAsync(tenantEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var universityId = await SeedUniversityAsync();
        var propertyId = await CreatePropertyAsync(universityId);
        var unitId = await CreateUnitAsync(propertyId);
        var code = await GetInvitationCodeAsync(unitId);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
        await JoinUnitAsync(code);
        using var create = await CreateReparacionAsync(unitId, "Chapa dañada", "La puerta no cierra bien");
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        using var response = await _client.GetAsync($"/api/v1/unidades/{unitId}/reparaciones");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(tenantEmail, body, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(body);
        var item = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("Chapa dañada", item.GetProperty("titulo").GetString());
        Assert.Equal("Pendiente", item.GetProperty("estado").GetString());
        Assert.Equal("Ana López", item.GetProperty("reportadoPorNombreCompleto").GetString());
    }

    [Fact]
    public async Task Listar_CuentaSinAccesoALaUnidad_DebeResponder403()
    {
        var ownerToken = await AuthenticateAsync($"reparaciones.owner7.{Guid.NewGuid():N}@uni.edu");
        var otherToken = await AuthenticateAsync($"reparaciones.other2.{Guid.NewGuid():N}@uni.edu");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var universityId = await SeedUniversityAsync();
        var propertyId = await CreatePropertyAsync(universityId);
        var unitId = await CreateUnitAsync(propertyId);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        using var response = await _client.GetAsync($"/api/v1/unidades/{unitId}/reparaciones");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Listar_SinSesion_DebeResponder401()
    {
        using var response = await _client.GetAsync($"/api/v1/unidades/{Guid.NewGuid()}/reparaciones");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CambiarEstado_ComoPropietario_DebeActualizarYNotificarAlInquilino()
    {
        var ownerToken = await AuthenticateAsync($"reparaciones.owner8.{Guid.NewGuid():N}@uni.edu");
        var tenantToken = await AuthenticateAsync($"reparaciones.tenant3.{Guid.NewGuid():N}@uni.edu");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var universityId = await SeedUniversityAsync();
        var propertyId = await CreatePropertyAsync(universityId);
        var unitId = await CreateUnitAsync(propertyId);
        var code = await GetInvitationCodeAsync(unitId);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
        await JoinUnitAsync(code);
        using var create = await CreateReparacionAsync(unitId, "Aire acondicionado", "No enfría");
        using var createDocument = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var reparacionId = createDocument.RootElement.GetProperty("id").GetGuid();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/reparaciones/{reparacionId}/estado",
            new { estado = "EnProceso" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("EnProceso", document.RootElement.GetProperty("estado").GetString());

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
        using var notifications = await _client.GetAsync("/api/v1/notificaciones");
        Assert.Equal(HttpStatusCode.OK, notifications.StatusCode);
        using var notificationsDocument = JsonDocument.Parse(await notifications.Content.ReadAsStringAsync());
        Assert.Contains(
            notificationsDocument.RootElement.EnumerateArray(),
            item => item.GetProperty("tipo").GetString() == "ReparacionActualizada" &&
                    item.GetProperty("reparacionId").GetGuid() == reparacionId);
    }

    [Fact]
    public async Task CambiarEstado_ComoInquilino_DebeResponder403()
    {
        var ownerToken = await AuthenticateAsync($"reparaciones.owner9.{Guid.NewGuid():N}@uni.edu");
        var tenantToken = await AuthenticateAsync($"reparaciones.tenant4.{Guid.NewGuid():N}@uni.edu");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var universityId = await SeedUniversityAsync();
        var propertyId = await CreatePropertyAsync(universityId);
        var unitId = await CreateUnitAsync(propertyId);
        var code = await GetInvitationCodeAsync(unitId);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
        await JoinUnitAsync(code);
        using var create = await CreateReparacionAsync(unitId, "Cerradura", "No abre");
        using var createDocument = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var reparacionId = createDocument.RootElement.GetProperty("id").GetGuid();

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/reparaciones/{reparacionId}/estado",
            new { estado = "Atendido" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CambiarEstado_ReporteInexistente_DebeResponder404()
    {
        var ownerToken = await AuthenticateAsync($"reparaciones.owner10.{Guid.NewGuid():N}@uni.edu");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var universityId = await SeedUniversityAsync();
        var propertyId = await CreatePropertyAsync(universityId);
        var unitId = await CreateUnitAsync(propertyId);

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/reparaciones/{Guid.NewGuid()}/estado",
            new { estado = "Atendido" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CambiarEstado_SinSesion_DebeResponder401()
    {
        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{Guid.NewGuid()}/reparaciones/{Guid.NewGuid()}/estado",
            new { estado = "Atendido" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpResponseMessage> CreateReparacionAsync(
        Guid unitId,
        string titulo,
        string descripcion,
        bool withPhoto = false)
    {
        using var multipart = new MultipartFormDataContent
        {
            { new StringContent(titulo), "Titulo" },
            { new StringContent(descripcion), "Descripcion" }
        };

        if (withPhoto)
        {
            var file = new ByteArrayContent(PngBytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            multipart.Add(file, "Foto", "foto.png");
        }

        return await _client
            .PostAsync($"/api/v1/unidades/{unitId}/reparaciones", multipart)
            .ConfigureAwait(false);
    }

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
            Guid.NewGuid(), $"Universidad {Guid.NewGuid():N}", true, DateTimeOffset.UtcNow);
        await _factory.Universities.AddAsync(university);
        return university.Id;
    }

    private async Task<Guid> CreatePropertyAsync(Guid universityId)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/propiedades",
            new
            {
                nombre = $"Casa {Guid.NewGuid():N}",
                direccion = "Calle 1",
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

    private async Task<Guid> CreateUnitAsync(Guid propertyId)
    {
        using var response = await _client.PostAsJsonAsync(
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
                muebles = SingleFurniture
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<string> GetInvitationCodeAsync(Guid unitId)
    {
        using var invitation = await _client.GetAsync($"/api/v1/unidades/{unitId}/invitacion");
        using var invitationJson = JsonDocument.Parse(await invitation.Content.ReadAsStringAsync());
        return invitationJson.RootElement.GetProperty("codigo").GetString()!;
    }

    private async Task JoinUnitAsync(string code)
    {
        using var response = await _client.PostAsJsonAsync("/api/v1/unidades/unirse", new { codigo = code });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
