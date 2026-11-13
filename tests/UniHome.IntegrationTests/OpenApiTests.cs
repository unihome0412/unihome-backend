using System.Net;
using System.Text.Json;

namespace UniHome.IntegrationTests;

public sealed class OpenApiTests : IClassFixture<SwaggerEnabledApiFactory>
{
    private readonly HttpClient _client;

    public OpenApiTests(SwaggerEnabledApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApi_DebeEstarDisponibleConSwaggerActivado()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("openapi", out _));
        Assert.True(root.TryGetProperty("paths", out _));
    }

    [Fact]
    public async Task OpenApi_DebeIncluirElRegistroDeM01()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/auth/registro", out var registro));
        Assert.True(registro.TryGetProperty("post", out _));
    }

    [Fact]
    public async Task OpenApi_DebeIncluirLosEndpointsNuevosDeM01()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/auth/confirmacion-correo", out var confirm));
        Assert.True(confirm.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty("/api/v1/auth/confirmacion-correo/reenvio", out var resend));
        Assert.True(resend.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty("/api/v1/cuentas/mi-cuenta", out var deleteAccount));
        Assert.True(deleteAccount.TryGetProperty("delete", out _));

        Assert.True(paths.TryGetProperty("/api/v1/auth/recuperacion/solicitud", out var resetRequest));
        Assert.True(resetRequest.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty("/api/v1/auth/recuperacion/confirmacion", out var resetConfirm));
        Assert.True(resetConfirm.TryGetProperty("post", out _));
    }

    [Fact]
    public async Task OpenApi_DebeIncluirLosEndpointsDeUniversidadesDeM03()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/admin/universidades", out var adminUniversities));
        Assert.True(adminUniversities.TryGetProperty("post", out _));
        Assert.True(adminUniversities.TryGetProperty("get", out _));

        Assert.True(paths.TryGetProperty("/api/v1/universidades", out var search));
        Assert.True(search.TryGetProperty("get", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/admin/universidades/{id}",
            out var editUniversity));
        Assert.True(editUniversity.TryGetProperty("put", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/admin/universidades/{id}/estado",
            out var changeUniversityStatus));
        Assert.True(changeUniversityStatus.TryGetProperty("put", out _));
    }

    [Fact]
    public async Task OpenApi_DebeIncluirLosEndpointsDeM05()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/propiedades", out var properties));
        Assert.True(properties.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/propiedades/{propertyId}/unidades",
            out var units));
        Assert.True(units.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/unidades/{unitId}/fotos",
            out var photos));
        Assert.True(photos.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/unidades/{unitId}/servicios",
            out var services));
        Assert.True(services.TryGetProperty("put", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/unidades/{unitId}/zona-reglas",
            out var zone));
        Assert.True(zone.TryGetProperty("put", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/unidades/{unitId}/vista-previa",
            out var preview));
        Assert.True(preview.TryGetProperty("get", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/publicaciones/primera/terminos",
            out var terms));
        Assert.True(terms.TryGetProperty("post", out _));

        Assert.True(paths.TryGetProperty(
            "/api/v1/fotos/{photoRef}",
            out var photo));
        Assert.True(photo.TryGetProperty("get", out _));
    }

    [Fact]
    public async Task OpenApi_DebeIncluirElPerfilDeM02()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/perfil/mi-perfil", out var profile));
        Assert.True(profile.TryGetProperty("get", out _));
    }
}
