using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Application.Universities.Abstractions;
using UniHome.Domain.Universities;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// EP-M10 (HU-050/051/052/053/054/055/056/057/058/060/061): configuración,
/// división, pagos informativos, vista del inquilino y resumen mensual sobre
/// la renta de una unidad por periodo.
/// </summary>
public sealed class RentEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private const string OwnerEmail = "renta.owner@uni.edu";
    private const string TenantEmail = "renta.tenant@uni.edu";
    private const string OtherEmail = "renta.other@uni.edu";
    private const string Aviso =
        "UniHome no procesa pagos ni interviene en transacciones económicas entre las partes.";
    private static readonly string[] SingleFurniture = ["Cama"];

    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public RentEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FlujoCompleto_ConfigurarDividirRegistrarPagoYConsultarComoPropietarioEInquilino()
    {
        // Correos únicos: a diferencia de las demás pruebas, el resumen mensual
        // agrega TODAS las unidades del titular para el periodo, así que
        // reutilizar OwnerEmail/TenantEmail contaminaría el resultado con las
        // unidades que crean otras pruebas de esta misma clase (mismo fixture).
        var flujoOwnerEmail = $"renta.flujo.owner.{Guid.NewGuid():N}@uni.edu";
        var flujoTenantEmail = $"renta.flujo.tenant.{Guid.NewGuid():N}@uni.edu";
        var ownerToken = await AuthenticateAsync(flujoOwnerEmail);
        var tenantToken = await AuthenticateAsync(flujoTenantEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "Renta");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);
        var code = await GetInvitationCodeAsync(unitId);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
        await JoinUnitAsync(code);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var tenantAccountId = await GetFirstActiveTenantIdAsync(unitId);

        using var configure = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/renta/2026-11",
            new { rentaTotalMxn = 1000m, diaCorte = 5, conceptos = AllConcepts() });
        Assert.Equal(HttpStatusCode.OK, configure.StatusCode);
        var configureBody = await configure.Content.ReadAsStringAsync();
        Assert.Contains(Aviso, configureBody, StringComparison.Ordinal);
        Assert.DoesNotContain(flujoOwnerEmail, configureBody, StringComparison.OrdinalIgnoreCase);

        using var division = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/renta/2026-11/division",
            new { metodo = "Igual" });
        Assert.Equal(HttpStatusCode.OK, division.StatusCode);

        using var registerPayment = await _client.PostAsJsonAsync(
            $"/api/v1/unidades/{unitId}/renta/2026-11/pagos/{tenantAccountId}",
            new { montoMxn = 1000m, fechaRecepcion = "2026-11-01" });
        Assert.Equal(HttpStatusCode.Created, registerPayment.StatusCode);
        var registerPaymentBody = await registerPayment.Content.ReadAsStringAsync();
        Assert.Contains(Aviso, registerPaymentBody, StringComparison.Ordinal);

        using var listPayments = await _client.GetAsync($"/api/v1/unidades/{unitId}/renta/2026-11/pagos");
        Assert.Equal(HttpStatusCode.OK, listPayments.StatusCode);
        var listPaymentsBody = await listPayments.Content.ReadAsStringAsync();
        Assert.Contains(Aviso, listPaymentsBody, StringComparison.Ordinal);
        Assert.DoesNotContain(flujoTenantEmail, listPaymentsBody, StringComparison.OrdinalIgnoreCase);
        using var listPaymentsJson = JsonDocument.Parse(listPaymentsBody);
        var tenantEntry = Assert.Single(listPaymentsJson.RootElement.GetProperty("inquilinos").EnumerateArray());
        Assert.Equal("Registrado", tenantEntry.GetProperty("estado").GetString());
        Assert.Equal(0m, tenantEntry.GetProperty("saldoMxn").GetDecimal());

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);

        using var miRenta = await _client.GetAsync($"/api/v1/unidades/{unitId}/renta/2026-11/mi-renta");
        Assert.Equal(HttpStatusCode.OK, miRenta.StatusCode);
        var miRentaBody = await miRenta.Content.ReadAsStringAsync();
        Assert.Contains(Aviso, miRentaBody, StringComparison.Ordinal);
        Assert.DoesNotContain(flujoTenantEmail, miRentaBody, StringComparison.OrdinalIgnoreCase);
        using var miRentaJson = JsonDocument.Parse(miRentaBody);
        Assert.Equal(1000m, miRentaJson.RootElement.GetProperty("cuotaMxn").GetDecimal());
        Assert.Equal("Registrado", miRentaJson.RootElement.GetProperty("estado").GetString());

        using var misPagos = await _client.GetAsync($"/api/v1/unidades/{unitId}/renta/mis-pagos");
        Assert.Equal(HttpStatusCode.OK, misPagos.StatusCode);
        var misPagosBody = await misPagos.Content.ReadAsStringAsync();
        Assert.Contains(Aviso, misPagosBody, StringComparison.Ordinal);
        using var misPagosJson = JsonDocument.Parse(misPagosBody);
        Assert.Single(misPagosJson.RootElement.GetProperty("pagos").EnumerateArray());

        using var resumenTenant = await _client.GetAsync("/api/v1/mi-resumen-renta/2026-11");
        Assert.Equal(HttpStatusCode.OK, resumenTenant.StatusCode);
        var resumenTenantBody = await resumenTenant.Content.ReadAsStringAsync();
        Assert.Contains(Aviso, resumenTenantBody, StringComparison.Ordinal);
        using var resumenTenantJson = JsonDocument.Parse(resumenTenantBody);
        Assert.Empty(resumenTenantJson.RootElement.GetProperty("comoPropietario").EnumerateArray());
        Assert.Single(resumenTenantJson.RootElement.GetProperty("comoInquilino").EnumerateArray());

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        using var resumenOwner = await _client.GetAsync("/api/v1/mi-resumen-renta/2026-11");
        Assert.Equal(HttpStatusCode.OK, resumenOwner.StatusCode);
        using var resumenOwnerJson = JsonDocument.Parse(await resumenOwner.Content.ReadAsStringAsync());
        var ownerEntry = Assert.Single(resumenOwnerJson.RootElement.GetProperty("comoPropietario").EnumerateArray());
        Assert.Equal(1000m, ownerEntry.GetProperty("recaudadoMxn").GetDecimal());
        Assert.Equal(0m, ownerEntry.GetProperty("pendienteMxn").GetDecimal());
        Assert.False(ownerEntry.GetProperty("divisionPendiente").GetBoolean());
    }

    [Fact]
    public async Task Configurar_SinSesion_DebeResponder401()
    {
        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{Guid.NewGuid()}/renta/2026-11",
            new { rentaTotalMxn = 1000m, diaCorte = 5, conceptos = AllConcepts() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Configurar_UnidadInexistente_DebeResponder404()
    {
        var token = await AuthenticateAsync(OwnerEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{Guid.NewGuid()}/renta/2026-11",
            new { rentaTotalMxn = 1000m, diaCorte = 5, conceptos = AllConcepts() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Configurar_UnidadAjena_DebeResponder403()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var otherToken = await AuthenticateAsync(OtherEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "Ajena");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/renta/2026-11",
            new { rentaTotalMxn = 1000m, diaCorte = 5, conceptos = AllConcepts() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Configurar_DiaCorteFueraDeRango_DebeResponder400()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "DiaCorte");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/renta/2026-11",
            new { rentaTotalMxn = 1000m, diaCorte = 32, conceptos = AllConcepts() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Configurar_SinCampoRentaTotalMxn_DebeResponder400()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "SinRentaTotal");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/renta/2026-11",
            new { diaCorte = 5, conceptos = AllConcepts() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Configurar_SinCampoDiaCorte_DebeResponder400()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "SinDiaCorte");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/renta/2026-11",
            new { rentaTotalMxn = 1000m, conceptos = AllConcepts() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Configurar_CuerpoDemasiadoGrande_DebeResponder413()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{Guid.NewGuid()}/renta/2026-11",
            new
            {
                rentaTotalMxn = 1000m,
                diaCorte = 5,
                conceptos = AllConcepts(),
                padding = new string('a', 16 * 1024)
            });

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Dividir_SinSesion_DebeResponder401()
    {
        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{Guid.NewGuid()}/renta/2026-11/division",
            new { metodo = "Igual" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dividir_RentaNoConfigurada_DebeResponder404()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "SinRenta");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/renta/2026-11/division",
            new { metodo = "Igual" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Dividir_UnidadAjena_DebeResponder403()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var otherToken = await AuthenticateAsync(OtherEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "DivisionAjena");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/renta/2026-11/division",
            new { metodo = "Igual" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Dividir_SinCampoMetodo_DebeResponder400()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{Guid.NewGuid()}/renta/2026-11/division",
            new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Dividir_PersonalizadaConSumaIncorrecta_DebeResponder400()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var tenantToken = await AuthenticateAsync(TenantEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "Personalizada");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);
        var code = await GetInvitationCodeAsync(unitId);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
        await JoinUnitAsync(code);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var tenantAccountId = await GetFirstActiveTenantIdAsync(unitId);
        await ConfigureRentAsync(unitId, 1000m, 5);

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/renta/2026-11/division",
            new
            {
                metodo = "Personalizada",
                cuotas = new[] { new { inquilinoAccountId = tenantAccountId, montoMxn = 500m } }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegistrarPago_SinSesion_DebeResponder401()
    {
        using var response = await _client.PostAsJsonAsync(
            $"/api/v1/unidades/{Guid.NewGuid()}/renta/2026-11/pagos/{Guid.NewGuid()}",
            new { montoMxn = 500m, fechaRecepcion = "2026-11-01" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RegistrarPago_UnidadAjena_DebeResponder403()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var otherToken = await AuthenticateAsync(OtherEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "PagoAjeno");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        using var response = await _client.PostAsJsonAsync(
            $"/api/v1/unidades/{unitId}/renta/2026-11/pagos/{Guid.NewGuid()}",
            new { montoMxn = 500m, fechaRecepcion = "2026-11-01" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RegistrarPago_InquilinoSinVinculoActivo_DebeResponder400()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "SinVinculo");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);
        await ConfigureRentAsync(unitId, 1000m, 5);

        using var response = await _client.PostAsJsonAsync(
            $"/api/v1/unidades/{unitId}/renta/2026-11/pagos/{Guid.NewGuid()}",
            new { montoMxn = 500m, fechaRecepcion = "2026-11-01" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegistrarPago_SinCampoMontoMxn_DebeResponder400()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        using var response = await _client.PostAsJsonAsync(
            $"/api/v1/unidades/{Guid.NewGuid()}/renta/2026-11/pagos/{Guid.NewGuid()}",
            new { fechaRecepcion = "2026-11-01" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegistrarPago_SinCampoFechaRecepcion_DebeResponder400()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        using var response = await _client.PostAsJsonAsync(
            $"/api/v1/unidades/{Guid.NewGuid()}/renta/2026-11/pagos/{Guid.NewGuid()}",
            new { montoMxn = 500m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListarPagos_SinSesion_DebeResponder401()
    {
        using var response = await _client.GetAsync(
            $"/api/v1/unidades/{Guid.NewGuid()}/renta/2026-11/pagos");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListarPagos_UnidadAjena_DebeResponder403()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var otherToken = await AuthenticateAsync(OtherEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "ListaAjena");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        using var response = await _client.GetAsync($"/api/v1/unidades/{unitId}/renta/2026-11/pagos");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListarPagos_DivisionPendiente_DebeResponder422()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "DivisionPendiente");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);
        await ConfigureRentAsync(unitId, 1000m, 5);

        using var response = await _client.GetAsync($"/api/v1/unidades/{unitId}/renta/2026-11/pagos");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task MiRenta_SinSesion_DebeResponder401()
    {
        using var response = await _client.GetAsync(
            $"/api/v1/unidades/{Guid.NewGuid()}/renta/2026-11/mi-renta");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MiRenta_SinVinculoActivo_DebeResponder403()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var otherToken = await AuthenticateAsync(OtherEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "MiRentaAjena");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        using var response = await _client.GetAsync($"/api/v1/unidades/{unitId}/renta/2026-11/mi-renta");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MiRenta_RentaNoConfigurada_DebeResponder404()
    {
        var ownerToken = await AuthenticateAsync(OwnerEmail);
        var tenantToken = await AuthenticateAsync(TenantEmail);
        var universityId = await SeedUniversityAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var propertyId = await CreatePropertyAsync(universityId, $"Casa {Guid.NewGuid():N}", "MiRentaSinConfigurar");
        var unitId = await CreateUnitAsync(propertyId, "Individual", 1);
        var code = await GetInvitationCodeAsync(unitId);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
        await JoinUnitAsync(code);

        using var response = await _client.GetAsync($"/api/v1/unidades/{unitId}/renta/2026-11/mi-renta");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MisPagos_SinSesion_DebeResponder401()
    {
        using var response = await _client.GetAsync($"/api/v1/unidades/{Guid.NewGuid()}/renta/mis-pagos");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MisPagos_UnidadInexistente_DebeResponder404()
    {
        var tenantToken = await AuthenticateAsync(TenantEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);

        using var response = await _client.GetAsync($"/api/v1/unidades/{Guid.NewGuid()}/renta/mis-pagos");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResumenMensual_SinSesion_DebeResponder401()
    {
        using var response = await _client.GetAsync("/api/v1/mi-resumen-renta/2026-11");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ResumenMensual_PeriodoInvalido_DebeResponder400()
    {
        var token = await AuthenticateAsync(OwnerEmail);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.GetAsync("/api/v1/mi-resumen-renta/noviembre");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static object[] AllConcepts() =>
    [
        new { nombre = "Renta", esFijo = true },
        new { nombre = "Mantenimiento", esFijo = true },
        new { nombre = "Luz", esFijo = false },
        new { nombre = "Gas", esFijo = false }
    ];

    private async Task ConfigureRentAsync(Guid unitId, decimal rentaTotalMxn, int diaCorte)
    {
        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/unidades/{unitId}/renta/2026-11",
            new { rentaTotalMxn, diaCorte, conceptos = AllConcepts() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
