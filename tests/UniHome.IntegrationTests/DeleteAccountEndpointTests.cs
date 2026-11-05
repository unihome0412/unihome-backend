using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Domain.Accounts;

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
