using System.Net;
using System.Text.Json;

namespace UniHome.IntegrationTests;

public sealed class OpenApiVerificationMultipartContractTests
    : IClassFixture<SwaggerEnabledApiFactory>
{
    private readonly HttpClient _client;

    public OpenApiVerificationMultipartContractTests(SwaggerEnabledApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/v1/expedientes/mi-expediente/ine")]
    [InlineData("/api/v1/expedientes/mi-expediente/comprobante")]
    public async Task CargaDocumental_DebeDeclararseComoMultipartBinario(string path)
    {
        using var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty(path)
            .GetProperty("post");
        var requestBody = operation.GetProperty("requestBody");
        Assert.True(requestBody.GetProperty("required").GetBoolean());
        var content = requestBody.GetProperty("content");

        Assert.True(content.TryGetProperty("multipart/form-data", out var multipart));
        Assert.False(content.TryGetProperty("application/x-www-form-urlencoded", out _));

        var schema = multipart.GetProperty("schema");
        Assert.Equal("object", schema.GetProperty("type").GetString());
        var properties = schema.GetProperty("properties");
        var file = properties.GetProperty("Archivo");
        Assert.Equal("string", file.GetProperty("type").GetString());
        Assert.Equal("binary", file.GetProperty("format").GetString());
        Assert.True(properties.TryGetProperty("NombreArchivoOriginal", out _));
        Assert.Contains(
            "Archivo",
            schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));

        var responses = operation.GetProperty("responses");

        foreach (var statusCode in new[] { "200", "401", "403", "404", "409", "413", "415", "422", "503" })
        {
            Assert.True(
                responses.TryGetProperty(statusCode, out _),
                $"Falta la respuesta {statusCode} en {path}.");
        }
    }
}
