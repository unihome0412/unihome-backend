using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Domain.Accounts;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>EP-M13 (HU-072/073/074): administración de cuentas de usuario.</summary>
public sealed class AdminCuentasEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public AdminCuentasEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Buscar_ComoAdmin_DebeEncontrarPorNombreOCorreoYPaginar()
    {
        await AuthenticateAsAdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var email = $"buscar.{marker}@uni.edu";
        await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var response = await _client.GetAsync(
            $"/api/v1/admin/cuentas?buscar={Uri.EscapeDataString(marker)}&page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Total-Count"));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(email, item.GetProperty("correo").GetString());
        Assert.Equal("Active", item.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Buscar_FiltradoPorBuscanAlojamiento_DebeExcluirCuentasConPublicaciones()
    {
        await AuthenticateAsAdminAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var sinUnidadesEmail = $"sinunidades.{marker}@uni.edu";
        await AuthTestHelpers.SeedAccountAsync(_factory, sinUnidadesEmail);

        using var response = await _client.GetAsync(
            $"/api/v1/admin/cuentas?buscar={Uri.EscapeDataString(marker)}&filtro=BuscanAlojamiento");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(
            document.RootElement.EnumerateArray(),
            item => item.GetProperty("correo").GetString() == sinUnidadesEmail);
    }

    [Fact]
    public async Task Buscar_ConFiltroInvalido_DebeResponder400()
    {
        await AuthenticateAsAdminAsync();

        using var response = await _client.GetAsync("/api/v1/admin/cuentas?filtro=NoExiste");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Buscar_SinPrivilegioAdministrativo_DebeResponder403()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        using var response = await _client.GetAsync("/api/v1/admin/cuentas");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Buscar_SinSesion_DebeResponder401()
    {
        using var response = await _client.GetAsync("/api/v1/admin/cuentas");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Suspender_ComoAdmin_DebeResponder200YRevocarSesionesDelUsuario()
    {
        await AuthenticateAsAdminAsync();
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var userLogin = await AuthTestHelpers.LoginAsync(_client, email);
        var userTokens = await AuthTestHelpers.ReadTokensAsync(userLogin);

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/admin/cuentas/{account.Id}/estado",
            new { suspendida = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Suspended", document.RootElement.GetProperty("estado").GetString());

        using var refresh = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new { refreshToken = userTokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Reactivar_SobreCuentaSuspendida_DebeResponder200YVolverActiva()
    {
        await AuthenticateAsAdminAsync();
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(
            _factory, email, status: AccountStatus.Suspended);

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/admin/cuentas/{account.Id}/estado",
            new { suspendida = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Active", document.RootElement.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task CambiarEstado_CuentaInexistente_DebeResponder404()
    {
        await AuthenticateAsAdminAsync();

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/admin/cuentas/{Guid.NewGuid()}/estado",
            new { suspendida = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CambiarEstado_SinPrivilegioAdministrativo_DebeResponder403()
    {
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/admin/cuentas/{account.Id}/estado",
            new { suspendida = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Editar_ComoAdmin_DebeActualizarNombreYCorreo()
    {
        await AuthenticateAsAdminAsync();
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);
        var nuevoCorreo = AuthTestHelpers.UniqueEmail();

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/admin/cuentas/{account.Id}",
            new { nombreCompleto = "Nuevo Nombre", correo = nuevoCorreo });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Nuevo Nombre", document.RootElement.GetProperty("nombreCompleto").GetString());
        Assert.Equal(nuevoCorreo, document.RootElement.GetProperty("correo").GetString());

        var stored = await _factory.Accounts.FindByIdAsync(account.Id);
        Assert.Equal(nuevoCorreo, stored!.Email.Value);
    }

    [Fact]
    public async Task Editar_ConCorreoYaUsado_DebeResponder400()
    {
        await AuthenticateAsAdminAsync();
        var existingEmail = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, existingEmail);
        var targetEmail = AuthTestHelpers.UniqueEmail();
        var target = await AuthTestHelpers.SeedAccountAsync(_factory, targetEmail);

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/admin/cuentas/{target.Id}",
            new { nombreCompleto = "Nombre", correo = existingEmail });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Editar_CuentaInexistente_DebeResponder404()
    {
        await AuthenticateAsAdminAsync();

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/admin/cuentas/{Guid.NewGuid()}",
            new { nombreCompleto = "Nombre", correo = AuthTestHelpers.UniqueEmail() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Editar_SinPrivilegioAdministrativo_DebeResponder403()
    {
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        using var response = await _client.PutAsJsonAsync(
            $"/api/v1/admin/cuentas/{account.Id}",
            new { nombreCompleto = "Nombre", correo = AuthTestHelpers.UniqueEmail() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Respuestas_NuncaExponenHashDeContrasena()
    {
        await AuthenticateAsAdminAsync();
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var response = await _client.GetAsync(
            $"/api/v1/admin/cuentas?buscar={Uri.EscapeDataString(email)}");

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hashContrasena", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var existing = await _factory.Accounts.FindByIdAsync(UniHomeApiFactory.TestAdminAccountId);
        var admin = existing ?? await AuthTestHelpers.SeedAccountAsync(
            _factory,
            "admin.cuentas@uni.edu",
            id: UniHomeApiFactory.TestAdminAccountId);

        using var login = await AuthTestHelpers.LoginAsync(_client, admin.Email.Value);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }
}
