using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Domain.Accounts;
using UniHome.Domain.OccupancyLinks;
using UniHome.Domain.Properties;
using UniHome.Domain.Units;

namespace UniHome.IntegrationTests;

public sealed class DeleteAccountEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public DeleteAccountEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Eliminar_ConContrasenaYConfirmacion_DebeResponder204YRevocarTodasLasSesiones()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var firstLogin = await AuthTestHelpers.LoginAsync(_client, email);
        var first = await AuthTestHelpers.ReadTokensAsync(firstLogin);
        using var secondLogin = await AuthTestHelpers.LoginAsync(_client, email);
        var second = await AuthTestHelpers.ReadTokensAsync(secondLogin);

        using var response = await DeleteAsync(
            first.AccessToken,
            AuthTestHelpers.Password,
            confirmacionPerdidaInformacion: true);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var account = await _factory.Accounts.FindByEmailAsync(EmailAddress.Create(email));
        Assert.Equal(AccountStatus.Deleted, account!.Status);

        // Todos los dispositivos quedan sin sesión, no solo el actual (a diferencia de logout).
        using var firstRefresh = await RefreshAsync(first.RefreshToken);
        using var secondRefresh = await RefreshAsync(second.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, firstRefresh.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, secondRefresh.StatusCode);
    }

    [Fact]
    public async Task Eliminar_TrasEliminar_NoPermiteLoginNiUsarElAccessTokenAnterior()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);

        using var delete = await DeleteAsync(
            tokens.AccessToken,
            AuthTestHelpers.Password,
            confirmacionPerdidaInformacion: true);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        using var loginAfter = await AuthTestHelpers.LoginAsync(_client, email);
        Assert.Equal(HttpStatusCode.Unauthorized, loginAfter.StatusCode);

        // El access token anterior ya no identifica una cuenta habilitada (BE-SEC-008).
        using var protectedCall = await DeleteAsync(
            tokens.AccessToken,
            AuthTestHelpers.Password,
            confirmacionPerdidaInformacion: true);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedCall.StatusCode);
    }

    [Fact]
    public async Task Eliminar_ConContrasenaIncorrecta_DebeResponder401SinEliminarLaCuenta()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);

        using var response = await DeleteAsync(
            tokens.AccessToken,
            "OtraClave1",
            confirmacionPerdidaInformacion: true);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var account = await _factory.Accounts.FindByEmailAsync(EmailAddress.Create(email));
        Assert.Equal(AccountStatus.Active, account!.Status);
    }

    [Fact]
    public async Task Eliminar_SinConfirmacion_DebeResponder400SinEliminarLaCuenta()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);

        using var response = await DeleteAsync(
            tokens.AccessToken,
            AuthTestHelpers.Password,
            confirmacionPerdidaInformacion: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement
            .GetProperty("errors")
            .TryGetProperty("confirmacionPerdidaInformacion", out _));

        var account = await _factory.Accounts.FindByEmailAsync(EmailAddress.Create(email));
        Assert.Equal(AccountStatus.Active, account!.Status);
    }

    [Fact]
    public async Task Eliminar_SinToken_DebeResponder401()
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/cuentas/mi-cuenta")
        {
            Content = JsonContent.Create(new
            {
                contrasena = AuthTestHelpers.Password,
                confirmacionPerdidaInformacion = true
            })
        };

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Eliminar_NoExponeContrasenaNiTokensEnLaRespuesta()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);

        using var response = await DeleteAsync(
            tokens.AccessToken,
            AuthTestHelpers.Password,
            confirmacionPerdidaInformacion: true);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(AuthTestHelpers.Password, body, StringComparison.Ordinal);
        Assert.DoesNotContain(tokens.RefreshToken, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Eliminar_ConVinculoDeOcupacionActivo_DebeResponder409SinEliminar()
    {
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);

        await _factory.OccupancyLinks.SaveAsync(
            OccupancyLink.CreateActive(account.Id, Guid.NewGuid(), DateTimeOffset.UtcNow));

        using var response = await DeleteAsync(
            tokens.AccessToken,
            AuthTestHelpers.Password,
            confirmacionPerdidaInformacion: true);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var stored = await _factory.Accounts.FindByEmailAsync(EmailAddress.Create(email));
        Assert.Equal(AccountStatus.Active, stored!.Status);
    }

    [Fact]
    public async Task Eliminar_ConPropiedadConInquilinosActivos_DebeResponder409SinEliminar()
    {
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);

        var unit = Unit.Create(
            Guid.NewGuid(),
            account.Id,
            Guid.NewGuid(),
            UnitType.Habitacion,
            Occupancy.Compartida,
            3,
            1500m,
            12m,
            ForWhom.Mixto,
            BathroomType.Compartido,
            FurnitureType.SinMuebles,
            furnitureItems: null,
            template: null,
            DateTimeOffset.UtcNow);
        await _factory.Units.AddAsync(unit);
        await _factory.OccupancyLinks.SaveAsync(
            OccupancyLink.CreateActive(Guid.NewGuid(), unit.Id, DateTimeOffset.UtcNow));

        using var response = await DeleteAsync(
            tokens.AccessToken,
            AuthTestHelpers.Password,
            confirmacionPerdidaInformacion: true);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var stored = await _factory.Accounts.FindByEmailAsync(EmailAddress.Create(email));
        Assert.Equal(AccountStatus.Active, stored!.Status);
    }

    [Fact]
    public async Task Eliminar_ConCuentaAdministrativa_DebeResponder403SinEliminar()
    {
        var existing = await _factory.Accounts.FindByIdAsync(UniHomeApiFactory.TestAdminAccountId);
        var admin = existing ?? await AuthTestHelpers.SeedAccountAsync(
            _factory,
            "admin.autoeliminacion@uni.edu",
            id: UniHomeApiFactory.TestAdminAccountId);

        using var login = await AuthTestHelpers.LoginAsync(_client, admin.Email.Value);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);

        using var response = await DeleteAsync(
            tokens.AccessToken,
            AuthTestHelpers.Password,
            confirmacionPerdidaInformacion: true);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var stored = await _factory.Accounts.FindByIdAsync(UniHomeApiFactory.TestAdminAccountId);
        Assert.Equal(AccountStatus.Active, stored!.Status);
    }

    private Task<HttpResponseMessage> DeleteAsync(
        string accessToken,
        string password,
        bool confirmacionPerdidaInformacion)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/cuentas/mi-cuenta")
        {
            Content = JsonContent.Create(new
            {
                contrasena = password,
                confirmacionPerdidaInformacion
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken });
}
