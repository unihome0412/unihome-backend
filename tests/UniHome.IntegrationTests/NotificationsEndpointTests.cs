using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Universities;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// EP-M12-01 (HU-059): recordatorio de renta, generado al consultar
/// <c>GET /notificaciones</c>.
/// </summary>
public sealed class NotificationsEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private const string Aviso =
        "UniHome no procesa pagos ni interviene en transacciones económicas entre las partes.";
    private static readonly string[] SingleFurniture = ["Cama"];

    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public NotificationsEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Notificaciones_SinSesion_DebeResponder401()
    {
        using var response = await _client.GetAsync("/api/v1/notificaciones");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Notificaciones_SinVinculosActivos_DevuelveListaVacia()
    {
        var email = $"notificaciones.sinvinculos.{Guid.NewGuid():N}@uni.edu";
        var token = await AuthenticateAsync(email);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.GetAsync("/api/v1/notificaciones");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(json.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Notificaciones_CorteDentroDeTresDiasSinPagar_GeneraRecordatorioConAviso()
    {
        var ownerEmail = $"notificaciones.owner.{Guid.NewGuid():N}@uni.edu";
        var tenantEmail = $"notificaciones.tenant.{Guid.NewGuid():N}@uni.edu";
        var ownerToken = await AuthenticateAsync(ownerEmail);
        var tenantToken = await AuthenticateAsync(tenantEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "Recordatorio");
        var unitId = await CreateUnitAsync(propertyId);
        var code = await GetInvitationCodeAsync(unitId);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
        await JoinUnitAsync(code);

        var (periodo, diaCorte) = CurrentPeriodoWithCutoffInDays(2);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await ConfigureAndDivideRentAsync(unitId, periodo, diaCorte);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
        using var response = await _client.GetAsync("/api/v1/notificaciones");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(tenantEmail, body, StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(body);
        var reminder = Assert.Single(
            json.RootElement.EnumerateArray(),
            item => item.GetProperty("unitId").GetGuid() == unitId);
        Assert.Equal(periodo, reminder.GetProperty("periodo").GetString());
        Assert.Equal("RecordatorioRenta", reminder.GetProperty("tipo").GetString());
        Assert.Equal(Aviso, reminder.GetProperty("aviso").GetString());
    }

    [Fact]
    public async Task Notificaciones_CuotaYaCubierta_NoGeneraRecordatorio()
    {
        var ownerEmail = $"notificaciones.pagado.owner.{Guid.NewGuid():N}@uni.edu";
        var tenantEmail = $"notificaciones.pagado.tenant.{Guid.NewGuid():N}@uni.edu";
        var ownerToken = await AuthenticateAsync(ownerEmail);
        var tenantToken = await AuthenticateAsync(tenantEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "YaPagado");
        var unitId = await CreateUnitAsync(propertyId);
        var code = await GetInvitationCodeAsync(unitId);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
        await JoinUnitAsync(code);

        var (periodo, diaCorte) = CurrentPeriodoWithCutoffInDays(1);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await ConfigureAndDivideRentAsync(unitId, periodo, diaCorte);
        var tenantAccountId = await GetFirstActiveTenantIdAsync(unitId);
        using var payment = await _client.PostAsJsonAsync(
            $"/api/v1/unidades/{unitId}/renta/{periodo}/pagos/{tenantAccountId}",
            new { montoMxn = 1000m, fechaRecepcion = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        Assert.Equal(HttpStatusCode.Created, payment.StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
        using var response = await _client.GetAsync("/api/v1/notificaciones");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(
            json.RootElement.EnumerateArray(),
            item => item.GetProperty("unitId").GetGuid() == unitId);
    }

    /// <summary>Periodo "yyyy-MM" del mes en curso y un día de corte a <paramref name="daysAhead"/> días, sin desbordar el mes.</summary>
    private static (string Periodo, int DiaCorte) CurrentPeriodoWithCutoffInDays(int daysAhead)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodo = today.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var diaCorte = Math.Min(
            today.Day + daysAhead,
            DateTime.DaysInMonth(today.Year, today.Month));
        return (periodo, diaCorte);
    }

    private async Task ConfigureAndDivideRentAsync(Guid unitId, string periodo, int diaCorte)
    {
        using var configure = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/renta/{periodo}",
            new
            {
                rentaTotalMxn = 1000m,
                diaCorte,
                conceptos = new object[]
                {
                    new { nombre = "Renta", esFijo = true },
                    new { nombre = "Mantenimiento", esFijo = true },
                    new { nombre = "Luz", esFijo = false },
                    new { nombre = "Gas", esFijo = false }
                }
            });
        Assert.Equal(HttpStatusCode.OK, configure.StatusCode);

        using var division = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/renta/{periodo}/division",
            new { metodo = "Igual" });
        Assert.Equal(HttpStatusCode.OK, division.StatusCode);
    }

    private async Task<string> GetInvitationCodeAsync(Guid unitId)
    {
        using var invitation = await _client.GetAsync($"/api/v1/unidades/{unitId}/invitacion");
        using var invitationJson = JsonDocument.Parse(await invitation.Content.ReadAsStringAsync());
        return invitationJson.RootElement.GetProperty("codigo").GetString()!;
    }

    private async Task JoinUnitAsync(string code)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/unidades/unirse", new { codigo = code });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<Guid> GetFirstActiveTenantIdAsync(Guid unitId)
    {
        using var response = await _client.GetAsync($"/api/v1/unidades/{unitId}/inquilinos");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.EnumerateArray().First().GetProperty("accountId").GetGuid();
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
