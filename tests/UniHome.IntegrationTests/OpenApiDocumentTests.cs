using System.Net;
using System.Text.Json;

namespace UniHome.IntegrationTests;

public sealed class OpenApiDocumentTests : IClassFixture<SwaggerEnabledApiFactory>
{
    private readonly HttpClient _client;

    public OpenApiDocumentTests(SwaggerEnabledApiFactory factory)
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

        var resetRequest = root
            .GetProperty("paths")
            .GetProperty("/api/v1/auth/recuperacion/solicitud")
            .GetProperty("post");

        Assert.False(resetRequest.TryGetProperty("security", out _));

        var resetConfirm = root
            .GetProperty("paths")
            .GetProperty("/api/v1/auth/recuperacion/confirmacion")
            .GetProperty("post");

        Assert.False(resetConfirm.TryGetProperty("security", out _));

        var createUniversity = root
            .GetProperty("paths")
            .GetProperty("/api/v1/admin/universidades")
            .GetProperty("post");

        Assert.True(createUniversity.TryGetProperty("security", out var createSecurity));
        Assert.Contains(
            createSecurity.EnumerateArray(),
            requirement => requirement.TryGetProperty("Bearer", out _));

        var searchUniversities = root
            .GetProperty("paths")
            .GetProperty("/api/v1/universidades")
            .GetProperty("get");

        Assert.False(searchUniversities.TryGetProperty("security", out _));

        var listUniversities = root
            .GetProperty("paths")
            .GetProperty("/api/v1/admin/universidades")
            .GetProperty("get");

        Assert.True(listUniversities.TryGetProperty("security", out var listSecurity));
        Assert.Contains(
            listSecurity.EnumerateArray(),
            requirement => requirement.TryGetProperty("Bearer", out _));

        var editUniversity = root
            .GetProperty("paths")
            .GetProperty("/api/v1/admin/universidades/{id}")
            .GetProperty("put");

        Assert.True(editUniversity.TryGetProperty("security", out var editSecurity));
        Assert.Contains(
            editSecurity.EnumerateArray(),
            requirement => requirement.TryGetProperty("Bearer", out _));

        var changeStatus = root
            .GetProperty("paths")
            .GetProperty("/api/v1/admin/universidades/{id}/estado")
            .GetProperty("put");

        Assert.True(changeStatus.TryGetProperty("security", out var statusSecurity));
        Assert.Contains(
            statusSecurity.EnumerateArray(),
            requirement => requirement.TryGetProperty("Bearer", out _));

        var profile = root
            .GetProperty("paths")
            .GetProperty("/api/v1/perfil/mi-perfil")
            .GetProperty("get");

        Assert.True(profile.TryGetProperty("security", out var profileSecurity));
        Assert.Contains(
            profileSecurity.EnumerateArray(),
            requirement => requirement.TryGetProperty("Bearer", out _));
    }
}
