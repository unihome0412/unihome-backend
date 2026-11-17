using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using UniHome.Domain.Accounts;
using UniHome.IntegrationTests.TestSupport;

namespace UniHome.IntegrationTests;

/// <summary>
/// La documentación interna exige el mismo privilegio administrativo que
/// /admin. El Bearer se usa para descargar el contrato y puede canjearse por una
/// cookie HttpOnly para que el navegador cargue Swagger UI; la cookie no sirve
/// para operar la API.
/// </summary>
public sealed class SwaggerAccessTests : IClassFixture<SwaggerEnabledApiFactory>
{
    private readonly SwaggerEnabledApiFactory _factory;

    public SwaggerAccessTests(SwaggerEnabledApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SinAutenticacion_SwaggerYContratoResponden401()
    {
        using var client = _factory.CreateClient();

        foreach (var path in new[] { "/", "/swagger", "/swagger/index.html", "/swagger/swagger-ui.css", "/openapi/v1.json" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task UsuarioAutenticadoSinPrivilegio_Recibe403()
    {
        var account = await SeedNonAdminAccountAsync();
        using var client = CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization = Bearer(account.Id);

        foreach (var path in new[] { "/swagger", "/openapi/v1.json" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task AdministradorConBearer_DescargaContratoYRecibeCookieEndurecida()
    {
        using var client = _factory.CreateAdminClient(CreateHttpsClientOptions());

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("__Host-unihome-swagger=", setCookie, StringComparison.Ordinal);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdministradorConBearer_PuedeAbrirSwaggerUi()
    {
        using var client = _factory.CreateAdminClient(CreateHttpsClientOptions());

        using var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("style-src 'self' 'unsafe-inline'", csp, StringComparison.Ordinal);
        Assert.Contains("script-src 'self'", csp, StringComparison.Ordinal);
        Assert.Equal("noindex, nofollow", response.Headers.GetValues("X-Robots-Tag").Single());
    }

    [Fact]
    public async Task SesionSwagger_CanjeaBearerPorCookieYPermiteUiYContrato()
    {
        using var client = _factory.CreateAdminClient(CreateHttpsClientOptions());

        using var created = await client.PostAsync("/api/v1/auth/swagger-session", content: null);
        Assert.Equal(HttpStatusCode.NoContent, created.StatusCode);

        // A partir de aquí solo queda la cookie; el Bearer se retira para
        // demostrar que la API no acepta la sesión de documentación.
        client.DefaultRequestHeaders.Authorization = null;

        using var ui = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);

        using var openApi = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);

        using var api = await client.GetAsync("/api/v1/perfil/mi-perfil");
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
    }

    [Fact]
    public async Task SesionSwagger_SinBearerEs401YConUsuarioNoAdminEs403()
    {
        using var anonymous = CreateHttpsClient();
        using var anonymousResponse = await anonymous.PostAsync(
            "/api/v1/auth/swagger-session",
            content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var account = await SeedNonAdminAccountAsync();
        using var client = CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization = Bearer(account.Id);
        using var response = await client.PostAsync(
            "/api/v1/auth/swagger-session",
            content: null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CookieManipulada_EsRechazada()
    {
        using var client = CreateHttpsClient();
        client.DefaultRequestHeaders.Add(
            "Cookie",
            "__Host-unihome-swagger=payload.firma-invalida");

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CookieDeCuentaDeshabilitada_EsRechazada()
    {
        using var client = _factory.CreateAdminClient(CreateHttpsClientOptions());
        var account = _factory.AdminAccount;

        using var created = await client.PostAsync("/api/v1/auth/swagger-session", content: null);
        Assert.Equal(HttpStatusCode.NoContent, created.StatusCode);

        await _factory.Accounts.UpdateAsync(
            TestAccountFactory.WithStatus(account, AccountStatus.Suspended));

        try
        {
            using var response = await client.GetAsync("/swagger/index.html");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await _factory.Accounts.UpdateAsync(
                TestAccountFactory.WithStatus(account, AccountStatus.Active));
        }
    }

    [Fact]
    public async Task EliminarSesionSwagger_InvalidaLaCookie()
    {
        using var client = _factory.CreateAdminClient(CreateHttpsClientOptions());

        using var created = await client.PostAsync("/api/v1/auth/swagger-session", content: null);
        Assert.Equal(HttpStatusCode.NoContent, created.StatusCode);

        using var deleted = await client.DeleteAsync("/api/v1/auth/swagger-session");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;

        using var response = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CookieNoSeAceptaEnRutasDeApi()
    {
        using var client = _factory.CreateAdminClient(CreateHttpsClientOptions());

        using var created = await client.PostAsync("/api/v1/auth/swagger-session", content: null);
        Assert.Equal(HttpStatusCode.NoContent, created.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;

        using var response = await client.GetAsync("/api/v1/favoritos");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<Account> SeedNonAdminAccountAsync()
    {
        var account = TestAccountFactory.Create(
            $"swagger.user.{Guid.NewGuid():N}@uni.edu");
        await _factory.Accounts.AddAsync(account);
        return account;
    }

    private static AuthenticationHeaderValue Bearer(Guid accountId) =>
        new("Bearer", JwtTestTokenFactory.Create(accountId, Guid.NewGuid()));

    private HttpClient CreateHttpsClient() =>
        _factory.CreateClient(CreateHttpsClientOptions());

    private static WebApplicationFactoryClientOptions CreateHttpsClientOptions() =>
        new()
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        };
}
