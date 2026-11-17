using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// Flujo de navegador: formulario público, CSRF, login con credenciales,
/// permiso documental y cookie HttpOnly. Nunca se recibe un JWT.
/// </summary>
public sealed class SwaggerBrowserLoginTests : IClassFixture<SwaggerEnabledApiFactory>
{
    private static readonly Regex HiddenToken = new(
        "name=\"([^\"]+)\" value=\"([^\"]+)\"",
        RegexOptions.CultureInvariant);

    private readonly SwaggerEnabledApiFactory _factory;

    public SwaggerBrowserLoginTests(SwaggerEnabledApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LoginConCredencialesCorrectas_EmiteCookieSinJwt()
    {
        using var client = CreateHttpsClient();
        var token = await GetLoginTokenAsync(client);

        using var login = await PostLoginAsync(
            client,
            token,
            _factory.AdminAccount.Email.Value,
            TestAccountFactory.DefaultPassword);

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/swagger", login.Headers.Location!.OriginalString);
        var body = await login.Content.ReadAsStringAsync();
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);

        var setCookies = login.Headers.GetValues("Set-Cookie").ToArray();
        var swaggerCookies = setCookies
            .Where(value => value.StartsWith(
                "__Host-unihome-swagger=",
                StringComparison.Ordinal))
            .ToArray();
        var swaggerCookie = Assert.Single(swaggerCookies);
        Assert.Contains("httponly", swaggerCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", swaggerCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", swaggerCookie, StringComparison.OrdinalIgnoreCase);

        var ticket = swaggerCookie
            .Split(';', 2, StringSplitOptions.TrimEntries)[0]
            .Split('=', 2)[1];
        Assert.Equal(2, ticket.Split('.').Length);
        Assert.DoesNotContain("accessToken", swaggerCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", swaggerCookie, StringComparison.OrdinalIgnoreCase);

        using var ui = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);

        using var openApi = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);

        // La cookie documental no autentica la API normal.
        using var api = await client.GetAsync("/api/v1/perfil/mi-perfil");
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
    }

    [Fact]
    public async Task LectorDocumental_EntraYNoPuedeUsarRutasAdministrativas()
    {
        using var client = CreateHttpsClient();
        var token = await GetLoginTokenAsync(client);

        using var login = await PostLoginAsync(
            client,
            token,
            _factory.DocumentationReaderAccount.Email.Value,
            TestAccountFactory.DefaultPassword);

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        using var openApi = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);

        // La cookie documental no autentica /api/*; el lector no obtiene
        // privilegios administrativos por haber entrado a Swagger.
        using var admin = await client.GetAsync("/api/v1/admin/universidades");
        Assert.Equal(HttpStatusCode.Unauthorized, admin.StatusCode);
    }

    [Fact]
    public async Task UsuarioSinPermisoDocumental_RecibeErrorGenericoYSinCookie()
    {
        var account = TestSupport.TestAccountFactory.Create(
            $"swagger.normal.{Guid.NewGuid():N}@uni.edu");
        await _factory.Accounts.AddAsync(account);

        using var client = CreateHttpsClient();
        var token = await GetLoginTokenAsync(client);

        using var login = await PostLoginAsync(
            client,
            token,
            account.Email.Value,
            TestAccountFactory.DefaultPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        var body = await login.Content.ReadAsStringAsync();
        Assert.Contains("No se pudo iniciar sesi", body, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "__Host-unihome-swagger=",
            string.Join(
                "\n",
                login.Headers.TryGetValues("Set-Cookie", out var values) ? values : []),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContrasenaIncorrecta_NoEmiteCookieYNoEnumera()
    {
        using var client = CreateHttpsClient();
        var token = await GetLoginTokenAsync(client);

        using var login = await PostLoginAsync(
            client,
            token,
            _factory.AdminAccount.Email.Value,
            "ContraseñaIncorrecta1");

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        var body = await login.Content.ReadAsStringAsync();
        Assert.Contains("No se pudo iniciar sesi", body, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "__Host-unihome-swagger=",
            string.Join(
                "\n",
                login.Headers.TryGetValues("Set-Cookie", out var values) ? values : []),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginSinTokenCsrf_EsRechazado()
    {
        using var client = CreateHttpsClient();

        using var response = await client.PostAsync(
            "/swagger/login",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>(
                    "correo",
                    _factory.AdminAccount.Email.Value),
                new KeyValuePair<string, string>(
                    "contrasena",
                    TestAccountFactory.DefaultPassword)
            ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values
            : [];
        Assert.DoesNotContain(
            setCookies,
            value => value.StartsWith(
                "__Host-unihome-swagger=",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task LogoutConCsrf_EliminaLaCookieYRedirigeAlLogin()
    {
        using var client = CreateHttpsClient();
        var loginToken = await GetLoginTokenAsync(client);
        using var login = await PostLoginAsync(
            client,
            loginToken,
            _factory.AdminAccount.Email.Value,
            TestAccountFactory.DefaultPassword);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        using var logoutPage = await client.GetAsync("/swagger/logout");
        Assert.Equal(HttpStatusCode.OK, logoutPage.StatusCode);
        var logoutToken = ExtractToken(await logoutPage.Content.ReadAsStringAsync());

        using var logout = await client.PostAsync(
            "/swagger/logout",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>(
                    "__RequestVerificationToken",
                    logoutToken)
            ]));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/swagger/login?logout=1", logout.Headers.Location!.OriginalString);

        using var after = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.Equal("/swagger/login", after.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task LogoutSinCsrf_EsRechazado()
    {
        using var client = CreateHttpsClient();
        var loginToken = await GetLoginTokenAsync(client);
        using var login = await PostLoginAsync(
            client,
            loginToken,
            _factory.AdminAccount.Email.Value,
            TestAccountFactory.DefaultPassword);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        using var response = await client.PostAsync(
            "/swagger/logout",
            new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var stillAuthorized = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, stillAuthorized.StatusCode);
    }

    [Fact]
    public async Task CookieExpirada_EsRechazada()
    {
        using var client = CreateHttpsClient();
        var expired = CreateTicket(
            _factory.AdminAccount.Id,
            DateTimeOffset.UtcNow.AddMinutes(-1));
        client.DefaultRequestHeaders.Add(
            "Cookie",
            $"__Host-unihome-swagger={expired}");

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<string> GetLoginTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/swagger/login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ExtractToken(await response.Content.ReadAsStringAsync());
    }

    private static string ExtractToken(string html)
    {
        var match = HiddenToken.Match(html);
        Assert.True(match.Success, "No se encontró el token CSRF en el formulario.");
        Assert.Equal("__RequestVerificationToken", match.Groups[1].Value);
        return match.Groups[2].Value;
    }

    private static Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string token,
        string email,
        string password) =>
        client.PostAsync(
            "/swagger/login",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", token),
                new KeyValuePair<string, string>("correo", email),
                new KeyValuePair<string, string>("contrasena", password)
            ]));

    private static string CreateTicket(Guid accountId, DateTimeOffset expiresAt)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            sub = accountId.ToString("N"),
            iat = now - 60,
            exp = expiresAt.ToUnixTimeSeconds(),
            nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(8))
        });
        var encodedPayload = Base64UrlEncode(payload);
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(
            "UniHome.SwaggerAccess.v1:" + UniHomeApiFactory.TestSigningKey));
        using var hmac = new HMACSHA256(key);
        var signature = hmac.ComputeHash(Encoding.ASCII.GetBytes(encodedPayload));
        return $"{encodedPayload}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private HttpClient CreateHttpsClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });
}
