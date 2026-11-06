using System.Net;
using System.Text;
using System.Text.Json;

namespace UniHome.IntegrationTests;

public sealed class ProblemDetailsTests : IClassFixture<UniHomeApiFactory>
{
    private readonly HttpClient _client;

    public ProblemDetailsTests(UniHomeApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RutaInexistente_DebeResponder404ConProblemDetailsYTraceId()
    {
        using var response = await _client.GetAsync("/api/v1/ruta-inexistente");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidacionInvalida_DebeResponder400ConErrorsYTraceId()
    {
        using var content = new StringContent(
            """{"email":"no-es-un-correo"}""",
            Encoding.UTF8,
            "application/json");

        using var response = await _client.PostAsync("/__test/validation", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.NotEmpty(root.GetProperty("errors").EnumerateObject());
    }

    [Fact]
    public async Task JsonMalformado_DebeResponder400ConProblemDetails()
    {
        using var content = new StringContent("{", Encoding.UTF8, "application/json");

        using var response = await _client.PostAsync("/__test/validation", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, document.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task ExcepcionNoControlada_DebeResponder500SinDetallesInternos()
    {
        using var response = await _client.GetAsync("/__test/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("Mensaje sensible", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }
}
