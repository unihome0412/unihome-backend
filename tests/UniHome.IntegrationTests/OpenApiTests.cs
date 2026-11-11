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
}
