using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UniHome.IntegrationTests;

/// <summary>
/// Swagger UI y el documento OpenAPI solo se exponen con
/// <c>Api:EnableSwagger=true</c> (desactivado por defecto).
/// </summary>
public sealed class SwaggerUiTests :
    IClassFixture<UniHomeApiFactory>,
    IClassFixture<SwaggerEnabledApiFactory>
{
    private readonly UniHomeApiFactory _disabled;
    private readonly SwaggerEnabledApiFactory _enabled;

    public SwaggerUiTests(UniHomeApiFactory disabled, SwaggerEnabledApiFactory enabled)
    {
        _disabled = disabled;
        _enabled = enabled;
    }

    [Fact]
    public async Task ConLaOpcionDesactivada_SwaggerYOpenApi_Dan404()
    {
        using var client = _disabled.CreateClient();

        using var swagger = await client.GetAsync("/swagger");
        Assert.Equal(HttpStatusCode.NotFound, swagger.StatusCode);

        using var openApi = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.NotFound, openApi.StatusCode);
    }

    [Fact]
    public async Task ConLaOpcionActivada_SwaggerYOpenApi_Responden200()
    {
        using var client = _enabled.CreateClient();

        using var swagger = await client.GetAsync("/swagger");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);

        using var openApi = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
    }

    [Fact]
    public async Task ConLaOpcionActivada_RaizRedirigeASwagger()
    {
        using var client = _enabled.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/");

        Assert.True(
            response.StatusCode == HttpStatusCode.Redirect ||
            response.StatusCode == HttpStatusCode.Moved,
            $"Se esperaba una redirección, se obtuvo {response.StatusCode}.");
        Assert.StartsWith("/swagger", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConLaOpcionDesactivada_Raiz_NoRedirige()
    {
        using var client = _disabled.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
