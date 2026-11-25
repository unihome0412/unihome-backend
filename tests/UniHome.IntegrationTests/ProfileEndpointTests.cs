using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Domain.Universities;

namespace UniHome.IntegrationTests;

public sealed class ProfileEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public ProfileEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MiPerfil_ConSesionValida_DebeDevolverDatosYNoCamposInternos()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        using var response = await _client.GetAsync("/api/v1/perfil/mi-perfil");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal("Ana López", root.GetProperty("nombreCompleto").GetString());
        Assert.Equal(email, root.GetProperty("correo").GetString());
        Assert.Equal("2004-05-10", root.GetProperty("fechaNacimiento").GetString());
        Assert.Equal("Mujer", root.GetProperty("genero").GetString());
        Assert.Equal("UsuarioUniHome", root.GetProperty("etiquetaCuenta").GetString());
        Assert.False(root.GetProperty("vinculadoAUnidad").GetBoolean());
        Assert.True(root.GetProperty("puedePublicar").GetBoolean());
        Assert.Equal(
            JsonValueKind.Null,
            root.GetProperty("estadoPrimeraPublicacion").ValueKind);
        Assert.True(root.TryGetProperty("universidadActual", out var university));
        Assert.Equal(JsonValueKind.Null, university.ValueKind);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hashContrasena", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("admin", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MiPerfil_SinToken_DebeResponder401()
    {
        using var response = await _client.GetAsync("/api/v1/perfil/mi-perfil");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MiPerfil_Autenticado_DevuelveCacheControlNoStore()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        using var response = await _client.GetAsync("/api/v1/perfil/mi-perfil");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "no-store",
            response.Headers.CacheControl?.ToString() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RespuestaPublica_NoRecibeNoStoreDelPerfil()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        using var response = await _client.GetAsync("/api/v1/universidades?buscar=uni");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(
            "no-store",
            response.Headers.CacheControl?.ToString() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MiPerfil_MenorDeEdad_NoPuedePublicar()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(
            _factory,
            email,
            birthDate: new DateOnly(2010, 1, 1));
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        using var response = await _client.GetAsync("/api/v1/perfil/mi-perfil");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(document.RootElement.GetProperty("puedePublicar").GetBoolean());
    }

    [Fact]
    public async Task CambiarMiUniversidad_ConUniversidadActiva_DebeFijarlaYReflejarseEnElPerfil()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var university = University.Restore(
            Guid.NewGuid(), $"Universidad {Guid.NewGuid():N}", true, DateTimeOffset.UtcNow);
        await _factory.Universities.AddAsync(university);

        using var response = await _client.PutAsJsonAsync(
            "/api/v1/perfil/mi-universidad",
            new { universidadId = university.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var universidadActual = document.RootElement.GetProperty("universidadActual");
        Assert.Equal(university.Id, universidadActual.GetProperty("id").GetGuid());

        using var profile = await _client.GetAsync("/api/v1/perfil/mi-perfil");
        using var profileDocument = JsonDocument.Parse(await profile.Content.ReadAsStringAsync());
        Assert.Equal(
            university.Id,
            profileDocument.RootElement.GetProperty("universidadActual").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task CambiarMiUniversidad_ConUniversidadInexistente_DebeResponder400()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        using var response = await _client.PutAsJsonAsync(
            "/api/v1/perfil/mi-universidad",
            new { universidadId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CambiarMiUniversidad_ConUniversidadInactiva_DebeResponder400()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var university = University.Restore(
            Guid.NewGuid(), $"Universidad {Guid.NewGuid():N}", false, DateTimeOffset.UtcNow);
        await _factory.Universities.AddAsync(university);

        using var response = await _client.PutAsJsonAsync(
            "/api/v1/perfil/mi-universidad",
            new { universidadId = university.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CambiarMiUniversidad_SinSesion_DebeResponder401()
    {
        using var response = await _client.PutAsJsonAsync(
            "/api/v1/perfil/mi-universidad",
            new { universidadId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
