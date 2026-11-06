using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace UniHome.IntegrationTests;

public sealed class LogoutEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public LogoutEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Logout_ConSesionValida_DebeInvalidarSoloElRefreshActual()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var firstLogin = await AuthTestHelpers.LoginAsync(_client, email);
        var first = await AuthTestHelpers.ReadTokensAsync(firstLogin);

        using var secondLogin = await AuthTestHelpers.LoginAsync(_client, email);
        var second = await AuthTestHelpers.ReadTokensAsync(secondLogin);

        using var logout = await LogoutAsync(first.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        using var firstRefresh = await RefreshAsync(first.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, firstRefresh.StatusCode);

        using var secondRefresh = await RefreshAsync(second.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, secondRefresh.StatusCode);
    }

    [Fact]
    public async Task Logout_Repetido_ConElMismoAccessToken_DebeResponder401()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);

        using var first = await LogoutAsync(tokens.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        using var second = await LogoutAsync(tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task Logout_SinToken_DebeResponder401ConProblemDetails()
    {
        using var response = await _client.PostAsync(
            "/api/v1/auth/logout",
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.WwwAuthenticate.Count > 0);
    }

    [Fact]
    public async Task Logout_ConTokenInvalido_DebeResponder401()
    {
        using var response = await LogoutAsync("token-invalido");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ConAccessTokenDeOtraCuenta_NoDebeCerrarLaSesion()
    {
        var firstEmail = AuthTestHelpers.UniqueEmail();
        var secondEmail = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, firstEmail);
        await AuthTestHelpers.SeedAccountAsync(_factory, secondEmail);

        using var firstLogin = await AuthTestHelpers.LoginAsync(_client, firstEmail);
        var first = await AuthTestHelpers.ReadTokensAsync(firstLogin);
        using var secondLogin = await AuthTestHelpers.LoginAsync(_client, secondEmail);
        var second = await AuthTestHelpers.ReadTokensAsync(secondLogin);

        // El access token identifica al titular y la sesión; no se puede usar el
        // token de una cuenta para revocar la sesión de otra.
        using var logout = await LogoutAsync(first.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        using var secondRefresh = await RefreshAsync(second.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, secondRefresh.StatusCode);
    }

    private Task<HttpResponseMessage> LogoutAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken });
}
