using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace UniHome.IntegrationTests;

public sealed class AuthRateLimitTests : IClassFixture<RateLimitedApiFactory>
{
    private readonly HttpClient _client;

    public AuthRateLimitTests(RateLimitedApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_ExcedeElLimitePorIp_DebeResponder429ProblemDetails()
    {
        using var first = await LoginAsync();
        using var second = await LoginAsync();
        using var third = await LoginAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal(
            "application/problem+json",
            third.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await third.Content.ReadAsStringAsync());
        Assert.Equal(429, document.RootElement.GetProperty("status").GetInt32());
        Assert.Contains(
            "Demasiados intentos",
            document.RootElement.GetProperty("detail").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_ExcedeElLimitePorIp_DebeResponder429()
    {
        using var first = await RefreshAsync();
        using var second = await RefreshAsync();
        using var third = await RefreshAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal(
            "application/problem+json",
            third.Content.Headers.ContentType?.MediaType);
    }

    private Task<HttpResponseMessage> LoginAsync() =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                correo = AuthTestHelpers.UniqueEmail(),
                contrasena = "OtraClave1"
            });

    private Task<HttpResponseMessage> RefreshAsync() =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = "token-invalido" });
}
