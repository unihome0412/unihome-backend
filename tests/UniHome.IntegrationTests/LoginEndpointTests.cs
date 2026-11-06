using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using UniHome.Domain.Accounts;

namespace UniHome.IntegrationTests;

public sealed class LoginEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public LoginEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_ConCuentaConfirmada_DebeEmitirTokensSinRol()
    {
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);
        var sessionsBefore = _factory.Sessions.Count;

        using var response = await AuthTestHelpers.LoginAsync(_client, email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        var tokens = await AuthTestHelpers.ReadTokensAsync(response);

        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.InRange(
            tokens.ExpiraEn,
            DateTimeOffset.UtcNow.AddMinutes(58),
            DateTimeOffset.UtcNow.AddMinutes(61));

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(tokens.AccessToken);
        Assert.Equal(account.Id.ToString("N"), jwt.Subject);
        Assert.Contains(jwt.Claims, claim => claim.Type == "sid");
        Assert.DoesNotContain(
            jwt.Claims,
            claim => claim.Type is "role" or "roles");

        Assert.Equal(sessionsBefore + 1, _factory.Sessions.Count);

        var session = await _factory.Sessions.FindByIdAsync(
            AuthTestHelpers.GetSessionId(tokens.RefreshToken));

        Assert.NotNull(session);
        Assert.Empty(session.UsedTokens);
    }

    [Fact]
    public async Task Login_ConContrasenaIncorrecta_DebeResponder401Generico()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var response = await AuthTestHelpers.LoginAsync(_client, email, "OtraClave1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertGenericLoginFailureAsync(response);
    }

    [Fact]
    public async Task Login_ConCorreoInexistente_DebeResponderIgualQueContrasenaIncorrecta()
    {
        var existingEmail = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, existingEmail);

        using var wrongPassword = await AuthTestHelpers.LoginAsync(
            _client,
            existingEmail,
            "OtraClave1");
        using var unknownEmail = await AuthTestHelpers.LoginAsync(
            _client,
            AuthTestHelpers.UniqueEmail(),
            "OtraClave1");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);

        using var wrongDocument = JsonDocument.Parse(
            await wrongPassword.Content.ReadAsStringAsync());
        using var unknownDocument = JsonDocument.Parse(
            await unknownEmail.Content.ReadAsStringAsync());

        Assert.Equal(
            wrongDocument.RootElement.GetProperty("title").GetString(),
            unknownDocument.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            wrongDocument.RootElement.GetProperty("detail").GetString(),
            unknownDocument.RootElement.GetProperty("detail").GetString());
        Assert.Equal(
            "Revisa tu correo y contraseña",
            unknownDocument.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_ConCorreoNoConfirmado_DebeResponder403SinTokens()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email, emailConfirmed: false);

        using var response = await AuthTestHelpers.LoginAsync(_client, email);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertNoTokensAsync(response);
    }

    [Fact]
    public async Task Login_ConCuentaSuspendida_DebeResponder403SinTokens()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email, AccountStatus.Suspended);

        using var response = await AuthTestHelpers.LoginAsync(_client, email);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertNoTokensAsync(response);
    }

    [Fact]
    public async Task Login_ConCuentaEliminada_DebeResponder401Generico()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email, AccountStatus.Deleted);

        using var response = await AuthTestHelpers.LoginAsync(_client, email);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertGenericLoginFailureAsync(response);
    }

    [Fact]
    public async Task Login_DebeNormalizarElCorreo()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var response = await AuthTestHelpers.LoginAsync(
            _client,
            $"  {email.ToUpperInvariant()}  ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_ConRolEnviadoPorCliente_DebeIgnorarlo()
    {
        var email = AuthTestHelpers.UniqueEmail();
        var account = await AuthTestHelpers.SeedAccountAsync(_factory, email);

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                correo = email,
                contrasena = AuthTestHelpers.Password,
                rol = "Administrador",
                roles = new[] { "Administrador" }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tokens = await AuthTestHelpers.ReadTokensAsync(response);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(tokens.AccessToken);

        Assert.Equal(account.Id.ToString("N"), jwt.Subject);
        Assert.DoesNotContain(
            jwt.Claims,
            claim => claim.Type is "role" or "roles");
    }

    [Fact]
    public async Task Login_ConDatosFaltantes_DebeResponder400SinConsultarCuenta()
    {
        var sessionsBefore = _factory.Sessions.Count;

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { correo = "", contrasena = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(sessionsBefore, _factory.Sessions.Count);
    }

    [Fact]
    public async Task Login_NoDebeIncluirContrasenaNiTokensEnLogs()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        _factory.Logs.Clear();

        using var response = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(response);

        var logs = string.Join('\n', _factory.Logs.Messages);

        Assert.DoesNotContain(AuthTestHelpers.Password, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(tokens.AccessToken, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(tokens.RefreshToken, logs, StringComparison.Ordinal);
    }

    private static async Task AssertGenericLoginFailureAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(401, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            "Revisa tu correo y contraseña",
            document.RootElement.GetProperty("detail").GetString());

        Assert.DoesNotContain(AuthTestHelpers.Password, body, StringComparison.Ordinal);
        Assert.DoesNotContain("accessToken", body, StringComparison.Ordinal);
        Assert.DoesNotContain("refreshToken", body, StringComparison.Ordinal);
    }

    private static async Task AssertNoTokensAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("accessToken", body, StringComparison.Ordinal);
        Assert.DoesNotContain("refreshToken", body, StringComparison.Ordinal);
        Assert.DoesNotContain(AuthTestHelpers.Password, body, StringComparison.Ordinal);
    }
}
