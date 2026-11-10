using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Domain.Accounts;

namespace UniHome.IntegrationTests;

public sealed class OpenApiGenderContractTests : IClassFixture<SwaggerEnabledApiFactory>
{
    private const string Password = "ClaveSegura1";

    private readonly HttpClient _client;

    public OpenApiGenderContractTests(SwaggerEnabledApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApi_DescribeGenderComoStringEnum()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var schema = root
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("Gender");

        Assert.Equal("string", schema.GetProperty("type").GetString());
        Assert.Equal(
            Enum.GetNames<Gender>(),
            schema.GetProperty("enum").EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());

        var genero = root
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("RegistroRequest")
            .GetProperty("properties")
            .GetProperty("genero");

        Assert.Contains(
            genero.GetProperty("oneOf").EnumerateArray(),
            candidate => candidate.TryGetProperty("$ref", out var reference) &&
                         reference.GetString() == "#/components/schemas/Gender");
    }

    [Fact]
    public async Task Registro_AceptaNombreDeGeneroYRechazaEntero()
    {
        var email = $"genero.{Guid.NewGuid():N}@uni.edu";

        using var accepted = await _client.PostAsJsonAsync(
            "/api/v1/auth/registro",
            Payload(email, "Mujer"));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);

        using var rejected = await _client.PostAsJsonAsync(
            "/api/v1/auth/registro",
            Payload($"genero.{Guid.NewGuid():N}@uni.edu", 1));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    private static object Payload(string email, object gender) =>
        new
        {
            nombreCompleto = "Ana López",
            correo = email,
            fechaNacimiento = "2004-05-10",
            genero = gender,
            contrasena = Password,
            confirmarContrasena = Password,
            aceptoAvisoPrivacidad = true,
            versionAvisoPrivacidad = "2026-01"
        };
}
