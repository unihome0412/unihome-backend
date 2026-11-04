using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace UniHome.IntegrationTests;

public sealed class RegisterWithoutStorageTests : IClassFixture<UnconfiguredIdentityApiFactory>
{
    private readonly HttpClient _client;

    public RegisterWithoutStorageTests(UnconfiguredIdentityApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Registro_SinMongoConfigurado_DebeResponder503SinDetallesInternos()
    {
        var payload = new
        {
            nombreCompleto = "Ana López",
            correo = $"ana.{Guid.NewGuid():N}@uni.edu",
            fechaNacimiento = "2004-05-10",
            genero = "Mujer",
            contrasena = "ClaveSegura1",
            confirmarContrasena = "ClaveSegura1",
            aceptoAvisoPrivacidad = true,
            versionAvisoPrivacidad = "2026-01"
        };

        using var response = await _client.PostAsJsonAsync("/api/v1/auth/registro", payload);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(503, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("mongodb://", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mongodb+srv://", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }
}
