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

/// <summary>
/// EP-M08 (HU-067/068/069/070/086/089): conversaciones y mensajes entre el
/// propietario de una unidad y quien la contacta desde su ficha.
/// </summary>
public sealed class ChatEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private const string OwnerEmail = "chat.owner@uni.edu";
    private const string RequesterEmail = "chat.requester@uni.edu";
    private const string OutsiderEmail = "chat.outsider@uni.edu";
    private static readonly string[] SingleFurniture = ["Cama"];

    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public ChatEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IniciarConversacion_UnidadAprobada_CreaYReutilizaConversacion()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var requesterToken = await AuthenticateAsync(RequesterEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "Chat1");
        var unitId = await CreateUnitAsync(propertyId);
        await ApproveUnitAsync(unitId);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        using var first = await _client.PostAsJsonAsync($"/api/v1/unidades/{unitId}/conversaciones", new { });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var conversationId = firstJson.RootElement.GetProperty("conversacionId").GetGuid();
        Assert.Equal(unitId, firstJson.RootElement.GetProperty("unidadId").GetGuid());
        Assert.Equal("Ana López", firstJson.RootElement.GetProperty("otroNombreCompleto").GetString());

        using var second = await _client.PostAsJsonAsync($"/api/v1/unidades/{unitId}/conversaciones", new { });
        using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal(conversationId, secondJson.RootElement.GetProperty("conversacionId").GetGuid());
    }

    [Fact]
    public async Task IniciarConversacion_ConSuPropiaUnidad_DebeResponder403()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "Chat2");
        var unitId = await CreateUnitAsync(propertyId);
        await ApproveUnitAsync(unitId);

        using var response = await _client.PostAsJsonAsync($"/api/v1/unidades/{unitId}/conversaciones", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task IniciarConversacion_UnidadNoAprobada_DebeResponder404()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var requesterToken = await AuthenticateAsync(RequesterEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "Chat3");
        var unitId = await CreateUnitAsync(propertyId);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        using var response = await _client.PostAsJsonAsync($"/api/v1/unidades/{unitId}/conversaciones", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EnviarMensaje_Participante_GuardaYDevuelveAvisoCifrado()
    {
        var conversationId = await StartApprovedConversationAsync("chatmsg1");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await AuthenticateAsync(RequesterEmail));
        using var response = await _client.PostAsJsonAsync(
            $"/api/v1/conversaciones/{conversationId}/mensajes",
            new { texto = "Hola, ¿sigue disponible?" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Hola, ¿sigue disponible?", json.RootElement.GetProperty("texto").GetString());
        Assert.Equal(
            "Comunicación cifrada en tránsito.",
            json.RootElement.GetProperty("aviso").GetString());
    }

    [Fact]
    public async Task EnviarMensaje_NoParticipante_DebeResponder403()
    {
        var conversationId = await StartApprovedConversationAsync("chatmsg2");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await AuthenticateAsync(OutsiderEmail));
        using var response = await _client.PostAsJsonAsync(
            $"/api/v1/conversaciones/{conversationId}/mensajes",
            new { texto = "Intento ajeno" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EnviarMensaje_TextoVacio_DebeResponder400()
    {
        var conversationId = await StartApprovedConversationAsync("chatmsg3");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await AuthenticateAsync(RequesterEmail));
        using var response = await _client.PostAsJsonAsync(
            $"/api/v1/conversaciones/{conversationId}/mensajes",
            new { texto = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EnviarMensaje_TextoExcedeLongitud_DebeResponder400()
    {
        var conversationId = await StartApprovedConversationAsync("chatmsg4");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await AuthenticateAsync(RequesterEmail));
        using var response = await _client.PostAsJsonAsync(
            $"/api/v1/conversaciones/{conversationId}/mensajes",
            new { texto = new string('a', 1001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListarMensajes_Paginacion_DevuelveMasRecientesPrimeroYCursor()
    {
        var conversationId = await StartApprovedConversationAsync("chatmsg5");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await AuthenticateAsync(RequesterEmail));

        foreach (var text in new[] { "Uno", "Dos", "Tres" })
        {
            using var send = await _client.PostAsJsonAsync(
                $"/api/v1/conversaciones/{conversationId}/mensajes", new { texto = text });
            Assert.Equal(HttpStatusCode.Created, send.StatusCode);
        }

        using var page = await _client.GetAsync(
            $"/api/v1/conversaciones/{conversationId}/mensajes?limite=2");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        using var pageJson = JsonDocument.Parse(await page.Content.ReadAsStringAsync());
        var items = pageJson.RootElement.GetProperty("mensajes").EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Equal("Tres", items[0].GetProperty("texto").GetString());
        Assert.Equal("Dos", items[1].GetProperty("texto").GetString());
        var cursor = pageJson.RootElement.GetProperty("siguienteCursor").GetDateTimeOffset();

        using var nextPage = await _client.GetAsync(
            $"/api/v1/conversaciones/{conversationId}/mensajes?limite=2&antes={Uri.EscapeDataString(cursor.ToString("O"))}");
        using var nextPageJson = JsonDocument.Parse(await nextPage.Content.ReadAsStringAsync());
        var nextItems = nextPageJson.RootElement.GetProperty("mensajes").EnumerateArray().ToArray();
        Assert.Single(nextItems);
        Assert.Equal("Uno", nextItems[0].GetProperty("texto").GetString());
        Assert.Equal(JsonValueKind.Null, nextPageJson.RootElement.GetProperty("siguienteCursor").ValueKind);
    }

    [Fact]
    public async Task ListarMensajes_NoParticipante_DebeResponder403()
    {
        var conversationId = await StartApprovedConversationAsync("chatmsg6");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await AuthenticateAsync(OutsiderEmail));
        using var response = await _client.GetAsync($"/api/v1/conversaciones/{conversationId}/mensajes");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListarConversaciones_DevuelveContextoUltimoMensajeYNuncaCorreoNiTelefono()
    {
        var conversationId = await StartApprovedConversationAsync("chatlist1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await AuthenticateAsync(RequesterEmail));
        using var send = await _client.PostAsJsonAsync(
            $"/api/v1/conversaciones/{conversationId}/mensajes", new { texto = "Hola, ¿sigue disponible?" });
        Assert.Equal(HttpStatusCode.Created, send.StatusCode);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await AuthenticateAsync(OwnerEmail));
        using var list = await _client.GetAsync("/api/v1/conversaciones");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain(RequesterEmail, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(OwnerEmail, body, StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(body);
        var entry = Assert.Single(
            json.RootElement.EnumerateArray(),
            element => element.GetProperty("conversacionId").GetGuid() == conversationId);
        Assert.Equal("Hola, ¿sigue disponible?", entry.GetProperty("ultimoMensaje").GetString());
        Assert.Equal("Ana López", entry.GetProperty("otroNombreCompleto").GetString());
    }

    [Theory]
    [InlineData("POST", "/api/v1/unidades/{unitId}/conversaciones")]
    [InlineData("GET", "/api/v1/conversaciones")]
    [InlineData("GET", "/api/v1/conversaciones/{conversationId}/mensajes")]
    [InlineData("POST", "/api/v1/conversaciones/{conversationId}/mensajes")]
    public async Task RutasDeChat_SinSesion_DebenResponder401(string method, string template)
    {
        var placeholder = Guid.Empty.ToString("N");
        var path = template
            .Replace("{unitId}", placeholder, StringComparison.Ordinal)
            .Replace("{conversationId}", placeholder, StringComparison.Ordinal);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { });
        }

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<Guid> StartApprovedConversationAsync(string slug)
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var requesterToken = await AuthenticateAsync(RequesterEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", slug);
        var unitId = await CreateUnitAsync(propertyId);
        await ApproveUnitAsync(unitId);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", requesterToken);
        using var start = await _client.PostAsJsonAsync($"/api/v1/unidades/{unitId}/conversaciones", new { });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        using var startJson = JsonDocument.Parse(await start.Content.ReadAsStringAsync());
        return startJson.RootElement.GetProperty("conversacionId").GetGuid();
    }

    private async Task ApproveUnitAsync(Guid unitId)
    {
        var unit = await _factory.Units.FindByIdAsync(unitId) ??
            throw new InvalidOperationException("La unidad de prueba no existe.");
        _factory.Units.Replace(Unit.Restore(
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
            PublicationState.Aprobada,
            unit.CreatedAt,
            unit.Availability));
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
            Guid.NewGuid(),
            $"Universidad {Guid.NewGuid():N}",
            true,
            DateTimeOffset.UtcNow);
        await _factory.Universities.AddAsync(university);
        _factory.Universities.SetLocation(university.Id, new GeoCoordinate(19.4326, -99.1332));
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

    private async Task<Guid> CreateUnitAsync(Guid propertyId)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/api/v1/propiedades/{propertyId}/unidades",
            new
            {
                tipoUnidad = "Habitacion",
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
