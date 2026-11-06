using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Domain.Accounts;

namespace UniHome.IntegrationTests;

public sealed class RefreshEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public RefreshEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Refresh_ConTokenValido_DebeRotarYEntregarNuevosTokens()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var original = await AuthTestHelpers.ReadTokensAsync(login);

        using var response = await RefreshAsync(original.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        var renewed = await AuthTestHelpers.ReadTokensAsync(response);

        Assert.NotEqual(original.AccessToken, renewed.AccessToken);
        Assert.NotEqual(original.RefreshToken, renewed.RefreshToken);
        Assert.True(renewed.ExpiraEn > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Refresh_ConTokenReutilizado_DebeRevocarLaFamilia()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var original = await AuthTestHelpers.ReadTokensAsync(login);

        using var firstRefresh = await RefreshAsync(original.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);
        var renewed = await AuthTestHelpers.ReadTokensAsync(firstRefresh);

        using var reused = await RefreshAsync(original.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);

        // La familia completa quedó revocada: el token recién emitido tampoco sirve.
        using var afterRevocation = await RefreshAsync(renewed.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevocation.StatusCode);
    }

    [Fact]
    public async Task Refresh_ConSesionVencida_DebeResponder401()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var original = await AuthTestHelpers.ReadTokensAsync(login);
        var sessionId = AuthTestHelpers.GetSessionId(original.RefreshToken);

        _factory.Sessions.SetExpiresAt(sessionId, DateTimeOffset.UtcNow.AddMinutes(-1));

        using var response = await RefreshAsync(original.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoTokensInBodyAsync(response);
    }

    [Fact]
    public async Task Refresh_ConCuentaSuspendida_DebeRechazarYRevocarSesion()
    {
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);

        await _factory.Accounts.UpdateAsync(
            TestSupport.TestAccountFactory.WithStatus(account, AccountStatus.Suspended));

        using var response = await RefreshAsync(tokens.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var session = await _factory.Sessions.FindByIdAsync(
            AuthTestHelpers.GetSessionId(tokens.RefreshToken));
        Assert.NotNull(session);
        Assert.NotNull(session.RevokedAt);
    }

    [Fact]
    public async Task Refresh_ConCuentaEliminada_DebeRechazar()
    {
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);

        await _factory.Accounts.UpdateAsync(
            TestSupport.TestAccountFactory.WithStatus(account, AccountStatus.Deleted));

        using var response = await RefreshAsync(tokens.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ConTokenMalformado_DebeResponder401()
    {
        using var response = await RefreshAsync("esto-no-es-un-refresh-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoTokensInBodyAsync(response);
    }

    [Fact]
    public async Task Refresh_SinToken_DebeResponder400()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_Concurrente_ConElMismoToken_SoloUnaSolicitudDebeGanar()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var original = await AuthTestHelpers.ReadTokensAsync(login);

        var first = RefreshAsync(original.RefreshToken);
        var second = RefreshAsync(original.RefreshToken);

        using var firstResponse = await first;
        using var secondResponse = await second;

        var statuses = new[] { firstResponse.StatusCode, secondResponse.StatusCode };
        Assert.Contains(HttpStatusCode.OK, statuses);
        Assert.Contains(HttpStatusCode.Unauthorized, statuses);

        var winner = firstResponse.StatusCode == HttpStatusCode.OK
            ? firstResponse
            : secondResponse;
        var winnerTokens = await AuthTestHelpers.ReadTokensAsync(winner);

        // El perdedor detectó reuso y revocó la familia, incluido el token ganador.
        using var afterRace = await RefreshAsync(winnerTokens.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterRace.StatusCode);
    }

    [Fact]
    public async Task Refresh_NoDebeRegistrarTokensEnLogs()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var original = await AuthTestHelpers.ReadTokensAsync(login);
        _factory.Logs.Clear();

        using var response = await RefreshAsync(original.RefreshToken);
        var renewed = await AuthTestHelpers.ReadTokensAsync(response);

        var logs = string.Join('\n', _factory.Logs.Messages);

        Assert.DoesNotContain(original.AccessToken, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(original.RefreshToken, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(renewed.AccessToken, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(renewed.RefreshToken, logs, StringComparison.Ordinal);
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken });

    private static async Task AssertNoTokensInBodyAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("accessToken", body, StringComparison.Ordinal);
        Assert.DoesNotContain("refreshToken", body, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(401, document.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("traceId").GetString()));
    }
}
