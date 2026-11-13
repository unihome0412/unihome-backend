using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

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
}
