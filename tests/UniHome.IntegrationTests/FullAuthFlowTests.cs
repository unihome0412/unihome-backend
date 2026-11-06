using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace UniHome.IntegrationTests;

/// <summary>
/// Flujo completo exigido para M01: registro → confirmación → login → refresh →
/// logout → login → eliminar cuenta → login rechazado. Sin Mongo real y sin
/// envío de correo real (ambos sustituidos por dobles en <see cref="UniHomeApiFactory"/>).
/// </summary>
public sealed class FullAuthFlowTests : IClassFixture<UniHomeApiFactory>
{
    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public FullAuthFlowTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FlujoCompleto_RegistroConfirmacionLoginRefreshLogoutLoginEliminarLoginRechazado()
    {
        var email = AuthTestHelpers.UniqueEmail();

        // 1) Registro.
        using var register = await _client.PostAsJsonAsync(
            "/api/v1/auth/registro",
            new
            {
                nombreCompleto = "Ana López",
                correo = email,
                fechaNacimiento = "2004-05-10",
                genero = "Mujer",
                contrasena = AuthTestHelpers.Password,
                confirmarContrasena = AuthTestHelpers.Password,
                aceptoAvisoPrivacidad = true,
                versionAvisoPrivacidad = "2026-01"
            });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        // Antes de confirmar, el login se rechaza (HU-004 exige correo confirmado).
        using var loginBeforeConfirm = await AuthTestHelpers.LoginAsync(_client, email);
        Assert.Equal(HttpStatusCode.Forbidden, loginBeforeConfirm.StatusCode);

        // 2) Confirmación con el código capturado por el doble de correo.
        var code = _factory.EmailSender.Messages.Last(message => message.Email == email).Code;
        using var confirm = await _client.PostAsJsonAsync(
            "/api/v1/auth/confirmacion-correo",
            new { correo = email, codigo = code });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        // 3) Login.
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);

        // 4) Refresh con rotación.
        using var refresh = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = await AuthTestHelpers.ReadTokensAsync(refresh);

        // 5) Logout con el access token vigente (tras el refresh).
        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        using var logout = await _client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // El refresh token de esa sesión ya no sirve tras el logout.
        using var refreshAfterLogout = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = refreshed.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);

        // 6) Login de nuevo (nueva sesión, independiente de la cerrada).
        using var secondLogin = await AuthTestHelpers.LoginAsync(_client, email);
        Assert.Equal(HttpStatusCode.OK, secondLogin.StatusCode);
        var secondTokens = await AuthTestHelpers.ReadTokensAsync(secondLogin);

        // 7) Eliminar cuenta con esta segunda sesión.
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/cuentas/mi-cuenta")
        {
            Content = JsonContent.Create(new
            {
                contrasena = AuthTestHelpers.Password,
                confirmacionPerdidaInformacion = true
            })
        };
        deleteRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", secondTokens.AccessToken);
        using var delete = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // 8) Login rechazado tras eliminar, con el mismo mensaje genérico que
        // credenciales inválidas (anti-enumeración; HU-004/HU-096).
        using var loginAfterDelete = await AuthTestHelpers.LoginAsync(_client, email);
        Assert.Equal(HttpStatusCode.Unauthorized, loginAfterDelete.StatusCode);

        // La segunda sesión tampoco puede renovarse ni usar su access token.
        using var refreshAfterDelete = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = secondTokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterDelete.StatusCode);
    }
}
