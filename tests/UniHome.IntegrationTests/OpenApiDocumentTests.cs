using System.Net;
using System.Text.Json;

namespace UniHome.IntegrationTests;

public sealed class OpenApiDocumentTests : IClassFixture<UniHomeApiFactory>
{
    private readonly HttpClient _client;

    public OpenApiDocumentTests(UniHomeApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApi_DocumentaBearerYProtegeLogout()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        var bearer = root
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");

        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());

        var logout = root
            .GetProperty("paths")
            .GetProperty("/api/v1/auth/logout")
            .GetProperty("post");

        Assert.True(logout.TryGetProperty("security", out var security));
        Assert.Contains(
            security.EnumerateArray(),
            requirement => requirement.TryGetProperty("Bearer", out _));

        var login = root
            .GetProperty("paths")
            .GetProperty("/api/v1/auth/login")
            .GetProperty("post");

        Assert.False(login.TryGetProperty("security", out _));

        var deleteAccount = root
            .GetProperty("paths")
            .GetProperty("/api/v1/cuentas/mi-cuenta")
            .GetProperty("delete");

        Assert.True(deleteAccount.TryGetProperty("security", out var deleteSecurity));
        Assert.Contains(
            deleteSecurity.EnumerateArray(),
            requirement => requirement.TryGetProperty("Bearer", out _));

        var confirmEmail = root
            .GetProperty("paths")
            .GetProperty("/api/v1/auth/confirmacion-correo")
            .GetProperty("post");

        Assert.False(confirmEmail.TryGetProperty("security", out _));

        var resend = root
            .GetProperty("paths")
            .GetProperty("/api/v1/auth/confirmacion-correo/reenvio")
            .GetProperty("post");

        Assert.False(resend.TryGetProperty("security", out _));
    }
}
