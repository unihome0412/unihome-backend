using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UniHome.Domain.Accounts;

namespace UniHome.IntegrationTests;

public sealed class ResendConfirmationEndpointTests : IClassFixture<UniHomeApiFactory>
{
    private readonly UniHomeApiFactory _factory;
    private readonly HttpClient _client;

    public ResendConfirmationEndpointTests(UniHomeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Reenviar_ConCuentaSinConfirmar_DebeResponder202YEnviarNuevoCodigo()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedUnconfirmedAccountWithVerificationAsync(_factory, email);
        var before = _factory.EmailSender.Messages.Count(message => message.Email == email);

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/confirmacion-correo/reenvio",
            new { correo = email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Empty(body);

        var after = _factory.EmailSender.Messages.Count(message => message.Email == email);
        Assert.Equal(before + 1, after);
    }

    [Fact]
    public async Task Reenviar_ConCorreoInexistente_DebeResponder202SinEnviarCorreo()
    {
        var email = AuthTestHelpers.UniqueEmail();

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/confirmacion-correo/reenvio",
            new { correo = email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.DoesNotContain(_factory.EmailSender.Messages, message => message.Email == email);
    }

    [Fact]
    public async Task Reenviar_SobreCuentaYaConfirmada_DebeResponder202SinEnviarCorreo()
    {
        var email = AuthTestHelpers.UniqueEmail();
        await AuthTestHelpers.SeedAccountAsync(_factory, email, emailConfirmed: true);

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/confirmacion-correo/reenvio",
            new { correo = email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.DoesNotContain(_factory.EmailSender.Messages, message => message.Email == email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("correo-invalido")]
    public async Task Reenviar_ConCorreoMalFormado_DebeResponder400(string? email)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/confirmacion-correo/reenvio",
            new { correo = email });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("correo", out _));
    }

    [Fact]
    public async Task Reenviar_ConCuerpoMayorAlLimite_DebeResponder413()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/confirmacion-correo/reenvio",
            new { correo = new string('a', 16 * 1024) });

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }
}
