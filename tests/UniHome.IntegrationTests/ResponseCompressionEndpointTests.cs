using System.Net;
using System.Net.Http.Headers;

namespace UniHome.IntegrationTests;

/// <summary>
/// Bloque 2 (auditoría): Brotli/Gzip solo para los mismos cuatro GET
/// públicos del output cache. Nunca para rutas autenticadas ni con datos
/// personales (mitigación de BREACH).
/// </summary>
public sealed class ResponseCompressionEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public ResponseCompressionEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("br")]
    [InlineData("gzip")]
    public async Task ExploracionMapa_ConAcceptEncoding_RespondeComprimido(string encoding)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/exploracion/mapa?universidadId=" + Guid.NewGuid());
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue(encoding));

        using var response = await _client.SendAsync(request);

        // 404 (universidad inexistente) es la respuesta esperada aquí; lo que
        // importa es la cabecera de compresión, no el código de estado.
        Assert.Equal(encoding, response.Content.Headers.ContentEncoding.FirstOrDefault());
    }

    [Fact]
    public async Task BusquedaUniversidades_ConAcceptEncodingBrotli_RespondeComprimido()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/universidades?buscar=x");
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("br", response.Content.Headers.ContentEncoding.FirstOrDefault());
    }

    [Fact]
    public async Task Perfil_RutaAutenticadaConDatosPersonales_NuncaSeComprimeAunConAcceptEncoding()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email);
        using var login = await AuthTestHelpers.LoginAsync(_client, email);
        var tokens = await AuthTestHelpers.ReadTokensAsync(login);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/perfil/mi-perfil");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task Login_RutaDeAutenticacion_NuncaSeComprimeAunConAcceptEncoding()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                correo = AuthTestHelpers.UniqueEmail(),
                contrasena = "no-importa"
            })
        };
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

        using var response = await _client.SendAsync(request);

        Assert.Empty(response.Content.Headers.ContentEncoding);
    }
}
